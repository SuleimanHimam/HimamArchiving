using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Core;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>Read side of supply: stock, assets and their history, custody, dashboard, alerts (§7.3) and reports (§7.4).</summary>
public sealed class SupplyStockService(AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit) : ISupplyStockService
{
    private static readonly IssueRequestStatus[] Waiting = [IssueRequestStatus.Approved, IssueRequestStatus.PartiallyIssued, IssueRequestStatus.Suspended];
    private static readonly WorkOrderStatus[] OpenOrders = [WorkOrderStatus.New, WorkOrderStatus.InProgress, WorkOrderStatus.AwaitingParts];

    public async Task<IReadOnlyList<ItemStockDto>> CatalogueStockAsync(CancellationToken ct = default)
    {
        var items = await db.Items.AsNoTracking().Include(i => i.Category).OrderBy(i => i.Code).ToListAsync(ct);
        var stock = await db.StockMovements.AsNoTracking().GroupBy(m => new { m.ItemId, m.WarehouseId })
            .Select(g => new { g.Key.ItemId, g.Key.WarehouseId, Q = g.Sum(m => m.Quantity) }).ToListAsync(ct);
        var byItem = stock.GroupBy(s => s.ItemId).ToDictionary(g => g.Key, g => g.Where(x => x.Q != 0).ToDictionary(x => x.WarehouseId, x => x.Q));
        return items.Select(i =>
        {
            var per = byItem.GetValueOrDefault(i.Id) ?? [];
            var total = per.Values.Sum();
            return new ItemStockDto(SupplyCatalogService.ToDto(i), per, total, i.ReorderLevel is { } r && total <= r);
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<long, decimal>> AvailableAsync(long warehouseId, CancellationToken ct = default) =>
        await SupplyCommon.BalancesAsync(db, warehouseId, ct);

    public async Task<PagedResult<AssetDto>> AssetsAsync(AssetQuery q, CancellationToken ct = default)
    {
        var query = db.Assets.AsNoTracking().AsQueryable();
        var search = CoreGuards.Clean(q.Search);
        if (search.Length > 0)
            query = query.Where(a => a.Barcode == search || a.AssetNumber.Contains(search) || (a.SerialNumber != null && a.SerialNumber.Contains(search))
                || a.Item.Name.Contains(search));
        if (Enum.TryParse<AssetStatus>(q.Status, true, out var st)) query = query.Where(a => a.Status == st);
        if (q.ItemId is { } item) query = query.Where(a => a.ItemId == item);
        if (q.WarehouseId is { } wh) query = query.Where(a => a.WarehouseId == wh);
        if (q.EmployeeId is { } emp)
            query = query.Where(a => db.Custodies.Any(c => c.AssetId == a.Id && c.ToDate == null && c.EmployeeId == emp));
        if (q.OrgUnitId is { } unit)
            query = query.Where(a => db.Custodies.Any(c => c.AssetId == a.Id && c.ToDate == null && c.OrgUnitId == unit));
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(a => a.AssetNumber).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AssetDto> { Items = await SupplyCommon.AssetDtosAsync(db, rows, ct), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<AssetDto>> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var code = CoreGuards.Clean(barcode);
        var a = await db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Barcode == code || x.AssetNumber == code, ct);
        return a is null ? Result<AssetDto>.Fail("لا يوجد أصل بهذا الباركود") : Result<AssetDto>.Ok((await SupplyCommon.AssetDtosAsync(db, [a], ct))[0]);
    }

    public async Task<Result<AssetCardDto>> AssetCardAsync(long id, CancellationToken ct = default)
    {
        var a = await db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result<AssetCardDto>.Fail("الأصل غير موجود");
        var dto = (await SupplyCommon.AssetDtosAsync(db, [a], ct))[0];

        // History: every executed document line on the piece, oldest first (§6.4 "full history").
        var events = await (from l in db.SupplyDocumentLines.AsNoTracking()
                            join d in db.SupplyDocuments.AsNoTracking() on l.DocumentId equals d.Id
                            where l.AssetId == id && d.Status == DocumentStatus.Executed
                            orderby d.ExecutedAt
                            select new { d.Id, d.Number, d.Type, d.ExecutedAt, d.Title, d.RecipientEmployeeId, d.RecipientOrgUnitId })
            .ToListAsync(ct);
        var names = await HrCommon.NamesAsync(db, events.Where(e => e.RecipientEmployeeId != null).Select(e => e.RecipientEmployeeId!.Value), ct);
        var units = await SupplyCommon.UnitNamesAsync(db, events.Where(e => e.RecipientOrgUnitId != null).Select(e => e.RecipientOrgUnitId!.Value), ct);
        var history = events.Select(e => new AssetEventDto(e.ExecutedAt ?? DateTime.MinValue, e.Type.ToString(), e.Number, e.Id,
            e.RecipientEmployeeId is { } re ? $"{e.Title} — {names.GetValueOrDefault(re)}" : e.RecipientOrgUnitId is { } ru ? $"{e.Title} — {units.GetValueOrDefault(ru)}" : e.Title))
            .ToList();
        var orders = await db.WorkOrders.AsNoTracking().Where(w => w.AssetId == id).OrderByDescending(w => w.Id).ToListAsync(ct);
        history.AddRange(orders.Select(w => new AssetEventDto(w.CreatedAt, "WO", w.Number, null, w.Description)));
        history = history.OrderBy(h => h.At).ToList();
        var cost = orders.Sum(w => w.LaborCost + w.PartsCost);
        var pct = SupplyCommon.Dec(await SupplyCommon.SettingsAsync(db, ct), SupplySettingKeys.ReplacementCostPercent);
        return Result<AssetCardDto>.Ok(new AssetCardDto(dto, history,
            orders.Select(w => new AssetWorkOrderDto(w.Id, w.Number, w.Description, w.Status.ToString(), w.LaborCost + w.PartsCost, w.CreatedAt)).ToList(),
            orders.Count, cost, ReplacementAdvisor.Suggest(cost, a.Value, pct)));
    }

    public async Task<Result<CustodySheetDto>> CustodyAsync(CustodyQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeId is null && q.OrgUnitId is null) return Result<CustodySheetDto>.Fail("اختر موظفاً أو دائرة");
        // A manager sees the custody of the units in their scope; the supply manager sees everything.
        if (!await SupplyCommon.SeesAllAsync(scope, ct))
        {
            var units = (await scope.OrgUnitIdsAsync(ct)).Concat(await scope.ManagedOrgUnitIdsAsync(ct)).ToHashSet();
            var unit = q.OrgUnitId ?? await db.Employees.Where(e => e.Id == q.EmployeeId).Select(e => (long?)e.OrgUnitId).FirstOrDefaultAsync(ct);
            if (unit is null || !units.Contains(unit.Value)) return Result<CustodySheetDto>.Fail("خارج نطاق صلاحيتك");
        }
        var custodies = db.Custodies.AsNoTracking().Where(c => c.ToDate == null);
        if (q.EmployeeId is { } emp) custodies = custodies.Where(c => c.EmployeeId == emp);
        else
        {
            var unit = q.OrgUnitId!.Value;
            var staff = db.Employees.Where(e => e.OrgUnitId == unit).Select(e => e.Id);
            custodies = custodies.Where(c => c.OrgUnitId == unit || (q.IncludeStaff && c.EmployeeId != null && staff.Contains(c.EmployeeId.Value)));
        }
        var ids = await custodies.Select(c => c.AssetId).ToListAsync(ct);
        var assets = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.AssetNumber).ToListAsync(ct);
        var dtos = await SupplyCommon.AssetDtosAsync(db, assets, ct);
        decimal consumption = 0;
        if (q.OrgUnitId is { } u)
        {
            var today = SupplyCommon.Today();
            var start = new DateOnly(today.Year, today.Month, 1);
            consumption = -await db.StockMovements.AsNoTracking().Where(m => m.OrgUnitId == u && m.MovementDate >= start && m.Quantity < 0)
                .SumAsync(m => m.Quantity, ct);
        }
        await audit.WriteAsync("Viewed", "Custody", q.EmployeeId ?? q.OrgUnitId ?? 0, q.EmployeeId is not null ? "employee" : "unit", ct: ct);
        return Result<CustodySheetDto>.Ok(new CustodySheetDto(dtos, dtos.Count, assets.Sum(a => a.Value ?? 0), consumption));
    }

    public async Task<IReadOnlyList<AssetDto>> MyCustodyAsync(CancellationToken ct = default)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        return me is null ? [] : await EmployeeCustodyAsync(me.Id, ct);
    }

