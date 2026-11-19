using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Core;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>
/// الجرد الدوري الأعمى (§7.1, standards §8.3): plan → committee → (freeze) → blind sheet → entries → server-side
/// comparison → justify every variance → SUP-ADJ into approval → closed (freeze lifted) when it is executed.
/// No response of the counting steps carries an expected quantity.
/// </summary>
public sealed class SupplyCountService(
    AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit, IApprovalService approvals,
    INumberingService numbering) : ISupplyCountService
{
    private const string NotFound = "دورة الجرد غير موجودة";

    public async Task<IReadOnlyList<CountCycleDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.CountCycles.AsNoTracking().Include(c => c.Committee).OrderByDescending(c => c.Id).Take(200).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<Result<CountCycleDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().Include(x => x.Committee).FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? Result<CountCycleDto>.Fail(NotFound) : Result<CountCycleDto>.Ok((await ToDtosAsync([c], ct))[0]);
    }

    public async Task<Result<CountCycleDto>> CreateAsync(SaveCountCycleRequest r, CancellationToken ct = default)
    {
        var c = new CountCycle();
        var error = await ApplyAsync(c, r, ct);
        if (error is not null) return Result<CountCycleDto>.Fail(error);
        c.Number = await SupplyCommon.NumberAsync(numbering, SupplyCommon.CountNumbering, "دورات الجرد", ct);
        db.CountCycles.Add(c);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "CountCycle", c.Id, c.Name, ct: ct);
        return await GetAsync(c.Id, ct);
    }

    public async Task<Result<CountCycleDto>> UpdateAsync(long id, SaveCountCycleRequest r, CancellationToken ct = default)
    {
        var c = await db.CountCycles.Include(x => x.Committee).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountCycleDto>.Fail(NotFound);
        if (c.Status != CountStatus.Planning) return Result<CountCycleDto>.Fail("تُعدَّل الدورة في مرحلة التخطيط فقط");
        if (!CoreGuards.IsCurrent(c.RowVersion, r.RowVersion)) return Result<CountCycleDto>.Fail(CoreGuards.Stale);
        var error = await ApplyAsync(c, r, ct);
        if (error is not null) return Result<CountCycleDto>.Fail(error);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<CountCycleDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "CountCycle", c.Id, c.Name, ct: ct);
        return await GetAsync(c.Id, ct);
    }

    public async Task<Result<CountCycleDto>> StartAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.Include(x => x.Committee).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountCycleDto>.Fail(NotFound);
        if (c.Status != CountStatus.Planning) return Result<CountCycleDto>.Fail("الدورة بدأت مسبقاً");
        if (c.Committee.Count == 0) return Result<CountCycleDto>.Fail("شكّل اللجنة أولاً");
        c.Status = CountStatus.Counting;
        foreach (var m in c.Committee)
            await SupplyCommon.NotifyEmployeeAsync(db, m.EmployeeId, "بدء الجرد", $"{c.Number}: {c.Name}", "CountCycle", c.Id, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Started", "CountCycle", c.Id, c.Name, newValues: c.FreezeMovements ? "freeze" : null, ct: ct);
        return await GetAsync(c.Id, ct);
    }

    public async Task<Result<IReadOnlyList<CountSheetLineDto>>> SheetAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<IReadOnlyList<CountSheetLineDto>>.Fail(NotFound);
        if (!await MayCountAsync(c, ct)) return Result<IReadOnlyList<CountSheetLineDto>>.Fail("قائمة الجرد للجنة الدورة");
        var (warehouses, _) = await ScopeAsync(c, ct);
        var whNames = await SupplyCommon.WarehouseNamesAsync(db, ct);
        var items = await db.Items.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Code).ToListAsync(ct);
        var lines = new List<CountSheetLineDto>();
        // Quantity items: every active item in every warehouse of the scope — whether or not the records show stock,
        // so the list itself does not hint at what is expected.
        foreach (var wh in warehouses)
            lines.AddRange(items.Where(i => i.Kind != ItemKind.FixedAsset)
                .Select(i => new CountSheetLineDto(i.Id, i.Code, i.Name, i.Unit, i.Kind.ToString(), wh, whNames.GetValueOrDefault(wh))));
        // Assets are counted by scanning; the sheet lists the asset items only, not the pieces.
        lines.AddRange(items.Where(i => i.Kind == ItemKind.FixedAsset)
            .Select(i => new CountSheetLineDto(i.Id, i.Code, i.Name, i.Unit, i.Kind.ToString(), null, null)));
        return Result<IReadOnlyList<CountSheetLineDto>>.Ok(lines);
    }

    public async Task<Result<IReadOnlyList<CountEntryDto>>> EntriesAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<IReadOnlyList<CountEntryDto>>.Fail(NotFound);
        if (!await MayCountAsync(c, ct)) return Result<IReadOnlyList<CountEntryDto>>.Fail("الإدخالات للجنة الدورة");
        var rows = await db.CountEntries.AsNoTracking().Where(e => e.CountCycleId == id).OrderByDescending(e => e.Id).ToListAsync(ct);
        return Result<IReadOnlyList<CountEntryDto>>.Ok(await EntryDtosAsync(rows, ct));
    }

    public async Task<Result<CountEntryDto>> EnterAsync(long id, SaveCountEntry e, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountEntryDto>.Fail(NotFound);
        if (c.Status != CountStatus.Counting) return Result<CountEntryDto>.Fail("الدورة ليست في مرحلة العدّ");
        if (!await MayCountAsync(c, ct)) return Result<CountEntryDto>.Fail("العدّ لأعضاء اللجنة");
        AssetCondition? condition = null;
        if (e.Condition is { Length: > 0 } cs)
        {
            if (!Enum.TryParse<AssetCondition>(cs, true, out var cc)) return Result<CountEntryDto>.Fail("الحالة غير صالحة");
            condition = cc;
        }
        var note = CoreGuards.CleanOrNull(e.Note);
        if (note?.Length > 500) return Result<CountEntryDto>.Fail("الملاحظة طويلة");
        var entry = new CountEntry { CountCycleId = id, Condition = condition, Note = note };
        var barcode = CoreGuards.CleanOrNull(e.Barcode);
        if (barcode is not null)
        {
            if (barcode.Length > 64) return Result<CountEntryDto>.Fail("الباركود غير صالح");
            var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Barcode == barcode || a.AssetNumber == barcode, ct);
            entry.Barcode = asset?.Barcode ?? barcode;   // an unknown label is still recorded: it becomes a surplus variance
            entry.AssetId = asset?.Id;
            entry.ItemId = asset?.ItemId;
            entry.Quantity = 1;
        }
        else
        {
            if (e.ItemId is not { } itemId || e.WarehouseId is not { } wh) return Result<CountEntryDto>.Fail("امسح باركود أصل أو اختر صنفاً ومستودعاً");
            if (e.Quantity is not { } q || q < 0 || q > 10_000_000) return Result<CountEntryDto>.Fail("الكمية غير صالحة");
            var (warehouses, _) = await ScopeAsync(c, ct);
            if (!warehouses.Contains(wh)) return Result<CountEntryDto>.Fail("المستودع خارج نطاق الدورة");
            var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is null || item.Kind == ItemKind.FixedAsset) return Result<CountEntryDto>.Fail("الأصول تُجرد بمسح الباركود");
            entry.ItemId = itemId; entry.WarehouseId = wh; entry.Quantity = q;
        }
        db.CountEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return Result<CountEntryDto>.Ok((await EntryDtosAsync([entry], ct))[0]);
    }

    public async Task<Result<bool>> DeleteEntryAsync(long id, long entryId, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<bool>.Fail(NotFound);
        if (c.Status != CountStatus.Counting) return Result<bool>.Fail("الدورة ليست في مرحلة العدّ");
        if (!await MayCountAsync(c, ct)) return Result<bool>.Fail("العدّ لأعضاء اللجنة");
        var e = await db.CountEntries.FirstOrDefaultAsync(x => x.Id == entryId && x.CountCycleId == id, ct);
        if (e is null) return Result<bool>.Fail("الإدخال غير موجود");
        db.CountEntries.Remove(e);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<IReadOnlyList<CountVarianceDto>>> ReconcileAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<IReadOnlyList<CountVarianceDto>>.Fail(NotFound);
        if (c.Status != CountStatus.Counting) return Result<IReadOnlyList<CountVarianceDto>>.Fail("المطابقة بعد انتهاء العدّ");

        var (warehouses, units) = await ScopeAsync(c, ct);
        var expectedQty = (await db.StockMovements.AsNoTracking()
                .Where(m => warehouses.Contains(m.WarehouseId) && m.AssetId == null)
                .GroupBy(m => new { m.ItemId, m.WarehouseId }).Select(g => new { g.Key.ItemId, g.Key.WarehouseId, Q = g.Sum(m => m.Quantity) })
                .ToListAsync(ct))
            .Where(x => x.Q != 0).ToDictionary(x => (x.ItemId, x.WarehouseId), x => x.Q);
        var entries = await db.CountEntries.AsNoTracking().Where(e => e.CountCycleId == id).ToListAsync(ct);
        var countedQty = entries.Where(e => e.Barcode == null && e.ItemId != null && e.WarehouseId != null)
            .GroupBy(e => (e.ItemId!.Value, e.WarehouseId!.Value)).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity ?? 0));

        var expectedAssets = await ExpectedAssetsAsync(warehouses, units, ct);
        var scannedIds = entries.Where(e => e.AssetId != null).Select(e => e.AssetId!.Value).ToList();
        var scannedAssets = await db.Assets.AsNoTracking().Where(a => scannedIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var scanned = entries.Where(e => e.Barcode != null)
            .Select(e => new CountReconciler.ScannedAsset(e.Barcode!, e.AssetId, e.ItemId, e.Condition, e.AssetId != null)).ToList();

        var variances = CountReconciler.Compare(expectedQty, countedQty,
            expectedAssets.Select(a => new CountReconciler.ExpectedAsset(a.Id, a.Barcode, a.ItemId, a.Condition)).ToList(), scanned);

        foreach (var old in await db.CountVariances.Where(v => v.CountCycleId == id).ToListAsync(ct)) db.CountVariances.Remove(old);
        foreach (var v in variances)
            db.CountVariances.Add(new CountVariance
            {
                CountCycleId = id, Kind = v.Kind, ItemId = v.ItemId, WarehouseId = v.WarehouseId, AssetId = v.AssetId, Barcode = v.Barcode,
                Expected = v.Expected, Counted = v.Counted, Difference = v.Counted - v.Expected, CountedCondition = v.CountedCondition,
                Description = v.Kind == VarianceKind.Surplus && v.ItemId is null ? "باركود غير معرّف في النظام" : null,
            });
        // Every piece the committee actually saw has now been counted.
        var today = SupplyCommon.Today();
        foreach (var a in await db.Assets.Where(a => scannedIds.Contains(a.Id)).ToListAsync(ct)) a.LastCountedDate = today;
        c.Status = CountStatus.Reconciled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Reconciled", "CountCycle", c.Id, c.Name, newValues: $"variances={variances.Count}", ct: ct);
        return await VariancesAsync(id, ct);
    }

    public async Task<Result<IReadOnlyList<CountVarianceDto>>> VariancesAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<IReadOnlyList<CountVarianceDto>>.Fail(NotFound);
        if (c.Status is CountStatus.Planning or CountStatus.Counting)
            return Result<IReadOnlyList<CountVarianceDto>>.Fail("الفروقات تظهر بعد المطابقة");   // blind until counting ends
        var rows = await db.CountVariances.AsNoTracking().Where(v => v.CountCycleId == id).OrderBy(v => v.Kind).ThenBy(v => v.Id).ToListAsync(ct);
        var itemIds = rows.Where(v => v.ItemId != null).Select(v => v.ItemId!.Value).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        var assetIds = rows.Where(v => v.AssetId != null).Select(v => v.AssetId!.Value).ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.AssetNumber, ct);
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        IReadOnlyList<CountVarianceDto> list = rows.Select(v => new CountVarianceDto(v.Id, v.Kind.ToString(), v.ItemId,
            v.ItemId is { } i ? items.GetValueOrDefault(i) : null, v.AssetId, v.AssetId is { } a ? assets.GetValueOrDefault(a) : null,
            v.Barcode, v.WarehouseId, v.WarehouseId is { } w ? whs.GetValueOrDefault(w) : null, v.Expected, v.Counted, v.Difference,
            v.CountedCondition?.ToString(), v.Description, v.Justification)).ToList();
        return Result<IReadOnlyList<CountVarianceDto>>.Ok(list);
    }

    public async Task<Result<CountVarianceDto>> JustifyAsync(long id, long varianceId, string justification, CancellationToken ct = default)
    {
        var c = await db.CountCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountVarianceDto>.Fail(NotFound);
        if (c.Status != CountStatus.Reconciled) return Result<CountVarianceDto>.Fail("التبرير في مرحلة المطابقة");
        var text = CoreGuards.Clean(justification);
        if (text.Length is 0 or > 1000) return Result<CountVarianceDto>.Fail("التبرير مطلوب");
        var v = await db.CountVariances.FirstOrDefaultAsync(x => x.Id == varianceId && x.CountCycleId == id, ct);
        if (v is null) return Result<CountVarianceDto>.Fail("الفرق غير موجود");
        v.Justification = text;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Justified", "CountVariance", v.Id, v.Kind.ToString(), newValues: text, ct: ct);
        return Result<CountVarianceDto>.Ok((await VariancesAsync(id, ct)).Value!.First(x => x.Id == varianceId));
    }

    public async Task<Result<CountCycleDto>> SettleAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountCycleDto>.Fail(NotFound);
        if (c.Status != CountStatus.Reconciled) return Result<CountCycleDto>.Fail("الاعتماد بعد المطابقة");
        var variances = await db.CountVariances.AsNoTracking().Where(v => v.CountCycleId == id).ToListAsync(ct);
        if (variances.Any(v => string.IsNullOrWhiteSpace(v.Justification))) return Result<CountCycleDto>.Fail("برّر كل فرق قبل الاعتماد");

        // Only quantity differences, missing pieces and condition changes touch the records. A piece found in the
        // wrong place goes by a custody-transfer document; an unknown label is investigated, not booked.
        var lines = new List<SupplyDocumentLine>();
        foreach (var v in variances)
        {
            switch (v.Kind)
            {
                case VarianceKind.Shortage or VarianceKind.Surplus when v.AssetId is null && v.ItemId is { } item && v.WarehouseId is { } wh:
                    lines.Add(new SupplyDocumentLine { ItemId = item, WarehouseId = wh, Quantity = v.Difference, Note = v.Justification });
                    break;
                case VarianceKind.Missing when v.AssetId is { } a && v.ItemId is { } item:
                    lines.Add(new SupplyDocumentLine { ItemId = item, AssetId = a, Quantity = -1, Note = v.Justification });
                    break;
                case VarianceKind.ConditionChanged when v.AssetId is { } a && v.ItemId is { } item:
                    lines.Add(new SupplyDocumentLine { ItemId = item, AssetId = a, Quantity = 0, Condition = v.CountedCondition, Note = v.Justification });
                    break;
            }
        }

        if (lines.Count == 0)
        {
            c.Status = CountStatus.Closed;
            c.ClosedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Closed", "CountCycle", c.Id, c.Name, newValues: "no adjustment", ct: ct);
            return await GetAsync(id, ct);
        }

        var doc = new SupplyDocument
        {
            Type = DocumentType.ADJ, DocDate = SupplyCommon.Today(), CountCycleId = c.Id, WarehouseId = c.WarehouseId,
            Title = $"تسوية جرد {c.Number} — {c.Name}",
        };
        foreach (var l in lines) doc.Lines.Add(l);
        db.SupplyDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        var error = await SupplyDocumentService.SubmitCoreAsync(db, approvals, numbering, audit, doc, ct);
        if (error is not null)
        {
            db.SupplyDocuments.Remove(doc);
            await db.SaveChangesAsync(ct);
            return Result<CountCycleDto>.Fail(error);
        }
        c.Status = CountStatus.Settling;
        c.AdjustmentDocumentId = doc.Id;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Settling", "CountCycle", c.Id, c.Name, newValues: doc.Number, ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<CountCycleDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var c = await db.CountCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CountCycleDto>.Fail(NotFound);
        if (c.Status is CountStatus.Settling or CountStatus.Closed or CountStatus.Cancelled)
            return Result<CountCycleDto>.Fail("لا تُلغى الدورة في حالتها الحالية");
        c.Status = CountStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "CountCycle", c.Id, c.Name, ct: ct);
        return await GetAsync(id, ct);
    }

    // ---- helpers ----

    private async Task<string?> ApplyAsync(CountCycle c, SaveCountCycleRequest r, CancellationToken ct)
    {
        var name = CoreGuards.Clean(r.Name);
        if (name.Length is 0 or > 200) return "اسم الدورة مطلوب";
        if (!Enum.TryParse<CountScope>(r.Scope, true, out var s)) return "النطاق غير صالح";
        if (r.PlannedDate == default) return "تاريخ الجرد مطلوب";
        switch (s)
        {
            case CountScope.Warehouse when r.WarehouseId is not { } w || !await db.Warehouses.AnyAsync(x => x.Id == w, ct): return "حدّد المستودع";
            case CountScope.OrgUnit when r.OrgUnitId is not { } u || !await db.OrgUnits.AnyAsync(x => x.Id == u, ct): return "حدّد الدائرة";
            case CountScope.Site when r.SiteId is not { } st || !await db.Sites.AnyAsync(x => x.Id == st, ct): return "حدّد المعبر";
        }
        var members = r.CommitteeEmployeeIds.Distinct().ToList();
        if (members.Count > 20) return "اللجنة 20 عضواً كحد أقصى";
        if (await db.Employees.CountAsync(e => members.Contains(e.Id) && e.Status == EmployeeStatus.Active, ct) != members.Count)
            return "عضو لجنة غير موجود أو ليس على رأس عمله";
        c.Name = name; c.Scope = s; c.PlannedDate = r.PlannedDate; c.FreezeMovements = r.FreezeMovements;
        c.WarehouseId = s == CountScope.Warehouse ? r.WarehouseId : null;
        c.OrgUnitId = s == CountScope.OrgUnit ? r.OrgUnitId : null;
        c.SiteId = s == CountScope.Site ? r.SiteId : null;
        foreach (var m in c.Committee.Where(m => !members.Contains(m.EmployeeId)).ToList()) { db.CountCommitteeMembers.Remove(m); c.Committee.Remove(m); }
        await db.SaveChangesAsync(ct);   // soft-delete first: the (cycle, employee) index is unique on live rows
        foreach (var e in members.Where(e => c.Committee.All(m => m.EmployeeId != e))) c.Committee.Add(new CountCommitteeMember { EmployeeId = e });
        return null;
    }

    /// <summary>Warehouses and units the cycle covers. A unit scope counts its custody; a site its warehouses and units.</summary>
    private async Task<(List<long> Warehouses, List<long> Units)> ScopeAsync(CountCycle c, CancellationToken ct) => c.Scope switch
    {
        CountScope.Warehouse => ([c.WarehouseId!.Value], []),
        CountScope.OrgUnit => ([], [c.OrgUnitId!.Value]),
        CountScope.Site => (await db.Warehouses.AsNoTracking().Where(w => w.SiteId == c.SiteId).Select(w => w.Id).ToListAsync(ct),
                            await db.OrgUnits.AsNoTracking().Where(u => u.SiteId == c.SiteId).Select(u => u.Id).ToListAsync(ct)),
        _ => (await db.Warehouses.AsNoTracking().Select(w => w.Id).ToListAsync(ct), await db.OrgUnits.AsNoTracking().Select(u => u.Id).ToListAsync(ct)),
    };

    private async Task<List<Asset>> ExpectedAssetsAsync(List<long> warehouses, List<long> units, CancellationToken ct)
    {
        var inStores = await db.Assets.AsNoTracking()
            .Where(a => a.WarehouseId != null && warehouses.Contains(a.WarehouseId.Value)
                && a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Missing).ToListAsync(ct);
        if (units.Count == 0) return inStores;
        var staff = db.Employees.AsNoTracking().Where(e => units.Contains(e.OrgUnitId)).Select(e => e.Id);
        var custodyIds = await db.Custodies.AsNoTracking()
            .Where(c => c.ToDate == null && ((c.OrgUnitId != null && units.Contains(c.OrgUnitId.Value))
                || (c.EmployeeId != null && staff.Contains(c.EmployeeId.Value))))
            .Select(c => c.AssetId).ToListAsync(ct);
        var held = await db.Assets.AsNoTracking().Where(a => custodyIds.Contains(a.Id)).ToListAsync(ct);
        return inStores.Concat(held).DistinctBy(a => a.Id).ToList();
    }

    /// <summary>Committee members count; SupplyCount.Edit (or the supply manager) may also enter.</summary>
    private async Task<bool> MayCountAsync(CountCycle c, CancellationToken ct)
    {
        if (await SupplyCommon.SeesAllAsync(scope, ct)) return true;
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        return me is not null && await db.CountCommitteeMembers.AnyAsync(m => m.CountCycleId == c.Id && m.EmployeeId == me.Id, ct);
    }

    private async Task<IReadOnlyList<CountEntryDto>> EntryDtosAsync(IReadOnlyList<CountEntry> rows, CancellationToken ct)
    {
        var itemIds = rows.Where(e => e.ItemId != null).Select(e => e.ItemId!.Value).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        var assetIds = rows.Where(e => e.AssetId != null).Select(e => e.AssetId!.Value).ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.AssetNumber, ct);
        return rows.Select(e => new CountEntryDto(e.Id, e.ItemId, e.ItemId is { } i ? items.GetValueOrDefault(i) : null, e.WarehouseId,
            e.Quantity, e.Barcode, e.AssetId, e.AssetId is { } a ? assets.GetValueOrDefault(a) : null, e.Condition?.ToString(), e.Note,
            e.CreatedAt)).ToList();
    }

    private async Task<IReadOnlyList<CountCycleDto>> ToDtosAsync(IReadOnlyList<CountCycle> rows, CancellationToken ct)
    {
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id;
        var whs = await SupplyCommon.WarehouseNamesAsync(db, ct);
        var units = await SupplyCommon.UnitNamesAsync(db, rows.Where(c => c.OrgUnitId != null).Select(c => c.OrgUnitId!.Value), ct);
        var sites = await db.Sites.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var names = await HrCommon.NamesAsync(db, rows.SelectMany(c => c.Committee).Select(m => m.EmployeeId), ct);
        var ids = rows.Select(c => c.Id).ToList();
        var counts = await db.CountEntries.AsNoTracking().Where(e => ids.Contains(e.CountCycleId)).GroupBy(e => e.CountCycleId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return rows.Select(c => new CountCycleDto(c.Id, c.Number, c.Name, c.Scope.ToString(), c.WarehouseId,
            c.WarehouseId is { } w ? whs.GetValueOrDefault(w) : null, c.OrgUnitId, c.OrgUnitId is { } u ? units.GetValueOrDefault(u) : null,
            c.SiteId, c.SiteId is { } s ? sites.GetValueOrDefault(s) : null, c.PlannedDate, c.FreezeMovements, c.Status.ToString(),
            c.Committee.Select(m => m.EmployeeId).ToList(), c.Committee.Select(m => names.GetValueOrDefault(m.EmployeeId) ?? string.Empty).ToList(),
            counts.GetValueOrDefault(c.Id), c.AdjustmentDocumentId, c.ClosedAt, me is { } m2 && c.Committee.Any(x => x.EmployeeId == m2),
            HrCommon.B64(c.RowVersion))).ToList();
    }
}
