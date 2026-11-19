using System.Globalization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>Settings, category tree, items, warehouses and monthly quotas. All values the Authority sets are data.</summary>
public sealed class SupplyCatalogService(AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit) : ISupplyCatalogService
{
    private static readonly string[] WorkflowKeys = [.. Enum.GetNames<DocumentType>(), "REQ"];

    // ---- settings ----

    public async Task<SupplySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await SupplyCommon.SettingsAsync(db, ct);
        return new SupplySettingsDto(
            WorkflowKeys.ToDictionary(k => k, k => SupplyCommon.Long(s, SupplySettingKeys.Workflow(k))),
            SupplyCommon.Dec(s, SupplySettingKeys.ReplacementCostPercent), SupplyCommon.Int(s, SupplySettingKeys.WarrantyAlertDays),
            SupplyCommon.Int(s, SupplySettingKeys.SuspendedRequestDays), SupplyCommon.Int(s, SupplySettingKeys.StagnantDays),
            SupplyCommon.Int(s, SupplySettingKeys.CountOverdueDays),
            DateOnly.TryParse(s.GetValueOrDefault(SupplySettingKeys.OpeningLockedOn), CultureInfo.InvariantCulture, out var d) ? d : null);
    }

    public async Task<Result<SupplySettingsDto>> SaveSettingsAsync(SaveSupplySettingsRequest r, CancellationToken ct = default)
    {
        if (r.ReplacementCostPercent is < 0 or > 1000) return Result<SupplySettingsDto>.Fail("نسبة الاستبدال غير صالحة");
        foreach (var n in new[] { r.WarrantyAlertDays, r.SuspendedRequestDays, r.StagnantDays, r.CountOverdueDays })
            if (n is < 0 or > 3650) return Result<SupplySettingsDto>.Fail("عدد الأيام غير صالح");
        var workflowIds = r.Workflows.Values.Where(v => v is not null).Select(v => v!.Value).Distinct().ToList();
        if (workflowIds.Count > 0)
        {
            var found = await db.WorkflowDefinitions.CountAsync(w => workflowIds.Contains(w.Id), ct);
            if (found != workflowIds.Count) return Result<SupplySettingsDto>.Fail("مسار اعتماد غير موجود");
        }

        var rows = await db.SupplySettings.ToDictionaryAsync(x => x.Key, ct);
        void Set(string key, string? value)
        {
            if (rows.TryGetValue(key, out var row)) row.Value = value ?? string.Empty;
            else if (value is not null) db.SupplySettings.Add(new SupplySetting { Key = key, Value = value });
        }
        foreach (var k in WorkflowKeys)
            Set(SupplySettingKeys.Workflow(k), r.Workflows.TryGetValue(k, out var id) && id is { } v ? v.ToString(CultureInfo.InvariantCulture) : null);
        Set(SupplySettingKeys.ReplacementCostPercent, r.ReplacementCostPercent?.ToString(CultureInfo.InvariantCulture));
        Set(SupplySettingKeys.WarrantyAlertDays, r.WarrantyAlertDays?.ToString(CultureInfo.InvariantCulture));
        Set(SupplySettingKeys.SuspendedRequestDays, r.SuspendedRequestDays?.ToString(CultureInfo.InvariantCulture));
        Set(SupplySettingKeys.StagnantDays, r.StagnantDays?.ToString(CultureInfo.InvariantCulture));
        Set(SupplySettingKeys.CountOverdueDays, r.CountOverdueDays?.ToString(CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", "SupplySettings", 0, "إعدادات اللوازم", newValues: CoreGuards.Json(r), ct: ct);
        return Result<SupplySettingsDto>.Ok(await GetSettingsAsync(ct));
    }

    // ---- categories ----

    public async Task<IReadOnlyList<ItemCategoryDto>> CategoriesAsync(CancellationToken ct = default)
    {
        var counts = await db.Items.AsNoTracking().GroupBy(i => i.CategoryId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return (await db.ItemCategories.AsNoTracking().OrderBy(c => c.Kind).ThenBy(c => c.Code).ToListAsync(ct))
            .Select(c => new ItemCategoryDto(c.Id, c.ParentId, c.Code, c.Name, c.Kind.ToString(), c.IsActive,
                counts.GetValueOrDefault(c.Id), HrCommon.B64(c.RowVersion))).ToList();
    }

    public async Task<Result<ItemCategoryDto>> SaveCategoryAsync(long? id, SaveItemCategoryRequest r, CancellationToken ct = default)
    {
        var code = CoreGuards.Clean(r.Code);
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32 || name.Length is 0 or > 150) return Result<ItemCategoryDto>.Fail("الرمز والاسم مطلوبان");
        if (!Enum.TryParse<ItemKind>(r.Kind, true, out var kind)) return Result<ItemCategoryDto>.Fail("نوع الفئة غير صالح");
        if (await db.ItemCategories.AnyAsync(c => c.Code == code && c.Id != (id ?? 0), ct)) return Result<ItemCategoryDto>.Fail("رمز الفئة مستخدم");
        if (r.ParentId is { } pid)
        {
            var parent = await db.ItemCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == pid, ct);
            if (parent is null) return Result<ItemCategoryDto>.Fail("الفئة الرئيسية غير موجودة");
            if (parent.ParentId is not null) return Result<ItemCategoryDto>.Fail("الشجرة مستويان: فئة رئيسية ثم فرعية");
            if (parent.Kind != kind) return Result<ItemCategoryDto>.Fail("الفئة الفرعية من نوع فئتها الرئيسية");
            if (pid == id) return Result<ItemCategoryDto>.Fail("لا تكون الفئة أباً لنفسها");
        }

        ItemCategory c;
        if (id is { } existing)
        {
            c = await db.ItemCategories.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new KeyNotFoundException();
            if (!CoreGuards.IsCurrent(c.RowVersion, r.RowVersion)) return Result<ItemCategoryDto>.Fail(CoreGuards.Stale);
            if (c.Kind != kind && await db.Items.AnyAsync(i => i.CategoryId == c.Id, ct))
                return Result<ItemCategoryDto>.Fail("لا يتغير نوع فئة فيها أصناف");
        }
        else db.ItemCategories.Add(c = new ItemCategory());
        c.Code = code; c.Name = name; c.Kind = kind; c.ParentId = r.ParentId; c.IsActive = r.IsActive;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ItemCategoryDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync(id is null ? "Created" : "Updated", "ItemCategory", c.Id, name, ct: ct);
        return Result<ItemCategoryDto>.Ok((await CategoriesAsync(ct)).First(x => x.Id == c.Id));
    }

    // ---- items ----

    public async Task<IReadOnlyList<ItemDto>> ItemsAsync(bool activeOnly, CancellationToken ct = default) =>
        (await db.Items.AsNoTracking().Include(i => i.Category).Where(i => !activeOnly || i.IsActive).OrderBy(i => i.Code).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<Result<ItemDto>> SaveItemAsync(long? id, SaveItemRequest r, CancellationToken ct = default)
    {
        var code = CoreGuards.Clean(r.Code);
        var name = CoreGuards.Clean(r.Name);
        var unit = CoreGuards.Clean(r.Unit);
        if (code.Length is 0 or > 32 || name.Length is 0 or > 200) return Result<ItemDto>.Fail("الرمز والاسم مطلوبان");
        if (unit.Length is 0 or > 32) return Result<ItemDto>.Fail("وحدة القياس مطلوبة");
        if (r.ReorderLevel is < 0 || r.MinLevel is < 0) return Result<ItemDto>.Fail("الحدود لا تكون سالبة");
        if (r.UsefulLifeMonths is < 0 or > 1200 || r.WarrantyMonths is < 0 or > 1200) return Result<ItemDto>.Fail("المدة غير صالحة");
        var cat = await db.ItemCategories.FirstOrDefaultAsync(c => c.Id == r.CategoryId, ct);
        if (cat is null) return Result<ItemDto>.Fail("الفئة غير موجودة");
        if (await db.Items.AnyAsync(i => i.Code == code && i.Id != (id ?? 0), ct)) return Result<ItemDto>.Fail("رمز الصنف مستخدم");

        Item item;
        if (id is { } existing)
        {
            item = await db.Items.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == existing, ct) ?? throw new KeyNotFoundException();
            if (!CoreGuards.IsCurrent(item.RowVersion, r.RowVersion)) return Result<ItemDto>.Fail(CoreGuards.Stale);
            // The kind decides how the item is tracked; once it has stock or pieces it cannot change.
            if (item.Kind != cat.Kind && (await db.StockMovements.AnyAsync(m => m.ItemId == item.Id, ct) || await db.Assets.AnyAsync(a => a.ItemId == item.Id, ct)))
                return Result<ItemDto>.Fail("لا يتغير نوع صنف له حركات");
        }
        else db.Items.Add(item = new Item());
        item.Code = code; item.Name = name; item.CategoryId = cat.Id; item.Category = cat; item.Kind = cat.Kind; item.Unit = unit;
        item.ReorderLevel = r.ReorderLevel; item.MinLevel = r.MinLevel;
        item.UsefulLifeMonths = cat.Kind == ItemKind.FixedAsset ? r.UsefulLifeMonths : null;
        item.WarrantyMonths = cat.Kind == ItemKind.FixedAsset ? r.WarrantyMonths : null;
        item.IsActive = r.IsActive;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ItemDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync(id is null ? "Created" : "Updated", "Item", item.Id, $"{code} {name}", ct: ct);
        return Result<ItemDto>.Ok(ToDto(item));
    }

    internal static ItemDto ToDto(Item i) => new(i.Id, i.Code, i.Name, i.CategoryId, i.Category?.Name ?? string.Empty, i.Kind.ToString(),
        i.Unit, i.ReorderLevel, i.MinLevel, i.UsefulLifeMonths, i.WarrantyMonths, i.IsActive, HrCommon.B64(i.RowVersion));

    // ---- warehouses ----

    public async Task<IReadOnlyList<WarehouseDto>> WarehousesAsync(CancellationToken ct = default)
    {
        var mine = await SupplyCommon.WarehouseScopeAsync(db, scope, user, ct);
        var rows = await db.Warehouses.AsNoTracking().OrderBy(w => w.Kind).ThenBy(w => w.Code).ToListAsync(ct);
        var sites = await db.Sites.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var keepers = await HrCommon.NamesAsync(db, rows.Where(w => w.KeeperEmployeeId != null).Select(w => w.KeeperEmployeeId!.Value), ct);
        return rows.Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Kind.ToString(), w.SiteId,
            w.SiteId is { } s ? sites.GetValueOrDefault(s) : null, w.KeeperEmployeeId,
            w.KeeperEmployeeId is { } k ? keepers.GetValueOrDefault(k) : null, w.IsActive, mine.Contains(w.Id), HrCommon.B64(w.RowVersion))).ToList();
    }

    public async Task<Result<WarehouseDto>> SaveWarehouseAsync(long? id, SaveWarehouseRequest r, CancellationToken ct = default)
    {
        var code = CoreGuards.Clean(r.Code);
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32 || name.Length is 0 or > 200) return Result<WarehouseDto>.Fail("الرمز والاسم مطلوبان");
        if (!Enum.TryParse<WarehouseKind>(r.Kind, true, out var kind)) return Result<WarehouseDto>.Fail("نوع المستودع غير صالح");
        if (await db.Warehouses.AnyAsync(w => w.Code == code && w.Id != (id ?? 0), ct)) return Result<WarehouseDto>.Fail("رمز المستودع مستخدم");
        if (r.SiteId is { } sid && !await db.Sites.AnyAsync(s => s.Id == sid && s.IsActive, ct)) return Result<WarehouseDto>.Fail("المعبر غير موجود");
        if (kind == WarehouseKind.Site && r.SiteId is null) return Result<WarehouseDto>.Fail("مستودع المعبر يحتاج تحديد المعبر");
        if (r.KeeperEmployeeId is { } kid && !await db.Employees.AnyAsync(e => e.Id == kid && e.Status == Domain.Core.EmployeeStatus.Active, ct))
            return Result<WarehouseDto>.Fail("أمين المستودع غير موجود أو ليس على رأس عمله");

        Warehouse w;
        if (id is { } existing)
        {
            w = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new KeyNotFoundException();
            if (!CoreGuards.IsCurrent(w.RowVersion, r.RowVersion)) return Result<WarehouseDto>.Fail(CoreGuards.Stale);
            if (!r.IsActive && w.IsActive && await db.StockMovements.Where(m => m.WarehouseId == w.Id).SumAsync(m => m.Quantity, ct) != 0)
                return Result<WarehouseDto>.Fail("لا يُوقف مستودع فيه رصيد. انقل الرصيد أولاً");
        }
        else db.Warehouses.Add(w = new Warehouse());
        var oldKeeper = w.KeeperEmployeeId;
        w.Code = code; w.Name = name; w.Kind = kind; w.SiteId = r.SiteId; w.KeeperEmployeeId = r.KeeperEmployeeId; w.IsActive = r.IsActive;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<WarehouseDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync(id is null ? "Created" : "Updated", "Warehouse", w.Id, name,
            oldValues: oldKeeper?.ToString(CultureInfo.InvariantCulture), newValues: w.KeeperEmployeeId?.ToString(CultureInfo.InvariantCulture), ct: ct);
        return Result<WarehouseDto>.Ok((await WarehousesAsync(ct)).First(x => x.Id == w.Id));
    }

    // ---- monthly quotas ----

    public async Task<IReadOnlyList<MonthlyQuotaDto>> QuotasAsync(CancellationToken ct = default)
    {
        var rows = await db.MonthlyQuotas.AsNoTracking().OrderBy(q => q.OrgUnitId).ToListAsync(ct);
        var units = await SupplyCommon.UnitNamesAsync(db, rows.Select(q => q.OrgUnitId), ct);
        var items = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        return rows.Select(q => new MonthlyQuotaDto(q.Id, q.OrgUnitId, units.GetValueOrDefault(q.OrgUnitId) ?? string.Empty, q.ItemId,
            items.GetValueOrDefault(q.ItemId)?.Name ?? string.Empty, items.GetValueOrDefault(q.ItemId)?.Unit ?? string.Empty,
            q.WarehouseId, whs.GetValueOrDefault(q.WarehouseId) ?? string.Empty, q.Quantity, q.IsActive)).ToList();
    }

    public async Task<Result<MonthlyQuotaDto>> SaveQuotaAsync(long? id, SaveMonthlyQuotaRequest r, CancellationToken ct = default)
    {
        if (r.Quantity is <= 0 or > 100000) return Result<MonthlyQuotaDto>.Fail("الكمية غير صالحة");
        if (!await db.OrgUnits.AnyAsync(u => u.Id == r.OrgUnitId, ct)) return Result<MonthlyQuotaDto>.Fail("الدائرة غير موجودة");
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == r.ItemId && i.IsActive, ct);
        if (item is null) return Result<MonthlyQuotaDto>.Fail("الصنف غير موجود");
        if (item.Kind != ItemKind.Consumable) return Result<MonthlyQuotaDto>.Fail("الحصة الشهرية للمستهلكات فقط");
        if (!await db.Warehouses.AnyAsync(w => w.Id == r.WarehouseId && w.IsActive, ct)) return Result<MonthlyQuotaDto>.Fail("المستودع غير موجود");
        if (await db.MonthlyQuotas.AnyAsync(q => q.OrgUnitId == r.OrgUnitId && q.ItemId == r.ItemId && q.Id != (id ?? 0), ct))
            return Result<MonthlyQuotaDto>.Fail("للدائرة حصة لهذا الصنف. عدّلها");

        MonthlyQuota q;
        if (id is { } existing) q = await db.MonthlyQuotas.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new KeyNotFoundException();
        else db.MonthlyQuotas.Add(q = new MonthlyQuota());
        q.OrgUnitId = r.OrgUnitId; q.ItemId = r.ItemId; q.WarehouseId = r.WarehouseId; q.Quantity = r.Quantity; q.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(id is null ? "Created" : "Updated", "MonthlyQuota", q.Id, item.Name, newValues: $"{r.Quantity}", ct: ct);
        return Result<MonthlyQuotaDto>.Ok((await QuotasAsync(ct)).First(x => x.Id == q.Id));
    }

    public async Task<Result<bool>> DeleteQuotaAsync(long id, CancellationToken ct = default)
    {
        var q = await db.MonthlyQuotas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return Result<bool>.Fail("الحصة غير موجودة");
        db.MonthlyQuotas.Remove(q);   // soft delete by the interceptor
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "MonthlyQuota", id, null, ct: ct);
        return Result<bool>.Ok(true);
    }
}
