using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Common;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>
/// الصيانة الداخلية (§7.2): any employee reports a fault on an asset (barcode) or a place; maintenance assesses,
/// issues supplies from the maintenance store on the work order, and closes it with time and cost — or
/// recommends disposal, which then goes by a SUP-DSP document.
/// </summary>
public sealed class MaintenanceService(AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit, INumberingService numbering)
    : IMaintenanceService
{
    private const string NotFound = "أمر العمل غير موجود";
    private static readonly WorkOrderStatus[] Open = [WorkOrderStatus.New, WorkOrderStatus.InProgress, WorkOrderStatus.AwaitingParts];

    public async Task<PagedResult<WorkOrderDto>> ListAsync(WorkOrderQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id ?? 0;
        query = q.Folder switch
        {
            "mine" => query.Where(w => w.ReporterEmployeeId == me),
            "closed" => query.Where(w => !Open.Contains(w.Status)),
            _ => query.Where(w => Open.Contains(w.Status)),
        };
        var search = CoreGuards.Clean(q.Search);
        if (search.Length > 0) query = query.Where(w => w.Number.Contains(search) || w.Description.Contains(search));
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(w => w.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<WorkOrderDto> { Items = await ToDtosAsync(db, rows, ct), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<WorkOrderCountsDto> CountsAsync(CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id ?? 0;
        return new WorkOrderCountsDto(await query.CountAsync(w => Open.Contains(w.Status), ct),
            await query.CountAsync(w => w.ReporterEmployeeId == me, ct), await query.CountAsync(w => !Open.Contains(w.Status), ct));
    }

    public async Task<Result<WorkOrderDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var w = await (await ScopedAsync(ct)).FirstOrDefaultAsync(x => x.Id == id, ct);
        return w is null ? Result<WorkOrderDto>.Fail(NotFound) : Result<WorkOrderDto>.Ok((await ToDtosAsync(db, [w], ct))[0]);
    }

    public async Task<Result<WorkOrderDto>> ReportAsync(ReportFaultRequest r, CancellationToken ct = default)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me is null) return Result<WorkOrderDto>.Fail("حسابك غير مرتبط بسجل موظف");
        var description = CoreGuards.Clean(r.Description);
        if (description.Length is 0 or > 1000) return Result<WorkOrderDto>.Fail("وصف العطل مطلوب");
        var location = CoreGuards.CleanOrNull(r.Location);
        if (location?.Length > 200) return Result<WorkOrderDto>.Fail("المكان أطول من 200 حرف");
        Asset? asset = null;
        if (CoreGuards.CleanOrNull(r.Barcode) is { } code)
        {
            asset = await db.Assets.FirstOrDefaultAsync(a => a.Barcode == code || a.AssetNumber == code, ct);
            if (asset is null) return Result<WorkOrderDto>.Fail("لا يوجد أصل بهذا الباركود");
            if (asset.Status is AssetStatus.Disposed or AssetStatus.Missing) return Result<WorkOrderDto>.Fail("الأصل مشطوب أو مفقود");
            if (await db.WorkOrders.AnyAsync(w => w.AssetId == asset.Id && Open.Contains(w.Status), ct))
                return Result<WorkOrderDto>.Fail("للأصل أمر عمل مفتوح");
        }
        else if (location is null) return Result<WorkOrderDto>.Fail("امسح باركود الأصل أو اكتب المكان");
        if (r.SiteId is { } sid && !await db.Sites.AnyAsync(s => s.Id == sid, ct)) return Result<WorkOrderDto>.Fail("المعبر غير موجود");

        var w = new WorkOrder
        {
            Number = await SupplyCommon.NumberAsync(numbering, SupplyCommon.WorkOrderNumbering, "أوامر العمل", ct),
            AssetId = asset?.Id, Location = location, SiteId = r.SiteId ?? me.SiteId, Description = description, ReporterEmployeeId = me.Id,
        };
        if (asset is not null)
        {
            asset.StatusBeforeMaintenance = asset.Status;
            asset.Status = AssetStatus.Maintenance;
        }
        db.WorkOrders.Add(w);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Reported", "WorkOrder", w.Id, w.Number, newValues: description, ct: ct);
        return Result<WorkOrderDto>.Ok((await ToDtosAsync(db, [w], ct))[0]);
    }

    public async Task<Result<WorkOrderDto>> UpdateAsync(long id, UpdateWorkOrderRequest r, CancellationToken ct = default)
    {
        var w = await db.WorkOrders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return Result<WorkOrderDto>.Fail(NotFound);
        if (!Open.Contains(w.Status)) return Result<WorkOrderDto>.Fail("أمر العمل مغلق");
        if (!CoreGuards.IsCurrent(w.RowVersion, r.RowVersion)) return Result<WorkOrderDto>.Fail(CoreGuards.Stale);
        if (!Enum.TryParse<WorkOrderStatus>(r.Status, true, out var status) || !Open.Contains(status))
            return Result<WorkOrderDto>.Fail("الحالة غير صالحة. للإغلاق استخدم «إغلاق الأمر»");
        if (r.AssigneeEmployeeId is { } a && !await db.Employees.AnyAsync(e => e.Id == a, ct)) return Result<WorkOrderDto>.Fail("الفني غير موجود");
        var assessment = CoreGuards.CleanOrNull(r.Assessment);
        if (assessment?.Length > 1000) return Result<WorkOrderDto>.Fail("التقييم طويل");
        w.AssigneeEmployeeId = r.AssigneeEmployeeId;
        w.Status = status;
        w.Assessment = assessment;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<WorkOrderDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "WorkOrder", w.Id, w.Number, newValues: status.ToString(), ct: ct);
        return Result<WorkOrderDto>.Ok((await ToDtosAsync(db, [w], ct))[0]);
    }

    public async Task<Result<WorkOrderDto>> IssuePartsAsync(long id, WorkOrderPartsRequest r, CancellationToken ct = default)
    {
        var w = await db.WorkOrders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return Result<WorkOrderDto>.Fail(NotFound);
        if (!Open.Contains(w.Status)) return Result<WorkOrderDto>.Fail("أمر العمل مغلق");
        if (!CoreGuards.IsCurrent(w.RowVersion, r.RowVersion)) return Result<WorkOrderDto>.Fail(CoreGuards.Stale);
        var wh = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.WarehouseId && x.IsActive, ct);
        if (wh is null) return Result<WorkOrderDto>.Fail("المستودع غير موجود");
        if (wh.Kind != WarehouseKind.Maintenance) return Result<WorkOrderDto>.Fail("المستلزمات تُصرف من مستودع الصيانة");
        if (!(await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct)).Contains(wh.Id) && !await scope.HasPermissionAsync("SupplyMaintenance.Edit", ct))
            return Result<WorkOrderDto>.Fail("المستودع خارج نطاق صلاحيتك");
        if (r.Lines.Count is 0 or > 50) return Result<WorkOrderDto>.Fail("أضف صنفاً واحداً على الأقل");
        var ids = r.Lines.Select(l => l.ItemId).ToList();
        var items = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var doc = new SupplyDocument
        {
            Type = DocumentType.ISS, DocDate = SupplyCommon.Today(), WarehouseId = wh.Id, WorkOrderId = w.Id,
            Title = $"مستلزمات صيانة على أمر العمل {w.Number}", RecipientEmployeeId = w.AssigneeEmployeeId,
            RecipientOrgUnitId = w.AssigneeEmployeeId is null ? await UnitOfReporterAsync(w, ct) : null,
        };
        foreach (var l in r.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var item) || item.Kind == ItemKind.FixedAsset)
                return Result<WorkOrderDto>.Fail("أمر العمل يُصرف عليه مستلزمات ومستهلكات فقط");
            if (l.Quantity is <= 0 or > 100000) return Result<WorkOrderDto>.Fail($"{item.Name}: الكمية غير صالحة");
            doc.Lines.Add(new SupplyDocumentLine { ItemId = item.Id, Quantity = l.Quantity });
        }
        if (doc.RecipientEmployeeId is null && doc.RecipientOrgUnitId is null) return Result<WorkOrderDto>.Fail("عيّن الفني أولاً");

        try
        {
            await db.InTransactionAsync(async () =>
            {
                db.SupplyDocuments.Add(doc);
                doc.Number = await SupplyCommon.NumberAsync(numbering, SupplyCommon.DocumentNumbering(DocumentType.ISS), "المحاضر", ct);
                doc.Status = DocumentStatus.Submitted;   // authorised by the open work order
                await db.SaveChangesAsync(ct);
                await new SupplyDocumentEngine(db, numbering).ExecuteAsync(doc, ct);
                w.PartsCost = await PartsCostAsync(w.Id, ct);
                if (w.Status == WorkOrderStatus.AwaitingParts) w.Status = WorkOrderStatus.InProgress;
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync("PartsIssued", "WorkOrder", w.Id, w.Number, newValues: doc.Number, ct: ct);
                return true;
            }, _ => true, ct);
        }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Result<WorkOrderDto>.Fail(ex.Message); }
        return await GetAsync(w.Id, ct);
    }

    public async Task<Result<WorkOrderDto>> CloseAsync(long id, CloseWorkOrderRequest r, CancellationToken ct = default)
    {
        var w = await db.WorkOrders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return Result<WorkOrderDto>.Fail(NotFound);
        if (!Open.Contains(w.Status)) return Result<WorkOrderDto>.Fail("أمر العمل مغلق");
        if (!CoreGuards.IsCurrent(w.RowVersion, r.RowVersion)) return Result<WorkOrderDto>.Fail(CoreGuards.Stale);
        var outcome = CoreGuards.Clean(r.Outcome);
        if (outcome.Length is 0 or > 1000) return Result<WorkOrderDto>.Fail("نتيجة العمل مطلوبة");
        if (r.LaborCost < 0 || r.LaborHours is < 0 or > 10000) return Result<WorkOrderDto>.Fail("الوقت أو الكلفة غير صالحين");
        w.Status = r.Repaired ? WorkOrderStatus.Closed : WorkOrderStatus.RecommendedDisposal;
        w.LaborHours = r.LaborHours;
        w.LaborCost = decimal.Round(r.LaborCost, 2, MidpointRounding.AwayFromZero);
        w.PartsCost = await PartsCostAsync(w.Id, ct);
        w.Outcome = outcome;
        w.ClosedAt = DateTime.UtcNow;
        if (w.AssetId is { } aid)
        {
            var asset = await db.Assets.FirstAsync(a => a.Id == aid, ct);
            if (r.Repaired)
            {
                asset.Status = asset.StatusBeforeMaintenance ?? AssetStatus.InStock;
                asset.Condition = AssetCondition.Good;
            }
            else
            {
                // Not repairable: it waits for the disposal committee in its current place (custody or store).
                asset.Condition = AssetCondition.Damaged;
                asset.Status = asset.StatusBeforeMaintenance == AssetStatus.InCustody ? AssetStatus.InCustody : AssetStatus.PendingDisposal;
            }
            asset.StatusBeforeMaintenance = null;
        }
        await SupplyCommon.NotifyEmployeeAsync(db, w.ReporterEmployeeId, r.Repaired ? "أُغلق بلاغ العطل" : "توصية بالإتلاف", $"{w.Number}: {outcome}", "WorkOrder", w.Id, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<WorkOrderDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Closed", "WorkOrder", w.Id, w.Number, newValues: $"{w.Status}; cost={w.LaborCost + w.PartsCost}", ct: ct);
        return Result<WorkOrderDto>.Ok((await ToDtosAsync(db, [w], ct))[0]);
    }

    // ---- helpers ----

    /// <summary>Parts cost: issued quantities at the latest receipt value of each item (no costing method fixed yet).</summary>
    private async Task<decimal> PartsCostAsync(long workOrderId, CancellationToken ct)
    {
        var used = await db.StockMovements.AsNoTracking().Where(m => m.WorkOrderId == workOrderId)
            .GroupBy(m => m.ItemId).Select(g => new { g.Key, Q = -g.Sum(m => m.Quantity) }).ToListAsync(ct);
        decimal total = 0;
        foreach (var u in used)
        {
            var price = await (from l in db.SupplyDocumentLines.AsNoTracking()
                               join d in db.SupplyDocuments.AsNoTracking() on l.DocumentId equals d.Id
                               where l.ItemId == u.Key && l.UnitValue != null && d.Status == DocumentStatus.Executed
                                   && (d.Type == DocumentType.RCV || d.Type == DocumentType.OPN)
                               orderby d.ExecutedAt descending
                               select l.UnitValue).FirstOrDefaultAsync(ct);
            total += u.Q * (price ?? 0);
        }
        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<long?> UnitOfReporterAsync(WorkOrder w, CancellationToken ct) =>
        w.ReporterEmployeeId is { } r ? await db.Employees.Where(e => e.Id == r).Select(e => (long?)e.OrgUnitId).FirstOrDefaultAsync(ct) : null;

    /// <summary>Maintenance staff (SupplyMaintenance.Edit) see all orders; everyone else their own reports.</summary>
    private async Task<IQueryable<WorkOrder>> ScopedAsync(CancellationToken ct)
    {
        var q = db.WorkOrders.AsNoTracking();
        if (await SupplyCommon.SeesAllAsync(scope, ct) || await scope.HasPermissionAsync("SupplyMaintenance.Edit", ct)) return q;
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id ?? 0;
        return q.Where(w => w.ReporterEmployeeId == me || w.AssigneeEmployeeId == me);
    }

    internal static async Task<IReadOnlyList<WorkOrderDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<WorkOrder> rows, CancellationToken ct)
    {
        var names = await HrCommon.NamesAsync(db, rows.Where(w => w.ReporterEmployeeId != null).Select(w => w.ReporterEmployeeId!.Value)
            .Concat(rows.Where(w => w.AssigneeEmployeeId != null).Select(w => w.AssigneeEmployeeId!.Value)), ct);
        var assetIds = rows.Where(w => w.AssetId != null).Select(w => w.AssetId!.Value).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking().Include(a => a.Item).Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var costs = await db.WorkOrders.AsNoTracking().Where(w => w.AssetId != null && assetIds.Contains(w.AssetId.Value))
            .GroupBy(w => w.AssetId!.Value).Select(g => new { g.Key, C = g.Sum(w => w.LaborCost + w.PartsCost) })
            .ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var pct = SupplyCommon.Dec(await SupplyCommon.SettingsAsync(db, ct), SupplySettingKeys.ReplacementCostPercent);
        var ids = rows.Select(w => w.Id).ToList();
        var docs = await db.SupplyDocuments.AsNoTracking().Where(d => d.WorkOrderId != null && ids.Contains(d.WorkOrderId.Value))
            .Select(d => new { d.Id, W = d.WorkOrderId!.Value }).ToListAsync(ct);
        return rows.Select(w =>
        {
            var asset = w.AssetId is { } a ? assets.GetValueOrDefault(a) : null;
            return new WorkOrderDto(w.Id, w.Number, w.AssetId, asset?.AssetNumber, asset?.Item?.Name, w.Location, w.SiteId, w.Description,
                w.ReporterEmployeeId ?? 0, w.ReporterEmployeeId is { } r ? names.GetValueOrDefault(r) : null, w.AssigneeEmployeeId,
                w.AssigneeEmployeeId is { } s ? names.GetValueOrDefault(s) : null, w.Status.ToString(), w.Assessment, w.LaborHours,
                w.LaborCost, w.PartsCost, w.CreatedAt, w.ClosedAt, w.Outcome,
                asset is not null && ReplacementAdvisor.Suggest(costs.GetValueOrDefault(asset.Id), asset.Value, pct),
                docs.Where(d => d.W == w.Id).Select(d => d.Id).ToList(), HrCommon.B64(w.RowVersion));
        }).ToList();
    }
}
