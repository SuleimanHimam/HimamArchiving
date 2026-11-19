using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>
/// المحاضر (§6.1): drafts, submission into the type's approval path, receipt confirmation for transfers
/// between warehouses. Execution — the stock and custody effect — happens in <see cref="SupplyDocumentEngine"/>.
/// </summary>
public sealed class SupplyDocumentService(
    AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit, IApprovalService approvals,
    INumberingService numbering) : ISupplyDocumentService
{
    public const string RequestType = "supply.document";
    private const string NotFound = "المحضر غير موجود";

    public async Task<PagedResult<SupplyDocumentDto>> ListAsync(DocumentQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);
        if (Enum.TryParse<DocumentType>(q.Type, true, out var t)) query = query.Where(d => d.Type == t);
        if (Enum.TryParse<DocumentStatus>(q.Status, true, out var s)) query = query.Where(d => d.Status == s);
        var search = CoreGuards.Clean(q.Search);
        if (search.Length > 0) query = query.Where(d => (d.Number != null && d.Number.Contains(search)) || d.Title.Contains(search));
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var rows = await query.Include(d => d.Lines).OrderByDescending(d => d.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<SupplyDocumentDto> { Items = await ToDtosAsync(db, rows, ct), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<DocumentCountsDto> CountsAsync(CancellationToken ct = default)
    {
        var rows = await (await ScopedAsync(ct)).GroupBy(d => d.Type).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        return new DocumentCountsDto(rows.ToDictionary(r => r.Key.ToString(), r => r.N), rows.Sum(r => r.N));
    }

    public async Task<Result<SupplyDocumentDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var d = await (await ScopedAsync(ct)).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? Result<SupplyDocumentDto>.Fail(NotFound) : Result<SupplyDocumentDto>.Ok((await ToDtosAsync(db, [d], ct))[0]);
    }

    public async Task<Result<SupplyDocumentDto>> CreateAsync(SaveDocumentRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DocumentType>(r.Type, true, out var type) || !DocumentRules.IsManual(type))
            return Result<SupplyDocumentDto>.Fail("نوع المحضر غير صالح");
        var d = new SupplyDocument { Type = type };
        var error = await ApplyAsync(d, r, ct);
        if (error is not null) return Result<SupplyDocumentDto>.Fail(error);
        db.SupplyDocuments.Add(d);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "SupplyDocument", d.Id, $"{type}: {d.Title}", ct: ct);
        return await GetAsync(d.Id, ct);
    }

    public async Task<Result<SupplyDocumentDto>> UpdateAsync(long id, SaveDocumentRequest r, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return Result<SupplyDocumentDto>.Fail(NotFound);
        if (d.Status is not (DocumentStatus.Draft or DocumentStatus.Returned)) return Result<SupplyDocumentDto>.Fail("لا يُعدَّل المحضر بعد تقديمه");
        if (!DocumentRules.IsManual(d.Type)) return Result<SupplyDocumentDto>.Fail("هذا المحضر يُولَّد آلياً ولا يُعدَّل يدوياً");
        if (!CoreGuards.IsCurrent(d.RowVersion, r.RowVersion)) return Result<SupplyDocumentDto>.Fail(CoreGuards.Stale);
        var error = await ApplyAsync(d, r with { Type = d.Type.ToString() }, ct);
        if (error is not null) return Result<SupplyDocumentDto>.Fail(error);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<SupplyDocumentDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "SupplyDocument", d.Id, d.Title, ct: ct);
        return await GetAsync(d.Id, ct);
    }

    public async Task<Result<SupplyDocumentDto>> SubmitAsync(long id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return Result<SupplyDocumentDto>.Fail(NotFound);
        var result = await SubmitCoreAsync(db, approvals, numbering, audit, d, ct);
        return result is null ? await GetAsync(d.Id, ct) : Result<SupplyDocumentDto>.Fail(result);
    }

    /// <summary>Shared by the count (ADJ) and opening (OPN) flows. Returns an error, or null on success.</summary>
    internal static async Task<string?> SubmitCoreAsync(AppDbContext db, IApprovalService approvals, INumberingService numbering,
        IAuditWriter audit, SupplyDocument d, CancellationToken ct)
    {
        if (d.Status is not (DocumentStatus.Draft or DocumentStatus.Returned)) return "المحضر مقدّم مسبقاً أو مغلق";
        if (d.Lines.Count == 0) return "المحضر بلا أسطر";
        var settings = await SupplyCommon.SettingsAsync(db, ct);
        if (SupplyCommon.Long(settings, SupplySettingKeys.Workflow(d.Type.ToString())) is not { } workflowId)
            return "لم يُحدَّد مسار اعتماد لهذا النوع من المحاضر. يحدده مدير اللوازم من إعدادات اللوازم";
        if (d.Type == DocumentType.OPN && settings.ContainsKey(SupplySettingKeys.OpeningLockedOn)
            && settings[SupplySettingKeys.OpeningLockedOn].Length > 0)
            return "تاريخ الافتتاح مقفل: ما يُكتشف لاحقاً يدخل بمحضر تسوية جرد";

        return await db.InTransactionAsync<string?>(async () =>
        {
            d.Number ??= await SupplyCommon.NumberAsync(numbering, SupplyCommon.DocumentNumbering(d.Type), "المحاضر", ct);
            var submitted = await approvals.SubmitAsync(new SubmitApprovalRequest(RequestType, "Supply", d.Id, $"{d.Number}: {d.Title}", workflowId), ct);
            if (!submitted.Succeeded) return submitted.Error;
            d.Status = DocumentStatus.Submitted;
            d.ApprovalRequestId = submitted.Value!.Id;
            d.DecisionNote = null;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Submitted", "SupplyDocument", d.Id, d.Number, ct: ct);
            return null;
        }, e => e is null, ct);
    }

    public async Task<Result<SupplyDocumentDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return Result<SupplyDocumentDto>.Fail(NotFound);
        if (d.Status is not (DocumentStatus.Draft or DocumentStatus.Returned)) return Result<SupplyDocumentDto>.Fail("لا يُلغى إلا محضر مسودة أو معاد");
        d.Status = DocumentStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "SupplyDocument", d.Id, d.Number ?? d.Title, ct: ct);
        return await GetAsync(d.Id, ct);
    }

    public async Task<Result<SupplyDocumentDto>> ConfirmReceiptAsync(long id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return Result<SupplyDocumentDto>.Fail(NotFound);
        if (d.Type != DocumentType.MOV || d.Status != DocumentStatus.AwaitingReceipt)
            return Result<SupplyDocumentDto>.Fail("المحضر ليس بانتظار تأكيد الاستلام");
        var mine = await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct);
        if (!mine.Contains(d.TargetWarehouseId!.Value)) return Result<SupplyDocumentDto>.Fail("التأكيد لأمين المستودع المستلم");
        try
        {
            await db.InTransactionAsync(async () =>
            {
                await new SupplyDocumentEngine(db, numbering).ExecuteAsync(d, ct);
                await audit.WriteAsync("ReceiptConfirmed", "SupplyDocument", d.Id, d.Number, ct: ct);
                return true;
            }, _ => true, ct);
        }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Result<SupplyDocumentDto>.Fail(ex.Message); }
        await SupplyCommon.NotifyEmployeeAsync(db, await CreatorEmployeeAsync(d, ct), "تم استلام النقل", d.Number ?? string.Empty, "SupplyDocument", d.Id, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(d.Id, ct);
    }

    public async Task<Result<bool>> PrintedAsync(long id, CancellationToken ct = default)
    {
        var d = await (await ScopedAsync(ct)).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return Result<bool>.Fail(NotFound);
        await audit.WriteAsync("Printed", "SupplyDocument", d.Id, d.Number ?? d.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- validation ----

    private async Task<string?> ApplyAsync(SupplyDocument d, SaveDocumentRequest r, CancellationToken ct)
    {
        var title = CoreGuards.Clean(r.Title);
        if (title.Length > 500) return "البيان أطول من 500 حرف";
        var header = new DocumentRules.Header(d.Type, r.WarehouseId, r.TargetWarehouseId, r.RecipientEmployeeId, r.RecipientOrgUnitId, r.Supplier, title);
        if (DocumentRules.Validate(header) is { } headerError) return headerError;
        if (r.DocDate == default) return "تاريخ المحضر مطلوب";
        if (r.Lines.Count is 0 or > 500) return "أضف صنفاً واحداً على الأقل (500 سطر كحد أقصى)";

        var mine = await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct);
        foreach (var w in new[] { r.WarehouseId, d.Type == DocumentType.MOV ? null : r.TargetWarehouseId })
            if (w is { } wid && !mine.Contains(wid)) return "المستودع خارج نطاق صلاحيتك";
        if (r.TargetWarehouseId is { } tw && !await db.Warehouses.AnyAsync(x => x.Id == tw && x.IsActive, ct)) return "المستودع المستلم غير موجود";
        if (r.WarehouseId is { } sw && !await db.Warehouses.AnyAsync(x => x.Id == sw && x.IsActive, ct)) return "المستودع غير موجود";
        if (r.RecipientEmployeeId is { } re && !await db.Employees.AnyAsync(e => e.Id == re && e.Status == EmployeeStatus.Active, ct))
            return "المستلم غير موجود أو ليس على رأس عمله";
        if (r.RecipientOrgUnitId is { } ru && !await db.OrgUnits.AnyAsync(u => u.Id == ru, ct)) return "الدائرة غير موجودة";

        var itemIds = r.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var assetIds = r.Lines.Where(l => l.AssetId != null).Select(l => l.AssetId!.Value).ToList();
        if (assetIds.Count != assetIds.Distinct().Count()) return "القطعة نفسها مكررة في المحضر";
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        var lines = new List<SupplyDocumentLine>();
        foreach (var (l, i) in r.Lines.Select((l, i) => (l, i + 1)))
        {
            if (!items.TryGetValue(l.ItemId, out var item)) return $"السطر {i}: الصنف غير موجود";
            if (!item.IsActive && d.Type is DocumentType.RCV or DocumentType.ISS) return $"السطر {i}: الصنف غير مفعّل";
            var isAsset = item.Kind == ItemKind.FixedAsset;
            AssetCondition? condition = null;
            if (l.Condition is { Length: > 0 } cs)
            {
                if (!Enum.TryParse<AssetCondition>(cs, true, out var c)) return $"السطر {i}: الحالة غير صالحة";
                condition = c;
            }
            var qty = l.Quantity;
            if (isAsset)
            {
                if (d.Type == DocumentType.RCV)
                {
                    if (l.AssetId is not null) return $"السطر {i}: الاستلام ينشئ قطعة جديدة";
                    qty = 1;
                }
                else
                {
                    if (l.AssetId is not { } aid || !assets.TryGetValue(aid, out var a)) return $"السطر {i}: اختر القطعة بالباركود ({item.Name})";
                    if (a.ItemId != item.Id) return $"السطر {i}: القطعة لا تتبع الصنف";
                    qty = 1;
                }
            }
            else
            {
                if (d.Type == DocumentType.TRF) return $"السطر {i}: نقل العهدة للأصول الثابتة فقط";
                if (l.AssetId is not null) return $"السطر {i}: الصنف يُتتبع بالكمية لا بالقطعة";
                if (qty is <= 0 or > 1_000_000) return $"السطر {i}: الكمية غير صالحة";
            }
            if (l.UnitValue is < 0) return $"السطر {i}: القيمة غير صالحة";
            var serial = CoreGuards.CleanOrNull(l.SerialNumber);
            if (serial?.Length > 100) return $"السطر {i}: الرقم التسلسلي طويل";
            var note = CoreGuards.CleanOrNull(l.Note);
            if (note?.Length > 500) return $"السطر {i}: الملاحظة طويلة";
            lines.Add(new SupplyDocumentLine
            {
                ItemId = item.Id, Quantity = qty, AssetId = isAsset && d.Type != DocumentType.RCV ? l.AssetId : null,
                SerialNumber = d.Type == DocumentType.RCV ? serial : null, Condition = condition,
                UnitValue = l.UnitValue is { } v ? decimal.Round(v, 2, MidpointRounding.AwayFromZero) : null,
                PurchaseDate = l.PurchaseDate, Note = note,
            });
        }
        if (d.Type == DocumentType.RCV)
        {
            var serials = lines.Where(l => l.SerialNumber != null).Select(l => (l.ItemId, l.SerialNumber)).ToList();
            if (serials.Count != serials.Distinct().Count()) return "رقم تسلسلي مكرر في المحضر";
        }

        d.DocDate = r.DocDate;
        d.Title = title;
        d.Notes = CoreGuards.CleanOrNull(r.Notes);
        d.WarehouseId = d.Type == DocumentType.TRF ? null : r.WarehouseId;
        d.TargetWarehouseId = d.Type == DocumentType.MOV ? r.TargetWarehouseId : null;
        d.RecipientEmployeeId = d.Type is DocumentType.ISS or DocumentType.TRF ? r.RecipientEmployeeId : null;
        d.RecipientOrgUnitId = d.Type is DocumentType.ISS or DocumentType.TRF ? r.RecipientOrgUnitId : null;
        d.Supplier = d.Type == DocumentType.RCV ? CoreGuards.CleanOrNull(r.Supplier) : null;
        d.InvoiceNumber = d.Type == DocumentType.RCV ? CoreGuards.CleanOrNull(r.InvoiceNumber) : null;
        d.ReceivedByName = CoreGuards.CleanOrNull(r.ReceivedByName);
        // Lines are replaced wholesale on a draft; soft-deleted rows stay in the table.
        foreach (var old in d.Lines.ToList()) db.SupplyDocumentLines.Remove(old);
        d.Lines.Clear();
        foreach (var l in lines) d.Lines.Add(l);
        return null;
    }

    // ---- scope ----

    /// <summary>Supply manager: everything. Keeper: documents touching their warehouses. Anyone: what they created.</summary>
    private async Task<IQueryable<SupplyDocument>> ScopedAsync(CancellationToken ct)
    {
        var q = db.SupplyDocuments.AsNoTracking();
        if (await SupplyCommon.SeesAllAsync(scope, ct)) return q;
        var mine = (await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).ToList();
        var uid = user.UserId ?? 0;
        return q.Where(d => d.CreatedBy == uid || (d.WarehouseId != null && mine.Contains(d.WarehouseId.Value))
            || (d.TargetWarehouseId != null && mine.Contains(d.TargetWarehouseId.Value)));
    }

    private async Task<SupplyDocument?> FindTrackedAsync(long id, CancellationToken ct)
    {
        if (!await (await ScopedAsync(ct)).AnyAsync(d => d.Id == id, ct)) return null;
        return await db.SupplyDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    private async Task<long?> CreatorEmployeeAsync(SupplyDocument d, CancellationToken ct) =>
        d.CreatedBy is { } uid ? await db.Employees.Where(e => e.UserId == uid).Select(e => (long?)e.Id).FirstOrDefaultAsync(ct) : null;

    internal static async Task<IReadOnlyList<SupplyDocumentDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<SupplyDocument> rows, CancellationToken ct)
    {
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        var lines = rows.SelectMany(d => d.Lines).ToList();
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var assetIds = lines.Where(l => l.AssetId != null).Select(l => l.AssetId!.Value).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.AssetNumber, ct);
        var employees = await HrCommon.NamesAsync(db, rows.Where(d => d.RecipientEmployeeId != null).Select(d => d.RecipientEmployeeId!.Value), ct);
        var units = await SupplyCommon.UnitNamesAsync(db, rows.Where(d => d.RecipientOrgUnitId != null).Select(d => d.RecipientOrgUnitId!.Value), ct);
        var userIds = rows.Where(d => d.CreatedBy != null).Select(d => d.CreatedBy!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(d => new SupplyDocumentDto(d.Id, d.Number, d.Type.ToString(), d.Status.ToString(), d.DocDate, d.Title, d.Notes,
            d.WarehouseId, d.WarehouseId is { } w ? whs.GetValueOrDefault(w) : null,
            d.TargetWarehouseId, d.TargetWarehouseId is { } t ? whs.GetValueOrDefault(t) : null,
            d.RecipientEmployeeId, d.RecipientOrgUnitId,
            d.RecipientEmployeeId is { } e ? employees.GetValueOrDefault(e) : d.RecipientOrgUnitId is { } u ? units.GetValueOrDefault(u) : null,
            d.Supplier, d.InvoiceNumber, d.ReceivedByName, d.IssueRequestId, d.CountCycleId, d.WorkOrderId, d.ApprovalRequestId,
            d.ExecutedAt, d.DecisionNote, d.CreatedBy is { } cb ? users.GetValueOrDefault(cb) : null, d.CreatedAt,
            d.Lines.OrderBy(l => l.Id).Select(l =>
            {
                var item = items.GetValueOrDefault(l.ItemId);
                return new DocumentLineDto(l.Id, l.ItemId, item?.Name ?? string.Empty, item?.Kind.ToString() ?? string.Empty, item?.Unit ?? string.Empty,
                    l.Quantity, l.AssetId, l.AssetId is { } a ? assets.GetValueOrDefault(a) : null, l.SerialNumber, l.Barcode,
                    l.Condition?.ToString(), l.UnitValue, l.PurchaseDate, l.WarehouseId, l.HolderEmployeeId, l.HolderOrgUnitId, l.Note);
            }).ToList(),
            HrCommon.B64(d.RowVersion))).ToList();
    }
}

