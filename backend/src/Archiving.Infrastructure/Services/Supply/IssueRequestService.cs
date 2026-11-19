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
/// طلب الصرف (§6.3, slide 29): a request goes through its approval path (direct manager → supply manager by
/// default, as configured), then the keeper issues what is on hand as an executed SUP-ISS. Partial issue
/// leaves the rest pending; nothing on hand suspends the request with a shortage alert.
/// </summary>
public sealed class IssueRequestService(
    AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit, IApprovalService approvals,
    INumberingService numbering) : IIssueRequestService
{
    public const string RequestType = "supply.issue";
    private const string NotFound = "الطلب غير موجود";

    public async Task<PagedResult<IssueRequestDto>> ListAsync(IssueRequestQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(q.Mine, ct);
        if (Enum.TryParse<IssueRequestStatus>(q.Status, true, out var s)) query = query.Where(r => r.Status == s);
        else if (q.Status == "Pending")
            query = query.Where(r => r.Status == IssueRequestStatus.Approved || r.Status == IssueRequestStatus.PartiallyIssued
                || r.Status == IssueRequestStatus.Suspended);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var rows = await query.Include(r => r.Lines).OrderByDescending(r => r.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<IssueRequestDto> { Items = await ToDtosAsync(db, rows, ct), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<IssueRequestDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var r = await (await ScopedAsync(false, ct)).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? Result<IssueRequestDto>.Fail(NotFound) : Result<IssueRequestDto>.Ok((await ToDtosAsync(db, [r], ct))[0]);
    }

    public async Task<Result<IssueRequestDto>> CreateAsync(SaveIssueRequest r, CancellationToken ct = default)
    {
        var (employee, error) = await RequesterAsync(r.EmployeeId, ct);
        if (employee is null) return Result<IssueRequestDto>.Fail(error!);
        var req = new IssueRequest { RequesterEmployeeId = employee.Id, OrgUnitId = employee.OrgUnitId };
        var applyError = await ApplyAsync(req, r, ct);
        if (applyError is not null) return Result<IssueRequestDto>.Fail(applyError);
        db.IssueRequests.Add(req);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "IssueRequest", req.Id, employee.FullName, ct: ct);
        return await GetAsync(req.Id, ct);
    }

    public async Task<Result<IssueRequestDto>> UpdateAsync(long id, SaveIssueRequest r, CancellationToken ct = default)
    {
        var req = await FindTrackedAsync(id, ct);
        if (req is null) return Result<IssueRequestDto>.Fail(NotFound);
        if (req.Status is not (IssueRequestStatus.Draft or IssueRequestStatus.Returned)) return Result<IssueRequestDto>.Fail("لا يُعدَّل الطلب بعد تقديمه");
        if (!CoreGuards.IsCurrent(req.RowVersion, r.RowVersion)) return Result<IssueRequestDto>.Fail(CoreGuards.Stale);
        var applyError = await ApplyAsync(req, r, ct);
        if (applyError is not null) return Result<IssueRequestDto>.Fail(applyError);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<IssueRequestDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "IssueRequest", req.Id, req.Number, ct: ct);
        return await GetAsync(req.Id, ct);
    }

    public async Task<Result<IssueRequestDto>> SubmitAsync(long id, CancellationToken ct = default)
    {
        var req = await FindTrackedAsync(id, ct);
        if (req is null) return Result<IssueRequestDto>.Fail(NotFound);
        var error = await SubmitCoreAsync(req, ct);
        return error is null ? await GetAsync(req.Id, ct) : Result<IssueRequestDto>.Fail(error);
    }

    private async Task<string?> SubmitCoreAsync(IssueRequest req, CancellationToken ct)
    {
        if (req.Status is not (IssueRequestStatus.Draft or IssueRequestStatus.Returned)) return "الطلب مقدّم مسبقاً أو مغلق";
        if (req.Lines.Count == 0) return "الطلب بلا أصناف";
        var settings = await SupplyCommon.SettingsAsync(db, ct);
        if (SupplyCommon.Long(settings, SupplySettingKeys.IssueRequestWorkflow) is not { } workflowId)
            return "لم يُحدَّد مسار اعتماد طلبات الصرف. يحدده مدير اللوازم من إعدادات اللوازم";
        var name = await db.Employees.Where(e => e.Id == req.RequesterEmployeeId).Select(e => e.FullName).FirstAsync(ct);
        return await db.InTransactionAsync<string?>(async () =>
        {
            req.Number ??= await SupplyCommon.NumberAsync(numbering, SupplyCommon.IssueRequestNumbering, "طلبات الصرف", ct);
            var submitted = await approvals.SubmitAsync(new SubmitApprovalRequest(RequestType, "Supply", req.Id, $"طلب صرف {req.Number}: {name}", workflowId), ct);
            if (!submitted.Succeeded) return submitted.Error;
            req.Status = IssueRequestStatus.Submitted;
            req.ApprovalRequestId = submitted.Value!.Id;
            req.DecisionNote = null;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Submitted", "IssueRequest", req.Id, req.Number, ct: ct);
            return null;
        }, e => e is null, ct);
    }

    public async Task<Result<IssueRequestDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var req = await FindTrackedAsync(id, ct);
        if (req is null) return Result<IssueRequestDto>.Fail(NotFound);
        var keeper = (await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).Contains(req.WarehouseId);
        // The requester withdraws a draft; the keeper closes what is left of an approved request (the rest will not come).
        var allowed = req.Status is IssueRequestStatus.Draft or IssueRequestStatus.Returned
            || (keeper && req.Status is IssueRequestStatus.Approved or IssueRequestStatus.PartiallyIssued or IssueRequestStatus.Suspended);
        if (!allowed) return Result<IssueRequestDto>.Fail("لا يمكن إلغاء الطلب في حالته الحالية");
        req.Status = req.Lines.Any(l => l.IssuedQuantity > 0) ? IssueRequestStatus.Issued : IssueRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "IssueRequest", req.Id, req.Number, ct: ct);
        return await GetAsync(req.Id, ct);
    }

    public async Task<Result<IssueRequestDto>> FulfilAsync(long id, FulfilIssueRequest f, CancellationToken ct = default)
    {
        var req = await FindTrackedAsync(id, ct);
        if (req is null) return Result<IssueRequestDto>.Fail(NotFound);
        if (req.Status is not (IssueRequestStatus.Approved or IssueRequestStatus.PartiallyIssued or IssueRequestStatus.Suspended))
            return Result<IssueRequestDto>.Fail("الطلب ليس بانتظار التجهيز");
        if (!CoreGuards.IsCurrent(req.RowVersion, f.RowVersion)) return Result<IssueRequestDto>.Fail(CoreGuards.Stale);
        if (!(await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).Contains(req.WarehouseId))
            return Result<IssueRequestDto>.Fail("التجهيز لأمين المستودع المختص");

        var items = await db.Items.AsNoTracking().Where(i => req.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var doc = new SupplyDocument
        {
            Type = DocumentType.ISS, DocDate = SupplyCommon.Today(), WarehouseId = req.WarehouseId, IssueRequestId = req.Id,
            Title = $"صرف بموجب الطلب {req.Number}", ReceivedByName = CoreGuards.CleanOrNull(f.ReceivedByName),
            RecipientOrgUnitId = req.OrgUnitId, RecipientEmployeeId = req.ForDepartment ? null : req.RequesterEmployeeId,
        };
        foreach (var fl in f.Lines.Where(x => x.Quantity > 0))
        {
            var line = req.Lines.FirstOrDefault(l => l.Id == fl.LineId);
            if (line is null) return Result<IssueRequestDto>.Fail("سطر غير موجود في الطلب");
            var item = items[line.ItemId];
            if (fl.Quantity > line.Quantity - line.IssuedQuantity) return Result<IssueRequestDto>.Fail($"{item.Name}: أكثر من المتبقي في الطلب");
            if (item.Kind == ItemKind.FixedAsset)
            {
                var picked = fl.AssetIds ?? [];
                if (fl.Quantity != Math.Floor(fl.Quantity) || picked.Count != (int)fl.Quantity)
                    return Result<IssueRequestDto>.Fail($"{item.Name}: امسح باركود كل قطعة تُصرف");
                foreach (var a in picked) doc.Lines.Add(new SupplyDocumentLine { ItemId = item.Id, Quantity = 1, AssetId = a });
            }
            else doc.Lines.Add(new SupplyDocumentLine { ItemId = item.Id, Quantity = fl.Quantity });
            line.IssuedQuantity += fl.Quantity;
        }
        if (doc.Lines.Count == 0) return Result<IssueRequestDto>.Fail("لم تُحدَّد كميات للصرف. إن لم يتوفر شيء فعلّق الطلب");

        try
        {
            await db.InTransactionAsync(async () =>
            {
                db.SupplyDocuments.Add(doc);
                doc.Number = await SupplyCommon.NumberAsync(numbering, SupplyCommon.DocumentNumbering(DocumentType.ISS), "المحاضر", ct);
                doc.Status = DocumentStatus.Submitted;   // authorised by the approved request: executes now
                await db.SaveChangesAsync(ct);
                await new SupplyDocumentEngine(db, numbering).ExecuteAsync(doc, ct);
                req.Status = req.Lines.All(l => l.IssuedQuantity >= l.Quantity) ? IssueRequestStatus.Issued : IssueRequestStatus.PartiallyIssued;
                req.SuspendedAt = null;
                await SupplyCommon.NotifyEmployeeAsync(db, req.RequesterEmployeeId, "صُرف طلبك",
                    $"{req.Number} — {doc.Number}", "IssueRequest", req.Id, ct);
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync("Issued", "IssueRequest", req.Id, req.Number, newValues: doc.Number, ct: ct);
                return true;
            }, _ => true, ct);
        }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Result<IssueRequestDto>.Fail(ex.Message); }
        return await GetAsync(req.Id, ct);
    }

    public async Task<Result<IssueRequestDto>> SuspendAsync(long id, string reason, CancellationToken ct = default)
    {
        var req = await FindTrackedAsync(id, ct);
        if (req is null) return Result<IssueRequestDto>.Fail(NotFound);
        if (req.Status is not (IssueRequestStatus.Approved or IssueRequestStatus.PartiallyIssued))
            return Result<IssueRequestDto>.Fail("الطلب ليس بانتظار التجهيز");
        var why = CoreGuards.Clean(reason);
        if (why.Length is 0 or > 1000) return Result<IssueRequestDto>.Fail("سبب التعليق مطلوب");
        if (!(await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).Contains(req.WarehouseId))
            return Result<IssueRequestDto>.Fail("التعليق لأمين المستودع المختص");
        req.Status = IssueRequestStatus.Suspended;
        req.SuspendedAt = DateTime.UtcNow;
        req.DecisionNote = why;
        await SupplyCommon.NotifyEmployeeAsync(db, req.RequesterEmployeeId, "عُلّق طلبك لنقص المخزون", $"{req.Number}: {why}", "IssueRequest", req.Id, ct);
        await SupplyCommon.NotifyKeeperAsync(db, req.WarehouseId, "تنبيه نقص", $"{req.Number}: {why}", "IssueRequest", req.Id, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Suspended", "IssueRequest", req.Id, req.Number, newValues: why, ct: ct);
        return await GetAsync(req.Id, ct);
    }

    public async Task<Result<QuotaRunResult>> RunQuotasAsync(int year, int month, CancellationToken ct = default)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) return Result<QuotaRunResult>.Fail("الشهر غير صالح");
        var run = year * 100L + month;
        var quotas = await db.MonthlyQuotas.AsNoTracking().Where(q => q.IsActive).ToListAsync(ct);
        int created = 0, skipped = 0;
        foreach (var g in quotas.GroupBy(q => (q.OrgUnitId, q.WarehouseId)))
        {
            if (await db.IssueRequests.AnyAsync(r => r.OrgUnitId == g.Key.OrgUnitId && r.WarehouseId == g.Key.WarehouseId && r.MonthlyQuotaRunId == run, ct))
            { skipped++; continue; }
            // The request is raised in the name of the unit's manager seat holder, else the caller.
            var requester = await ManagerOfAsync(g.Key.OrgUnitId, ct) ?? await HrCommon.CurrentEmployeeAsync(db, user, ct);
            if (requester is null) { skipped++; continue; }
            var req = new IssueRequest
            {
                RequesterEmployeeId = requester.Id, OrgUnitId = g.Key.OrgUnitId, ForDepartment = true, WarehouseId = g.Key.WarehouseId,
                Justification = $"الحصة الشهرية {month:00}/{year}", MonthlyQuotaRunId = run,
            };
            foreach (var q in g) req.Lines.Add(new IssueRequestLine { ItemId = q.ItemId, Quantity = q.Quantity });
            db.IssueRequests.Add(req);
            await db.SaveChangesAsync(ct);
            var error = await SubmitCoreAsync(req, ct);
            if (error is not null) return Result<QuotaRunResult>.Fail(error);
            created++;
        }
        await audit.WriteAsync("QuotaRun", "IssueRequest", run, $"{month:00}/{year}", newValues: $"created={created}, skipped={skipped}", ct: ct);
        return Result<QuotaRunResult>.Ok(new QuotaRunResult(created, skipped));
    }

    // ---- helpers ----

    private async Task<string?> ApplyAsync(IssueRequest req, SaveIssueRequest r, CancellationToken ct)
    {
        var why = CoreGuards.Clean(r.Justification);
        if (why.Length > 1000) return "المبرر أطول من 1000 حرف";
        if (r.Lines.Count is 0 or > 100) return "أضف صنفاً واحداً على الأقل";
        if (!await db.Warehouses.AnyAsync(w => w.Id == r.WarehouseId && w.IsActive, ct)) return "المستودع غير موجود";
        var ids = r.Lines.Select(l => l.ItemId).ToList();
        if (ids.Count != ids.Distinct().Count()) return "الصنف مكرر في الطلب";
        var items = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        foreach (var l in r.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var item)) return "صنف غير موجود أو غير مفعّل";
            if (l.Quantity is <= 0 or > 100000) return $"{item.Name}: الكمية غير صالحة";
            if (item.Kind == ItemKind.FixedAsset && l.Quantity != Math.Floor(l.Quantity)) return $"{item.Name}: الأصول بعدد صحيح";
        }
        req.ForDepartment = r.ForDepartment;
        req.WarehouseId = r.WarehouseId;
        req.Justification = why;
        foreach (var old in req.Lines.ToList()) db.IssueRequestLines.Remove(old);
        req.Lines.Clear();
        foreach (var l in r.Lines) req.Lines.Add(new IssueRequestLine { ItemId = l.ItemId, Quantity = l.Quantity });
        return null;
    }

    /// <summary>Null id = the caller; another employee needs SupplyRequests.Edit and must be in scope.</summary>
    private async Task<(Employee?, string?)> RequesterAsync(long? employeeId, CancellationToken ct)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (employeeId is null || employeeId == me?.Id)
            return me is null ? (null, "حسابك غير مرتبط بسجل موظف") : (me, null);
        if (!await scope.HasPermissionAsync("SupplyRequests.Edit", ct)) return (null, "لا تملك صلاحية تقديم طلب عن موظف آخر");
        var e = await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        return e is null ? (null, "الموظف غير موجود") : (e, null);
    }

    private async Task<Employee?> ManagerOfAsync(long orgUnitId, CancellationToken ct)
    {
        var seat = await db.OrgUnits.AsNoTracking().Where(u => u.Id == orgUnitId).Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct);
        return seat is { } s ? await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.PositionId == s && e.Status == EmployeeStatus.Active, ct) : null;
    }

    /// <summary>Own requests; with SupplyRequests.View also the units in scope and the caller's warehouses.</summary>
    private async Task<IQueryable<IssueRequest>> ScopedAsync(bool mineOnly, CancellationToken ct)
    {
        var q = db.IssueRequests.AsNoTracking();
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id ?? 0;
        if (mineOnly) return q.Where(r => r.RequesterEmployeeId == me);
        if (await SupplyCommon.SeesAllAsync(scope, ct)) return q;
        if (!await scope.HasPermissionAsync("SupplyRequests.View", ct)) return q.Where(r => r.RequesterEmployeeId == me);
        var whs = (await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).ToList();
        var units = (await scope.OrgUnitIdsAsync(ct)).Concat(await scope.ManagedOrgUnitIdsAsync(ct)).Distinct().ToList();
        return q.Where(r => r.RequesterEmployeeId == me || whs.Contains(r.WarehouseId) || units.Contains(r.OrgUnitId));
    }

    private async Task<IssueRequest?> FindTrackedAsync(long id, CancellationToken ct)
    {
        if (!await (await ScopedAsync(false, ct)).AnyAsync(r => r.Id == id, ct)) return null;
        return await db.IssueRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    internal static async Task<IReadOnlyList<IssueRequestDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<IssueRequest> rows, CancellationToken ct)
    {
        var names = await HrCommon.NamesAsync(db, rows.Select(r => r.RequesterEmployeeId), ct);
        var units = await SupplyCommon.UnitNamesAsync(db, rows.Select(r => r.OrgUnitId), ct);
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        var itemIds = rows.SelectMany(r => r.Lines).Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var reqIds = rows.Select(r => r.Id).ToList();
        var docs = await db.SupplyDocuments.AsNoTracking().Where(d => d.IssueRequestId != null && reqIds.Contains(d.IssueRequestId.Value))
            .Select(d => new { d.Id, R = d.IssueRequestId!.Value }).ToListAsync(ct);
        var balances = new Dictionary<long, Dictionary<long, decimal>>();
        foreach (var wh in rows.Select(r => r.WarehouseId).Distinct()) balances[wh] = await SupplyCommon.BalancesAsync(db, wh, ct);
        return rows.Select(r => new IssueRequestDto(r.Id, r.Number, r.RequesterEmployeeId, names.GetValueOrDefault(r.RequesterEmployeeId),
            r.OrgUnitId, units.GetValueOrDefault(r.OrgUnitId), r.ForDepartment, r.WarehouseId, whs.GetValueOrDefault(r.WarehouseId),
            r.Justification, r.Status.ToString(), r.ApprovalRequestId, r.DecidedAt, r.SuspendedAt, r.DecisionNote, r.CreatedAt,
            r.Lines.OrderBy(l => l.Id).Select(l =>
            {
                var item = items.GetValueOrDefault(l.ItemId);
                return new IssueRequestLineDto(l.Id, l.ItemId, item?.Name ?? string.Empty, item?.Kind.ToString() ?? string.Empty,
                    item?.Unit ?? string.Empty, l.Quantity, l.IssuedQuantity, balances[r.WarehouseId].GetValueOrDefault(l.ItemId));
            }).ToList(),
            docs.Where(d => d.R == r.Id).Select(d => d.Id).ToList(), HrCommon.B64(r.RowVersion))).ToList();
    }
}