    public async Task<IReadOnlyList<AssetDto>> EmployeeCustodyAsync(long employeeId, CancellationToken ct = default)
    {
        var ids = await db.Custodies.AsNoTracking().Where(c => c.ToDate == null && c.EmployeeId == employeeId).Select(c => c.AssetId).ToListAsync(ct);
        var assets = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.AssetNumber).ToListAsync(ct);
        return await SupplyCommon.AssetDtosAsync(db, assets, ct);
    }

    public async Task<SupplyDashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        var settings = await SupplyCommon.SettingsAsync(db, ct);
        var today = SupplyCommon.Today();
        var stock = await CatalogueStockAsync(ct);
        var low = stock.Where(s => s.Low && s.Item.IsActive)
            .Select(s => new LowStockDto(s.Item.Id, s.Item.Name, s.Item.Unit, s.Total, s.Item.ReorderLevel, s.Item.MinLevel)).ToList();

        var mine = await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct);
        var waiting = await db.IssueRequests.AsNoTracking().Include(r => r.Lines)
            .Where(r => Waiting.Contains(r.Status) && mine.Contains(r.WarehouseId)).OrderBy(r => r.DecidedAt).Take(10).ToListAsync(ct);
        var pendingCount = await db.IssueRequests.CountAsync(r => Waiting.Contains(r.Status) && mine.Contains(r.WarehouseId), ct);
        var orders = await db.WorkOrders.AsNoTracking().Where(w => OpenOrders.Contains(w.Status)).OrderByDescending(w => w.Id).Take(10).ToListAsync(ct);
        var openOrders = await db.WorkOrders.CountAsync(w => OpenOrders.Contains(w.Status), ct);
        var inCustody = await db.Assets.CountAsync(a => a.Status == AssetStatus.InCustody, ct);

        // Consumption per department, last six months (pieces/units issued, any consumable).
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var cons = await db.StockMovements.AsNoTracking()
            .Where(m => m.OrgUnitId != null && m.Quantity < 0 && m.MovementDate >= from && m.AssetId == null)
            .GroupBy(m => new { m.OrgUnitId, m.MovementDate.Year, m.MovementDate.Month })
            .Select(g => new { Unit = g.Key.OrgUnitId!.Value, g.Key.Year, g.Key.Month, Q = -g.Sum(m => m.Quantity) }).ToListAsync(ct);
        var unitNames = await SupplyCommon.UnitNamesAsync(db, cons.Select(c => c.Unit), ct);
        var consumption = cons.Select(c => new ConsumptionPointDto(c.Year, c.Month, c.Unit, unitNames.GetValueOrDefault(c.Unit) ?? string.Empty, c.Q))
            .OrderBy(c => c.Year).ThenBy(c => c.Month).ToList();

        return new SupplyDashboardDto(low.Count, pendingCount, openOrders, inCustody, low.Take(10).ToList(), consumption,
            await IssueRequestService.ToDtosAsync(db, waiting, ct), await MaintenanceService.ToDtosAsync(db, orders, ct),
            await AlertsAsync(settings, today, ct));
    }

    /// <summary>§7.3 alerts other than low stock (listed separately).</summary>
    private async Task<IReadOnlyList<SupplyAlertDto>> AlertsAsync(Dictionary<string, string> settings, DateOnly today, CancellationToken ct)
    {
        var alerts = new List<SupplyAlertDto>();

        // Custody left behind: the holder's service ended, or they moved to another unit since it was handed over.
        var personal = await (from c in db.Custodies.AsNoTracking()
                              join e in db.Employees.AsNoTracking() on c.EmployeeId equals e.Id
                              where c.ToDate == null && (e.Status == EmployeeStatus.Terminated || (c.HolderOrgUnitId != null && e.OrgUnitId != c.HolderOrgUnitId))
                              select new { e.Id, e.FullName, e.Status }).Distinct().Take(50).ToListAsync(ct);
        alerts.AddRange(personal.Select(p => new SupplyAlertDto("PendingCustody", p.FullName,
            p.Status == EmployeeStatus.Terminated ? "انتهت خدمته وفي عهدته أصول" : "انتقل لدائرة أخرى وفي عهدته أصول", $"custody?employee={p.Id}")));

        var overdueDays = SupplyCommon.Int(settings, SupplySettingKeys.CountOverdueDays) ?? 365;
        var countBefore = today.AddDays(-overdueDays);
        var notCounted = await db.Assets.CountAsync(a => a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Missing
            && (a.LastCountedDate == null || a.LastCountedDate < countBefore), ct);
        if (notCounted > 0) alerts.Add(new SupplyAlertDto("NotCounted", $"{notCounted}", $"أصل لم يُجرد منذ أكثر من {overdueDays} يوماً", "assets"));

        if (SupplyCommon.Int(settings, SupplySettingKeys.WarrantyAlertDays) is { } wd and > 0)
        {
            var until = today.AddDays(wd);
            var expiring = await db.Assets.AsNoTracking().Include(a => a.Item)
                .Where(a => a.WarrantyEndDate != null && a.WarrantyEndDate >= today && a.WarrantyEndDate <= until && a.Status != AssetStatus.Disposed)
                .OrderBy(a => a.WarrantyEndDate).Take(20).ToListAsync(ct);
            alerts.AddRange(expiring.Select(a => new SupplyAlertDto("Warranty", $"{a.AssetNumber} — {a.Item.Name}",
                $"ينتهي الضمان {a.WarrantyEndDate:yyyy-MM-dd}", $"assets/{a.Id}")));
        }

        if (SupplyCommon.Int(settings, SupplySettingKeys.SuspendedRequestDays) is { } sd and > 0)
        {
            var before = DateTime.UtcNow.AddDays(-sd);
            var stale = await db.IssueRequests.AsNoTracking()
                .Where(r => r.Status == IssueRequestStatus.Suspended && r.SuspendedAt < before).Take(20).ToListAsync(ct);
            alerts.AddRange(stale.Select(r => new SupplyAlertDto("SuspendedRequest", r.Number ?? string.Empty,
                $"معلّق لنقص منذ أكثر من {sd} يوماً", $"requests/{r.Id}")));
        }
        return alerts;
    }

    // ---- reports (§7.4) ----

    public async Task<IReadOnlyList<WarehouseBalanceDto>> BalancesReportAsync(long? warehouseId, CancellationToken ct = default)
    {
        var q = db.StockMovements.AsNoTracking().AsQueryable();
        if (warehouseId is { } w) q = q.Where(m => m.WarehouseId == w);
        var rows = await q.GroupBy(m => new { m.ItemId, m.WarehouseId }).Select(g => new { g.Key.ItemId, g.Key.WarehouseId, Q = g.Sum(m => m.Quantity) })
            .Where(x => x.Q != 0).ToListAsync(ct);
        var items = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        return rows.Select(r => new WarehouseBalanceDto(r.ItemId, items[r.ItemId].Code, items[r.ItemId].Name, items[r.ItemId].Unit,
                items[r.ItemId].Kind.ToString(), r.WarehouseId, whs.GetValueOrDefault(r.WarehouseId) ?? string.Empty, r.Q))
            .OrderBy(r => r.WarehouseName).ThenBy(r => r.ItemCode).ToList();
    }

    public async Task<Result<ItemCardDto>> ItemCardAsync(long itemId, long? warehouseId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from || to.DayNumber - from.DayNumber > 3660) return Result<ItemCardDto>.Fail("الفترة غير صالحة");
        var item = await db.Items.AsNoTracking().Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result<ItemCardDto>.Fail("الصنف غير موجود");
        var q = db.StockMovements.AsNoTracking().Where(m => m.ItemId == itemId);
        if (warehouseId is { } w) q = q.Where(m => m.WarehouseId == w);
        var opening = await q.Where(m => m.MovementDate < from).SumAsync(m => m.Quantity, ct);
        var period = q.Where(m => m.MovementDate >= from && m.MovementDate <= to);
        var moves = await (from m in period
                           join d in db.SupplyDocuments.AsNoTracking() on m.DocumentId equals d.Id
                           orderby m.MovementDate, m.Id
                           select new { m.MovementDate, d.Number, d.Id, d.Type, m.WarehouseId, m.Quantity }).ToListAsync(ct);
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        var running = opening;
        var lines = moves.Select(m =>
        {
            running += m.Quantity;
            return new ItemCardLineDto(m.MovementDate, m.Number, m.Id, m.Type.ToString(), m.WarehouseId, whs.GetValueOrDefault(m.WarehouseId) ?? string.Empty,
                m.Quantity > 0 ? m.Quantity : 0, m.Quantity < 0 ? -m.Quantity : 0, running);
        }).ToList();
        return Result<ItemCardDto>.Ok(new ItemCardDto(SupplyCatalogService.ToDto(item), opening, lines, running));
    }

    public async Task<IReadOnlyList<ConsumptionRowDto>> ConsumptionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) return [];
        // Compared with the period of the same length just before.
        var span = to.DayNumber - from.DayNumber + 1;
        var prevFrom = from.AddDays(-span);
        var rows = await db.StockMovements.AsNoTracking()
            .Where(m => m.OrgUnitId != null && m.Quantity < 0 && m.AssetId == null && m.MovementDate >= prevFrom && m.MovementDate <= to)
            .Select(m => new { Unit = m.OrgUnitId!.Value, m.ItemId, m.MovementDate, m.Quantity }).ToListAsync(ct);
        var items = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var units = await SupplyCommon.UnitNamesAsync(db, rows.Select(r => r.Unit), ct);
        return rows.GroupBy(r => (r.Unit, r.ItemId)).Select(g => new ConsumptionRowDto(g.Key.Unit, units.GetValueOrDefault(g.Key.Unit) ?? string.Empty,
                g.Key.ItemId, items[g.Key.ItemId].Name, items[g.Key.ItemId].Unit,
                -g.Where(x => x.MovementDate >= from).Sum(x => x.Quantity), -g.Where(x => x.MovementDate < from).Sum(x => x.Quantity)))
            .Where(r => r.Quantity > 0 || r.PreviousQuantity > 0)
            .OrderBy(r => r.OrgUnitName).ThenByDescending(r => r.Quantity).ToList();
    }

    public async Task<IReadOnlyList<MaintenanceCostRowDto>> MaintenanceReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await db.WorkOrders.AsNoTracking().Where(w => w.CreatedAt >= start && w.CreatedAt < end)
            .GroupBy(w => w.AssetId).Select(g => new { g.Key, N = g.Count(), C = g.Sum(w => w.LaborCost + w.PartsCost) }).ToListAsync(ct);
        var ids = rows.Where(r => r.Key != null).Select(r => r.Key!.Value).ToList();
        var assets = await db.Assets.AsNoTracking().Include(a => a.Item).Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return rows.Select(r =>
        {
            var a = r.Key is { } k ? assets.GetValueOrDefault(k) : null;
            return new MaintenanceCostRowDto(r.Key, a?.AssetNumber, a?.Item.Name, r.N, r.C);
        }).OrderByDescending(r => r.Cost).ToList();
    }

    public async Task<IReadOnlyList<StagnantItemDto>> StagnantReportAsync(CancellationToken ct = default)
    {
        var days = SupplyCommon.Int(await SupplyCommon.SettingsAsync(db, ct), SupplySettingKeys.StagnantDays) ?? 180;
        var cutoff = SupplyCommon.Today().AddDays(-days);
        var rows = await db.StockMovements.AsNoTracking().GroupBy(m => m.ItemId)
            .Select(g => new { g.Key, Q = g.Sum(m => m.Quantity), Last = g.Max(m => m.MovementDate) }).ToListAsync(ct);
        var items = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        return rows.Where(r => r.Q > 0 && r.Last < cutoff)
            .Select(r => new StagnantItemDto(r.Key, items[r.Key].Name, items[r.Key].Unit, r.Q, r.Last)).OrderBy(r => r.LastMovement).ToList();
    }
}
