using System.Globalization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>Shared reads for the supply services: scope, settings, balances, names.</summary>
internal static class SupplyCommon
{
    /// <summary>Numbering template keys (seeded in DbSeeder; editable from Settings → Numbering).</summary>
    public static string DocumentNumbering(DocumentType t) => $"SupplyDocument.{t}";
    public const string IssueRequestNumbering = "SupplyIssueRequest";
    public const string CountNumbering = "SupplyCount";
    public const string WorkOrderNumbering = "SupplyWorkOrder";
    public const string AssetNumbering = "SupplyAsset";
    public const string BarcodeNumbering = "SupplyBarcode";

    /// <summary>Holds every warehouse: system administrator or SupplyAdmin.View (مدير اللوازم).</summary>
    public static async Task<bool> SeesAllAsync(IAccessScope scope, CancellationToken ct) =>
        await scope.IsSystemAdminAsync(ct) || await scope.HasPermissionAsync("SupplyAdmin.View", ct);

    /// <summary>Warehouses the caller acts on: all for the supply manager, else those they keep (§5.2).</summary>
    public static async Task<HashSet<long>> WarehouseScopeAsync(AppDbContext db, IAccessScope scope, ICurrentUser user, CancellationToken ct)
    {
        if (await SeesAllAsync(scope, ct)) return (await db.Warehouses.AsNoTracking().Select(w => w.Id).ToListAsync(ct)).ToHashSet();
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me is null) return [];
        return (await db.Warehouses.AsNoTracking().Where(w => w.KeeperEmployeeId == me.Id).Select(w => w.Id).ToListAsync(ct)).ToHashSet();
    }

    public static async Task<Dictionary<string, string>> SettingsAsync(AppDbContext db, CancellationToken ct) =>
        await db.SupplySettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public static decimal? Dec(Dictionary<string, string> s, string key) =>
        decimal.TryParse(s.GetValueOrDefault(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    public static int? Int(Dictionary<string, string> s, string key) =>
        int.TryParse(s.GetValueOrDefault(key), out var v) ? v : null;

    public static long? Long(Dictionary<string, string> s, string key) =>
        long.TryParse(s.GetValueOrDefault(key), out var v) ? v : null;

    /// <summary>Stock on hand per item in one warehouse = sum of movements.</summary>
    public static async Task<Dictionary<long, decimal>> BalancesAsync(AppDbContext db, long warehouseId, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking().Where(m => m.WarehouseId == warehouseId)
            .GroupBy(m => m.ItemId).Select(g => new { g.Key, Qty = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    public static async Task<decimal> BalanceAsync(AppDbContext db, long itemId, long warehouseId, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking().Where(m => m.ItemId == itemId && m.WarehouseId == warehouseId).SumAsync(m => m.Quantity, ct);

    public static async Task<Dictionary<long, string>> WarehouseNamesAsync(AppDbContext db, CancellationToken ct) =>
        await db.Warehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Name, ct);

    public static async Task<Dictionary<long, string>> UnitNamesAsync(AppDbContext db, IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.OrgUnits.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    public static async Task<string> NumberAsync(INumberingService numbering, string key, string what, CancellationToken ct) =>
        await numbering.GenerateNumberAsync(key, ct: ct) ?? throw new DomainException($"قالب ترقيم {what} غير معرّف ({key})");

    /// <summary>Notifies the keeper of a warehouse, when the keeper has a login.</summary>
    public static async Task NotifyKeeperAsync(AppDbContext db, long warehouseId, string title, string body, string entityType, long entityId, CancellationToken ct)
    {
        var uid = await (from w in db.Warehouses join e in db.Employees on w.KeeperEmployeeId equals e.Id
                         where w.Id == warehouseId select e.UserId).FirstOrDefaultAsync(ct);
        if (uid is { } u) HrCommon.Notify(db, u, title, body, entityType, entityId);
    }

    public static async Task NotifyEmployeeAsync(AppDbContext db, long? employeeId, string title, string body, string entityType, long entityId, CancellationToken ct)
    {
        if (employeeId is null) return;
        var uid = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (uid is { } u) HrCommon.Notify(db, u, title, body, entityType, entityId);
    }

    public static AssetDto ToAssetDto(Asset a, string itemName, IReadOnlyDictionary<long, string> warehouses, Custody? c,
        IReadOnlyDictionary<long, string> employees, IReadOnlyDictionary<long, string> units) =>
        new(a.Id, a.AssetNumber, a.Barcode, a.ItemId, itemName, a.SerialNumber, a.Status.ToString(), a.Condition.ToString(),
            a.WarehouseId, a.WarehouseId is { } w ? warehouses.GetValueOrDefault(w) : null,
            c?.HolderType.ToString(), c?.EmployeeId, c?.OrgUnitId,
            c is null ? null : c.EmployeeId is { } e ? employees.GetValueOrDefault(e) : c.OrgUnitId is { } u ? units.GetValueOrDefault(u) : null,
            a.PurchaseDate, a.Value, a.WarrantyEndDate, a.LastCountedDate, HrCommon.B64(a.RowVersion));

    /// <summary>Asset rows with their open custody and display names, in one go.</summary>
    public static async Task<IReadOnlyList<AssetDto>> AssetDtosAsync(AppDbContext db, IReadOnlyList<Asset> rows, CancellationToken ct)
    {
        var ids = rows.Select(a => a.Id).ToList();
        var custody = await db.Custodies.AsNoTracking().Where(c => ids.Contains(c.AssetId) && c.ToDate == null)
            .ToListAsync(ct);
        var byAsset = custody.GroupBy(c => c.AssetId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
        var employees = await HrCommon.NamesAsync(db, custody.Where(c => c.EmployeeId != null).Select(c => c.EmployeeId!.Value), ct);
        var units = await UnitNamesAsync(db, custody.Where(c => c.OrgUnitId != null).Select(c => c.OrgUnitId!.Value), ct);
        var whs = await WarehouseNamesAsync(db, ct);
        var itemIds = rows.Select(a => a.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        return rows.Select(a => ToAssetDto(a, items.GetValueOrDefault(a.ItemId) ?? string.Empty, whs, byAsset.GetValueOrDefault(a.Id), employees, units)).ToList();
    }

    public static DateOnly Today() => HrCommon.TodayLocal();
}

/// <summary>
/// Applies an approved document to stock, assets and custody (§6.1). The single place where stock moves.
/// Runs inside the caller's transaction; any rule broken throws <see cref="DomainException"/> and the
/// whole approval / fulfilment rolls back.
/// </summary>
internal sealed class SupplyDocumentEngine(AppDbContext db, INumberingService numbering)
{
    /// <summary>Validates and executes. MOV must already be confirmed by the receiving warehouse.</summary>
    public async Task ExecuteAsync(SupplyDocument doc, CancellationToken ct)
    {
        if (doc.Status is DocumentStatus.Executed or DocumentStatus.Cancelled or DocumentStatus.Rejected)
            throw new DomainException("المحضر منفذ أو مغلق");
        if (doc.Lines.Count == 0) throw new DomainException("المحضر بلا أسطر");
        await EnsureNotFrozenAsync(doc, ct);

        var itemIds = doc.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var assetIds = doc.Lines.Where(l => l.AssetId != null).Select(l => l.AssetId!.Value).ToList();
        var assets = await db.Assets.Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var today = SupplyCommon.Today();
        var touchedWarehouses = new HashSet<long>();

        foreach (var line in doc.Lines)
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new DomainException("صنف غير موجود في المحضر");
            var isAsset = item.Kind == ItemKind.FixedAsset;
            Asset? asset = line.AssetId is { } aid ? assets.GetValueOrDefault(aid) ?? throw new DomainException("أصل غير موجود في المحضر") : null;
            if (asset is not null && asset.ItemId != item.Id) throw new DomainException($"الأصل {asset.AssetNumber} لا يتبع الصنف {item.Name}");

            switch (doc.Type)
            {
                case DocumentType.RCV:
                case DocumentType.OPN:
                {
                    var wh = doc.Type == DocumentType.RCV ? doc.WarehouseId : line.WarehouseId ?? doc.WarehouseId;
                    if (isAsset)
                    {
                        var created = await CreateAssetAsync(item, line, doc, wh, ct);
                        line.AssetId = created.Id;
                        if (created.Status == AssetStatus.InCustody)
                            OpenCustody(created, line.HolderEmployeeId, line.HolderOrgUnitId, doc, today,
                                line.HolderEmployeeId is { } he ? await UnitOfAsync(he, ct) : null);
                        else Move(item.Id, wh!.Value, 1, doc, today, created.Id);
                    }
                    else
                    {
                        if (wh is null) throw new DomainException($"حدّد المستودع للصنف {item.Name}");
                        Move(item.Id, wh.Value, line.Quantity, doc, today);
                    }
                    if (wh is { } w) touchedWarehouses.Add(w);
                    break;
                }
                case DocumentType.ISS:
                {
                    var wh = doc.WarehouseId!.Value;
                    touchedWarehouses.Add(wh);
                    if (isAsset)
                    {
                        if (asset is null) throw new DomainException($"اختر القطعة (الباركود) للصنف {item.Name}");
                        if (asset.Status != AssetStatus.InStock || asset.WarehouseId != wh)
                            throw new DomainException($"الأصل {asset.AssetNumber} ليس في هذا المستودع");
                        asset.Status = AssetStatus.InCustody;
                        asset.WarehouseId = null;
                        var empUnit = doc.RecipientEmployeeId is { } re ? await UnitOfAsync(re, ct) : null;
                        OpenCustody(asset, doc.RecipientEmployeeId, doc.RecipientEmployeeId is null ? doc.RecipientOrgUnitId : null, doc, today, empUnit);
                        Move(item.Id, wh, -1, doc, today, asset.Id);
                    }
                    else
                    {
                        await RequireAvailableAsync(item, wh, line.Quantity, ct);
                        var unit = doc.RecipientOrgUnitId ?? (doc.RecipientEmployeeId is { } re ? await UnitOfAsync(re, ct) : null);
                        Move(item.Id, wh, -line.Quantity, doc, today, null, unit);
                    }
                    break;
                }
                case DocumentType.TRF:
                {
                    if (asset is null) throw new DomainException("نقل العهدة لأصل محدد بالباركود");
                    if (asset.Status != AssetStatus.InCustody) throw new DomainException($"الأصل {asset.AssetNumber} ليس في عهدة");
                    await CloseCustodyAsync(asset.Id, doc, today, ct);
                    var empUnit = doc.RecipientEmployeeId is { } re ? await UnitOfAsync(re, ct) : null;
                    OpenCustody(asset, doc.RecipientEmployeeId, doc.RecipientEmployeeId is null ? doc.RecipientOrgUnitId : null, doc, today, empUnit);
                    break;
                }
                case DocumentType.MOV:
                {
                    var from = doc.WarehouseId!.Value;
                    var to = doc.TargetWarehouseId!.Value;
                    touchedWarehouses.Add(from);
                    touchedWarehouses.Add(to);
                    if (isAsset)
                    {
                        if (asset is null) throw new DomainException($"اختر القطعة (الباركود) للصنف {item.Name}");
                        if (asset.WarehouseId != from || asset.Status is AssetStatus.InCustody or AssetStatus.Disposed)
                            throw new DomainException($"الأصل {asset.AssetNumber} ليس في المستودع المرسل");
                        asset.WarehouseId = to;
                        Move(item.Id, from, -1, doc, today, asset.Id);
                        Move(item.Id, to, 1, doc, today, asset.Id);
                    }
                    else
                    {
                        await RequireAvailableAsync(item, from, line.Quantity, ct);
                        Move(item.Id, from, -line.Quantity, doc, today);
                        Move(item.Id, to, line.Quantity, doc, today);
                    }
                    break;
                }
                case DocumentType.RET:
                {
                    var wh = doc.WarehouseId!.Value;
                    touchedWarehouses.Add(wh);
                    if (isAsset)
                    {
                        if (asset is null) throw new DomainException("الإرجاع لأصل محدد بالباركود");
                        if (asset.Status != AssetStatus.InCustody) throw new DomainException($"الأصل {asset.AssetNumber} ليس في عهدة");
                        await CloseCustodyAsync(asset.Id, doc, today, ct);
                        var condition = line.Condition ?? AssetCondition.Good;
                        asset.Condition = condition;
                        asset.WarehouseId = wh;
                        asset.Status = condition switch
                        {
                            AssetCondition.Damaged => AssetStatus.PendingDisposal,       // → لجنة الإتلاف
                            AssetCondition.NeedsMaintenance => AssetStatus.Maintenance,  // → أمر صيانة
                            _ => AssetStatus.InStock,
                        };
                        Move(item.Id, wh, 1, doc, today, asset.Id);
                        if (condition == AssetCondition.NeedsMaintenance)
                        {
                            asset.StatusBeforeMaintenance = AssetStatus.InStock;
                            db.WorkOrders.Add(new WorkOrder
                            {
                                Number = await SupplyCommon.NumberAsync(numbering, SupplyCommon.WorkOrderNumbering, "أوامر العمل", ct),
                                AssetId = asset.Id, Description = $"مرتجع يحتاج صيانة — {doc.Number}",
                            });
                        }
                    }
                    else Move(item.Id, wh, line.Quantity, doc, today);
                    break;
                }
                case DocumentType.DSP:
                {
                    if (isAsset)
                    {
                        if (asset is null) throw new DomainException("الإتلاف لأصل محدد بالباركود");
                        if (asset.Status == AssetStatus.Disposed) throw new DomainException($"الأصل {asset.AssetNumber} مشطوب مسبقاً");
                        if (asset.Status == AssetStatus.InCustody) await CloseCustodyAsync(asset.Id, doc, today, ct);
                        if (asset.WarehouseId is { } w)
                        {
                            Move(item.Id, w, -1, doc, today, asset.Id);
                            touchedWarehouses.Add(w);
                        }
                        asset.Status = AssetStatus.Disposed;   // the record stays forever (§6.5)
                        asset.WarehouseId = null;
                        asset.Condition = AssetCondition.Damaged;
                    }
                    else
                    {
                        var wh = doc.WarehouseId ?? throw new DomainException("حدّد المستودع للصنف المُتلف");
                        await RequireAvailableAsync(item, wh, line.Quantity, ct);
                        Move(item.Id, wh, -line.Quantity, doc, today);
                        touchedWarehouses.Add(wh);
                    }
                    break;
                }
                case DocumentType.ADJ:
                {
                    if (asset is not null)
                    {
                        if (line.Quantity < 0)
                        {
                            // Missing after an approved count: out of stock / custody, kept as Missing until a write-off decision.
                            if (asset.Status == AssetStatus.InCustody) await CloseCustodyAsync(asset.Id, doc, today, ct);
                            if (asset.WarehouseId is { } w) { Move(item.Id, w, -1, doc, today, asset.Id); touchedWarehouses.Add(w); }
                            asset.WarehouseId = null;
                            asset.Status = AssetStatus.Missing;
                        }
                        else if (line.Condition is { } c) asset.Condition = c;
                    }
                    else
                    {
                        var wh = line.WarehouseId ?? throw new DomainException("سطر تسوية بلا مستودع");
                        if (line.Quantity < 0) await RequireAvailableAsync(item, wh, -line.Quantity, ct);
                        Move(item.Id, wh, line.Quantity, doc, today);
                        touchedWarehouses.Add(wh);
                    }
                    break;
                }
            }
        }

        doc.Status = DocumentStatus.Executed;
        doc.ExecutedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AlertLowStockAsync(itemIds, touchedWarehouses, doc, ct);
    }

    /// <summary>§7.1: a count with "freeze" blocks issue and transfer on its scope while counting.</summary>
    public async Task EnsureNotFrozenAsync(SupplyDocument doc, CancellationToken ct)
    {
        if (doc.Type is DocumentType.ADJ or DocumentType.OPN or DocumentType.RCV) return;
        var frozen = await db.CountCycles.AsNoTracking()
            .Where(c => c.Status == CountStatus.Counting && c.FreezeMovements).ToListAsync(ct);
        if (frozen.Count == 0) return;

        var warehouses = new HashSet<long>();
        if (doc.WarehouseId is { } w1) warehouses.Add(w1);
        if (doc.TargetWarehouseId is { } w2) warehouses.Add(w2);
        var units = new HashSet<long>();
        if (doc.RecipientOrgUnitId is { } u) units.Add(u);
        if (doc.RecipientEmployeeId is { } re && await UnitOfAsync(re, ct) is { } ru) units.Add(ru);
        var assetIds = doc.Lines.Where(l => l.AssetId != null).Select(l => l.AssetId!.Value).ToList();
        foreach (var c in await db.Custodies.AsNoTracking().Where(c => assetIds.Contains(c.AssetId) && c.ToDate == null).ToListAsync(ct))
        {
            if (c.OrgUnitId is { } cu) units.Add(cu);
            if (c.HolderOrgUnitId is { } hu) units.Add(hu);
        }
        var siteOf = await db.Warehouses.AsNoTracking().Where(x => warehouses.Contains(x.Id)).Select(x => x.SiteId).ToListAsync(ct);

        foreach (var c in frozen)
        {
            var hit = c.Scope switch
            {
                CountScope.All => true,
                CountScope.Warehouse => c.WarehouseId is { } cw && warehouses.Contains(cw),
                CountScope.OrgUnit => c.OrgUnitId is { } cu && units.Contains(cu),
                CountScope.Site => c.SiteId is { } cs && siteOf.Contains(cs),
                _ => false,
            };
            if (hit) throw new DomainException($"الحركات مجمّدة على هذا النطاق أثناء الجرد ({c.Number ?? c.Name})");
        }
    }

    private async Task<Asset> CreateAssetAsync(Item item, SupplyDocumentLine line, SupplyDocument doc, long? warehouseId, CancellationToken ct)
    {
        if (line.Quantity != 1) throw new DomainException($"الأصل الثابت يُسجّل قطعةً قطعة ({item.Name})");
        if (line.SerialNumber is { } sn && await db.Assets.AnyAsync(a => a.SerialNumber == sn && a.ItemId == item.Id, ct))
            throw new DomainException($"الرقم التسلسلي {sn} مسجّل مسبقاً");
        var barcode = line.Barcode;
        if (barcode is not null && await db.Assets.AnyAsync(a => a.Barcode == barcode, ct)) throw new DomainException($"الباركود {barcode} مستخدم مسبقاً");
        var hasHolder = doc.Type == DocumentType.OPN && (line.HolderEmployeeId is not null || line.HolderOrgUnitId is not null);
        if (!hasHolder && warehouseId is null) throw new DomainException($"حدّد المستودع أو صاحب العهدة ({item.Name})");
        var purchase = line.PurchaseDate ?? (doc.Type == DocumentType.RCV ? doc.DocDate : null);
        var condition = line.Condition ?? AssetCondition.Good;
        var asset = new Asset
        {
            AssetNumber = await SupplyCommon.NumberAsync(numbering, SupplyCommon.AssetNumbering, "الأصول", ct),
            Barcode = barcode ?? await SupplyCommon.NumberAsync(numbering, SupplyCommon.BarcodeNumbering, "الباركود", ct),
            ItemId = item.Id, SerialNumber = line.SerialNumber, Condition = condition, Value = line.UnitValue,
            PurchaseDate = purchase,
            WarrantyEndDate = purchase is { } p && item.WarrantyMonths is > 0 ? p.AddMonths(item.WarrantyMonths.Value) : null,
            LastCountedDate = doc.Type == DocumentType.OPN ? doc.DocDate : null,   // the founding count is a count
            Status = hasHolder ? AssetStatus.InCustody
                : condition == AssetCondition.Damaged ? AssetStatus.PendingDisposal   // §5.3: recorded as is, then to the committee
                : AssetStatus.InStock,
            WarehouseId = hasHolder ? null : warehouseId,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);
        return asset;
    }

    private void OpenCustody(Asset asset, long? employeeId, long? orgUnitId, SupplyDocument doc, DateOnly today, long? employeeUnit)
    {
        if (employeeId is null && orgUnitId is null) throw new DomainException($"حدّد صاحب العهدة للأصل {asset.AssetNumber}");
        db.Custodies.Add(new Custody
        {
            AssetId = asset.Id, HolderType = employeeId is not null ? CustodyHolder.Employee : CustodyHolder.OrgUnit,
            EmployeeId = employeeId, OrgUnitId = employeeId is null ? orgUnitId : null, HolderOrgUnitId = employeeUnit,
            FromDate = today, OpenedByDocumentId = doc.Id,
        });
    }

    private async Task CloseCustodyAsync(long assetId, SupplyDocument doc, DateOnly today, CancellationToken ct)
    {
        var open = await db.Custodies.Where(c => c.AssetId == assetId && c.ToDate == null).ToListAsync(ct);
        foreach (var c in open) { c.ToDate = today; c.ClosedByDocumentId = doc.Id; }
    }

    private void Move(long itemId, long warehouseId, decimal qty, SupplyDocument doc, DateOnly today, long? assetId = null, long? orgUnitId = null) =>
        db.StockMovements.Add(new StockMovement
        {
            ItemId = itemId, WarehouseId = warehouseId, Quantity = qty, DocumentId = doc.Id, AssetId = assetId,
            OrgUnitId = orgUnitId, WorkOrderId = doc.WorkOrderId, MovementDate = today,
        });

    private async Task RequireAvailableAsync(Item item, long warehouseId, decimal qty, CancellationToken ct)
    {
        if (qty <= 0) throw new DomainException($"الكمية غير صالحة ({item.Name})");
        // Movements added in this unit of work are not in the database yet: count them too.
        var pending = db.ChangeTracker.Entries<StockMovement>()
            .Where(e => e.State == EntityState.Added && e.Entity.ItemId == item.Id && e.Entity.WarehouseId == warehouseId)
            .Sum(e => e.Entity.Quantity);
        var have = await SupplyCommon.BalanceAsync(db, item.Id, warehouseId, ct) + pending;
        if (have < qty) throw new DomainException($"الرصيد لا يكفي: {item.Name} — المتوفر {have:0.###}، المطلوب {qty:0.###}");
    }

    private async Task<long?> UnitOfAsync(long employeeId, CancellationToken ct) =>
        await db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (long?)e.OrgUnitId).FirstOrDefaultAsync(ct);

    /// <summary>§7.3: at or below the reorder level → notify the keeper (the dashboard lists it too).</summary>
    private async Task AlertLowStockAsync(IReadOnlyList<long> itemIds, IReadOnlySet<long> warehouses, SupplyDocument doc, CancellationToken ct)
    {
        if (warehouses.Count == 0) return;
        var watched = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id) && i.ReorderLevel != null).ToListAsync(ct);
        foreach (var item in watched)
        foreach (var wh in warehouses)
        {
            var bal = await SupplyCommon.BalanceAsync(db, item.Id, wh, ct);
            if (bal <= item.ReorderLevel)
                await SupplyCommon.NotifyKeeperAsync(db, wh, "مخزون حرج", $"{item.Name}: الرصيد {bal:0.###} عند حد الطلب أو تحته", "SupplyDocument", doc.Id, ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
