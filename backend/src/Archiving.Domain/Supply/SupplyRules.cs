namespace Archiving.Domain.Supply;

// Pure supply rules: no database, so every one is unit-tested directly.

/// <summary>How much of an approved request can be issued now (§6.3: full, partial, or none → suspend).</summary>
public static class IssuePlanner
{
    public enum Outcome { Full, Partial, None }

    public sealed record Line(long ItemId, decimal Remaining);

    /// <summary>Takes from <paramref name="available"/> line by line; never more than is on hand.</summary>
    public static (IReadOnlyList<(long ItemId, decimal Quantity)> Issue, Outcome Outcome) Plan(
        IReadOnlyList<Line> lines, IReadOnlyDictionary<long, decimal> available)
    {
        var left = available.ToDictionary(x => x.Key, x => x.Value);
        var issue = new List<(long, decimal)>();
        decimal wanted = 0, given = 0;
        foreach (var l in lines.Where(l => l.Remaining > 0))
        {
            wanted += l.Remaining;
            var have = Math.Max(0, left.GetValueOrDefault(l.ItemId));
            var take = Math.Min(have, l.Remaining);
            if (take <= 0) continue;
            left[l.ItemId] = have - take;
            given += take;
            issue.Add((l.ItemId, take));
        }
        var outcome = given == 0 ? Outcome.None : given < wanted ? Outcome.Partial : Outcome.Full;
        return (issue, outcome);
    }
}

/// <summary>
/// Blind count comparison (§7.1). Runs on the server only; the committee entered counts without seeing
/// the expected figures.
/// </summary>
public static class CountReconciler
{
    public sealed record ExpectedAsset(long AssetId, string Barcode, long ItemId, AssetCondition Condition);
    public sealed record ScannedAsset(string Barcode, long? AssetId, long? ItemId, AssetCondition? Condition, bool Exists);

    public sealed record Variance(VarianceKind Kind, long? ItemId, long? WarehouseId, long? AssetId, string? Barcode,
        decimal Expected, decimal Counted, AssetCondition? CountedCondition);

    /// <param name="expectedQty">(item, warehouse) → recorded balance for quantity-tracked items in scope.</param>
    /// <param name="countedQty">(item, warehouse) → total the committee entered. Missing key = counted 0.</param>
    public static IReadOnlyList<Variance> Compare(
        IReadOnlyDictionary<(long Item, long Warehouse), decimal> expectedQty,
        IReadOnlyDictionary<(long Item, long Warehouse), decimal> countedQty,
        IReadOnlyList<ExpectedAsset> expectedAssets,
        IReadOnlyList<ScannedAsset> scanned)
    {
        var result = new List<Variance>();
        foreach (var key in expectedQty.Keys.Union(countedQty.Keys).OrderBy(k => k.Item).ThenBy(k => k.Warehouse))
        {
            var exp = expectedQty.GetValueOrDefault(key);
            var cnt = countedQty.GetValueOrDefault(key);
            if (cnt < exp) result.Add(new(VarianceKind.Shortage, key.Item, key.Warehouse, null, null, exp, cnt, null));
            else if (cnt > exp) result.Add(new(VarianceKind.Surplus, key.Item, key.Warehouse, null, null, exp, cnt, null));
        }

        // A barcode scanned twice counts once.
        var seen = scanned.GroupBy(s => s.Barcode, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        var byBarcode = seen.ToDictionary(s => s.Barcode, StringComparer.OrdinalIgnoreCase);
        foreach (var a in expectedAssets)
        {
            if (!byBarcode.TryGetValue(a.Barcode, out var s))
                result.Add(new(VarianceKind.Missing, a.ItemId, null, a.AssetId, a.Barcode, 1, 0, null));
            else if (s.Condition is { } c && c != a.Condition)
                result.Add(new(VarianceKind.ConditionChanged, a.ItemId, null, a.AssetId, a.Barcode, 1, 1, c));
        }
        var expectedCodes = expectedAssets.Select(a => a.Barcode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in seen.Where(s => !expectedCodes.Contains(s.Barcode)))
            result.Add(s.Exists
                ? new(VarianceKind.Misplaced, s.ItemId, null, s.AssetId, s.Barcode, 0, 1, s.Condition)
                : new(VarianceKind.Surplus, null, null, null, s.Barcode, 0, 1, s.Condition));
        return result;
    }
}

public static class ReplacementAdvisor
{
    /// <summary>§7.2: suggest replacement once accumulated maintenance cost passes a % of the asset's value.
    /// No percentage configured, or no value recorded → never suggest.</summary>
    public static bool Suggest(decimal accumulatedCost, decimal? assetValue, decimal? percent) =>
        percent is > 0 && assetValue is > 0 && accumulatedCost > assetValue.Value * percent.Value / 100m;
}

/// <summary>The opening-balance template (§5.3). CSV, UTF-8; Excel opens and saves it as is.</summary>
public static class OpeningTemplate
{
    public static readonly string[] Columns =
    [
        "item_code", "warehouse_code", "quantity", "serial_number", "barcode",
        "holder_employee_number", "holder_org_unit_code", "condition", "purchase_date", "value",
    ];

    public sealed record Row(int Line, string ItemCode, string? WarehouseCode, decimal Quantity, string? Serial, string? Barcode,
        string? HolderEmployeeNumber, string? HolderOrgUnitCode, AssetCondition Condition, DateOnly? PurchaseDate, decimal? Value);

    public sealed record Error(int Line, string Message);

    public static string Header => string.Join(',', Columns);