/// <summary>Approved request → waiting for the keeper, who is notified. Rejected/returned → requester notified.</summary>
public sealed class IssueRequestEffectHandler(AppDbContext db, IAuditWriter audit) : IApprovalEffect
{
    public string RequestType => IssueRequestService.RequestType;

    public async Task OnApprovedAsync(ApprovalRequest request, CancellationToken ct)
    {
        var r = await LoadAsync(request, ct);
        if (r.Status != IssueRequestStatus.Submitted) throw new DomainException("الطلب ليس بانتظار الاعتماد");
        r.Status = IssueRequestStatus.Approved;
        r.DecidedAt = DateTime.UtcNow;
        await SupplyCommon.NotifyKeeperAsync(db, r.WarehouseId, "طلب صرف بانتظار التجهيز", r.Number ?? string.Empty, "IssueRequest", r.Id, ct);
        await SupplyCommon.NotifyEmployeeAsync(db, r.RequesterEmployeeId, "اعتُمد طلب الصرف", r.Number ?? string.Empty, "IssueRequest", r.Id, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Approved", "IssueRequest", r.Id, r.Number, ct: ct);
    }

    public Task OnRejectedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        DecideAsync(request, IssueRequestStatus.Rejected, reason, "رُفض طلب الصرف", ct);

    public Task OnReturnedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        DecideAsync(request, IssueRequestStatus.Returned, reason, "أُعيد طلب الصرف للتعديل", ct);

    private async Task DecideAsync(ApprovalRequest request, IssueRequestStatus status, string reason, string title, CancellationToken ct)
    {
        var r = await LoadAsync(request, ct);
        r.Status = status;
        r.DecisionNote = reason;
        r.DecidedAt = DateTime.UtcNow;
        await SupplyCommon.NotifyEmployeeAsync(db, r.RequesterEmployeeId, title, $"{r.Number}: {reason}", "IssueRequest", r.Id, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(status.ToString(), "IssueRequest", r.Id, r.Number, newValues: reason, ct: ct);
    }

    private async Task<IssueRequest> LoadAsync(ApprovalRequest request, CancellationToken ct) =>
        await db.IssueRequests.FirstOrDefaultAsync(x => x.Id == request.SourceId, ct) ?? throw new DomainException("طلب الصرف غير موجود");
}