/// <summary>
/// Approved document → executed in the approval transaction (MOV → awaiting the receiving keeper).
/// ADJ closes its count; OPN marks its import. A broken rule throws and the approval rolls back.
/// </summary>
public sealed class SupplyDocumentEffectHandler(AppDbContext db, INumberingService numbering, IAuditWriter audit) : IApprovalEffect
{
    public string RequestType => SupplyDocumentService.RequestType;

    public async Task OnApprovedAsync(ApprovalRequest request, CancellationToken ct)
    {
        var d = await LoadAsync(request, ct);
        if (d.Status != DocumentStatus.Submitted) throw new DomainException("المحضر ليس بانتظار الاعتماد");
        if (d.Type == DocumentType.MOV)
        {
            await new SupplyDocumentEngine(db, numbering).EnsureNotFrozenAsync(d, ct);
            d.Status = DocumentStatus.AwaitingReceipt;
            await SupplyCommon.NotifyKeeperAsync(db, d.TargetWarehouseId!.Value, "نقل بانتظار تأكيد الاستلام", d.Number ?? d.Title, "SupplyDocument", d.Id, ct);
            await db.SaveChangesAsync(ct);
        }
        else await new SupplyDocumentEngine(db, numbering).ExecuteAsync(d, ct);

        if (d.Type == DocumentType.ADJ && d.CountCycleId is { } cid)
        {
            var cycle = await db.CountCycles.FirstAsync(c => c.Id == cid, ct);
            cycle.Status = CountStatus.Closed;   // also lifts the freeze
            cycle.ClosedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await NotifyCreatorAsync(d, "اعتُمد المحضر", d.Number ?? d.Title, ct);
        await audit.WriteAsync("Approved", "SupplyDocument", d.Id, d.Number, ct: ct);
    }

    public Task OnRejectedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        DecideAsync(request, DocumentStatus.Rejected, reason, "رُفض المحضر", ct);

    public Task OnReturnedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        DecideAsync(request, DocumentStatus.Returned, reason, "أُعيد المحضر للتعديل", ct);

    private async Task DecideAsync(ApprovalRequest request, DocumentStatus status, string reason, string title, CancellationToken ct)
    {
        var d = await LoadAsync(request, ct);
        d.Status = status;
        d.DecisionNote = reason;
        // A refused settlement sends its count back to the justification step; settling again builds a fresh ADJ.
        if (d.Type == DocumentType.ADJ && d.CountCycleId is { } cid)
        {
            var cycle = await db.CountCycles.FirstAsync(c => c.Id == cid, ct);
            cycle.Status = CountStatus.Reconciled;
            cycle.AdjustmentDocumentId = null;
            if (status == DocumentStatus.Returned) d.Status = DocumentStatus.Cancelled;
        }
        await NotifyCreatorAsync(d, title, $"{d.Number}: {reason}", ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(status.ToString(), "SupplyDocument", d.Id, d.Number, newValues: reason, ct: ct);
    }

    private async Task NotifyCreatorAsync(SupplyDocument d, string title, string body, CancellationToken ct)
    {
        if (d.CreatedBy is { } uid) HrCommon.Notify(db, uid, title, body, "SupplyDocument", d.Id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SupplyDocument> LoadAsync(ApprovalRequest request, CancellationToken ct) =>
        await db.SupplyDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == request.SourceId, ct)
        ?? throw new DomainException("المحضر غير موجود");
}