    /// <summary>Parses the file and checks what needs no database: shape, numbers, dates, duplicates.</summary>
    public static (IReadOnlyList<Row> Rows, IReadOnlyList<Error> Errors) Parse(string content)
    {
        var rows = new List<Row>();
        var errors = new List<Error>();
        var lines = SplitLines(content.TrimStart('﻿'));
        if (lines.Count == 0) return (rows, [new Error(0, "الملف فارغ")]);
        var header = SplitCsv(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var idx = Columns.ToDictionary(c => c, c => header.IndexOf(c));
        foreach (var missing in idx.Where(x => x.Value < 0).Select(x => x.Key))
            errors.Add(new Error(1, $"العمود «{missing}» غير موجود في الملف"));
        if (errors.Count > 0) return (rows, errors);

        var serials = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var barcodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Count; i++)
        {
            var n = i + 1;
            var cells = SplitCsv(lines[i]);
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            string? Cell(string col) { var j = idx[col]; var v = j < cells.Count ? cells[j].Trim() : ""; return v.Length == 0 ? null : v; }

            var item = Cell("item_code");
            if (item is null) { errors.Add(new Error(n, "رمز الصنف فارغ")); continue; }
            var qtyText = Cell("quantity") ?? "1";
            if (!decimal.TryParse(qtyText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            { errors.Add(new Error(n, "الكمية غير صالحة")); continue; }
            var condition = AssetCondition.Good;
            if (Cell("condition") is { } c && !TryCondition(c, out condition)) { errors.Add(new Error(n, "الحالة غير معروفة (جيد، يحتاج صيانة، تالف)")); continue; }
            DateOnly? purchase = null;
            if (Cell("purchase_date") is { } pd)
            {
                if (!DateOnly.TryParseExact(pd, ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy"], out var p)) { errors.Add(new Error(n, "تاريخ الشراء غير صالح")); continue; }
                purchase = p;
            }
            decimal? value = null;
            if (Cell("value") is { } vt)
            {
                if (!decimal.TryParse(vt, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) || v < 0)
                { errors.Add(new Error(n, "القيمة غير صالحة")); continue; }
                value = decimal.Round(v, 2, MidpointRounding.AwayFromZero);
            }
            var serial = Cell("serial_number");
            var barcode = Cell("barcode");
            if (serial is not null && !serials.TryAdd(serial, n)) { errors.Add(new Error(n, $"رقم تسلسلي مكرر ({serial}) مع السطر {serials[serial]}")); continue; }
            if (barcode is not null && !barcodes.TryAdd(barcode, n)) { errors.Add(new Error(n, $"باركود مكرر ({barcode}) مع السطر {barcodes[barcode]}")); continue; }
            rows.Add(new Row(n, item, Cell("warehouse_code"), qty, serial, barcode, Cell("holder_employee_number"),
                Cell("holder_org_unit_code"), condition, purchase, value));
        }
        if (rows.Count == 0 && errors.Count == 0) errors.Add(new Error(0, "لا توجد أسطر بيانات في الملف"));
        return (rows, errors);
    }

    public static bool TryCondition(string text, out AssetCondition c)
    {
        c = text.Trim() switch
        {
            "جيد" or "Good" or "good" or "0" => AssetCondition.Good,
            "يحتاج صيانة" or "NeedsMaintenance" or "needs_maintenance" or "1" => AssetCondition.NeedsMaintenance,
            "تالف" or "Damaged" or "damaged" or "2" => AssetCondition.Damaged,
            _ => (AssetCondition)(-1),
        };
        return (int)c >= 0;
    }

    private static List<string> SplitLines(string s) =>
        s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Length > 0).ToList();

    /// <summary>RFC 4180 field split for one line: quotes, doubled quotes, commas inside quotes.</summary>
    public static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var cur = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cur.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch is ',' or ';') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        cells.Add(cur.ToString());
        return cells;
    }
}

/// <summary>Header rules per document type, checked before saving and again before execution.</summary>
public static class DocumentRules
{
    public sealed record Header(DocumentType Type, long? WarehouseId, long? TargetWarehouseId, long? RecipientEmployeeId,
        long? RecipientOrgUnitId, string? Supplier, string Title);

    public static string? Validate(Header h)
    {
        if (string.IsNullOrWhiteSpace(h.Title)) return "البيان مطلوب";
        return h.Type switch
        {
            DocumentType.RCV when h.WarehouseId is null => "حدّد المستودع المستلم",
            DocumentType.RCV when string.IsNullOrWhiteSpace(h.Supplier) => "اسم المورد مطلوب",
            DocumentType.ISS when h.WarehouseId is null => "حدّد مستودع الصرف",
            DocumentType.ISS when h.RecipientEmployeeId is null && h.RecipientOrgUnitId is null => "حدّد المستلم: موظف أو دائرة",
            DocumentType.MOV when h.WarehouseId is null || h.TargetWarehouseId is null => "حدّد المستودع المرسل والمستلم",
            DocumentType.MOV when h.WarehouseId == h.TargetWarehouseId => "لا نقل إلى المستودع نفسه",
            DocumentType.TRF when h.RecipientEmployeeId is null && h.RecipientOrgUnitId is null => "حدّد صاحب العهدة الجديد",
            DocumentType.TRF when h.RecipientEmployeeId is not null && h.RecipientOrgUnitId is not null => "صاحب العهدة الجديد موظف أو دائرة، لا الاثنان",
            DocumentType.RET when h.WarehouseId is null => "حدّد المستودع المستلم للمرتجع",
            DocumentType.OPN or DocumentType.ADJ => null,
            _ => null,
        };
    }

    /// <summary>Types an ordinary user may create by hand. ADJ comes from a count, OPN from the import.</summary>
    public static bool IsManual(DocumentType t) => t is not (DocumentType.ADJ or DocumentType.OPN);
}
