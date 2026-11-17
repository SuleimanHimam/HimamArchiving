using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Core;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// Pay elements, their dated rates, the salary scale, employee-specific amounts and tax tables.
/// Dated rows are add-only: a correction is a newer row, so a past month always recalculates the same.
/// </summary>
public sealed class PayrollSettingsService(AppDbContext db, IAccessScope scope, IAuditWriter audit) : IPayrollSettingsService
{
    private const decimal MaxAmount = 10_000_000m;

    public async Task<IReadOnlyList<PayElementDto>> ElementsAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.PayElements.AsNoTracking();
        if (activeOnly) q = q.Where(e => e.IsActive);
        return (await q.OrderBy(e => e.SortOrder).ThenBy(e => e.Code).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<Result<PayElementDto>> CreateElementAsync(SavePayElementRequest r, CancellationToken ct = default)
    {
        var e = new PayElement();
        if (await ApplyAsync(e, r, null, ct) is { } error) return Result<PayElementDto>.Fail(error);
        db.PayElements.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PayElement", e.Id, e.Name, ct: ct);
        return Result<PayElementDto>.Ok(ToDto(e));
    }

    public async Task<Result<PayElementDto>> UpdateElementAsync(long id, SavePayElementRequest r, CancellationToken ct = default)
    {
        var e = await db.PayElements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<PayElementDto>.Fail("عنصر الراتب غير موجود");
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<PayElementDto>.Fail(CoreGuards.Stale);
        var before = CoreGuards.Json(ToDto(e));
        if (await ApplyAsync(e, r, id, ct) is { } error) return Result<PayElementDto>.Fail(error);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<PayElementDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "PayElement", e.Id, e.Name, before, CoreGuards.Json(ToDto(e)), ct);
        return Result<PayElementDto>.Ok(ToDto(e));
    }

    private async Task<string?> ApplyAsync(PayElement e, SavePayElementRequest r, long? id, CancellationToken ct)
    {
        var code = CoreGuards.Clean(r.Code).ToUpperInvariant();
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32) return "الرمز مطلوب ولا يتجاوز 32 حرفاً";
        if (name.Length is 0 or > 150) return "الاسم مطلوب ولا يتجاوز 150 حرفاً";
        if (!Enum.TryParse<PayElementKind>(r.Kind, true, out var kind) || !Enum.IsDefined(kind)) return "نوع العنصر غير صالح";
        if (!Enum.TryParse<PayElementMethod>(r.Method, true, out var method) || !Enum.IsDefined(method)) return "طريقة الاحتساب غير صالحة";
        if (!Enum.TryParse<PayElementSource>(r.Source, true, out var source) || !Enum.IsDefined(source)) return "مصدر الكمية غير صالح";
        if (method == PayElementMethod.Basic && kind != PayElementKind.Earning) return "الراتب الأساسي عنصر استحقاق";
        if (method == PayElementMethod.Tax && kind != PayElementKind.Deduction) return "الضريبة عنصر استقطاع";
        if (method != PayElementMethod.PerUnit && source != PayElementSource.Manual) return "مصدر الكمية للعناصر بالوحدة فقط";
        if (r.IsPreTax && kind != PayElementKind.Deduction) return "الخصم قبل الضريبة للاستقطاعات فقط";
        if (!r.AppliesToMilitary && !r.AppliesToCivil && !r.AppliesToContract) return "اختر فئة واحدة على الأقل";
        if (await db.PayElements.IgnoreQueryFilters().AnyAsync(x => x.Code == code && x.Id != (id ?? 0), ct)) return "الرمز مستخدم";
        if (method is PayElementMethod.Basic or PayElementMethod.Tax && r.IsActive
            && await db.PayElements.AnyAsync(x => x.Method == method && x.IsActive && x.Id != (id ?? 0), ct))
            return method == PayElementMethod.Basic ? "يوجد عنصر راتب أساسي مفعّل" : "يوجد عنصر ضريبة مفعّل";

        e.Code = code; e.Name = name; e.Kind = kind; e.Method = method; e.Source = source;
        e.IsTaxable = kind == PayElementKind.Earning && r.IsTaxable;
        e.IsPreTax = r.IsPreTax;
        e.AppliesToMilitary = r.AppliesToMilitary; e.AppliesToCivil = r.AppliesToCivil; e.AppliesToContract = r.AppliesToContract;
        e.SortOrder = r.SortOrder; e.IsActive = r.IsActive;
        return null;
    }

    private static PayElementDto ToDto(PayElement e) => new(e.Id, e.Code, e.Name, e.Kind.ToString(), e.Method.ToString(),
        e.Source.ToString(), e.IsTaxable, e.IsPreTax, e.AppliesToMilitary, e.AppliesToCivil, e.AppliesToContract, e.SortOrder,
        e.IsActive, HrCommon.B64(e.RowVersion));

    // ---- dated rates ----

    public async Task<IReadOnlyList<PayElementRateDto>> RatesAsync(long? payElementId, CancellationToken ct = default)
    {
        var q = db.PayElementRates.AsNoTracking();
        if (payElementId is { } id) q = q.Where(r => r.PayElementId == id);
        return (await q.OrderBy(r => r.PayElementId).ThenByDescending(r => r.EffectiveFrom).ToListAsync(ct))
            .Select(r => new PayElementRateDto(r.Id, r.PayElementId, r.Category?.ToString(), r.EffectiveFrom, r.Value)).ToList();
    }

    public async Task<Result<PayElementRateDto>> AddRateAsync(AddPayElementRateRequest r, CancellationToken ct = default)
    {
        var element = await db.PayElements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == r.PayElementId, ct);
        if (element is null) return Result<PayElementRateDto>.Fail("عنصر الراتب غير موجود");
        if (element.Method is PayElementMethod.Basic or PayElementMethod.Tax)
            return Result<PayElementRateDto>.Fail("الأساسي من سلم الرواتب والضريبة من جدول الشرائح");
        if (!TryCategory(r.Category, out var cat)) return Result<PayElementRateDto>.Fail("الفئة غير صالحة");
        if (r.EffectiveFrom == default) return Result<PayElementRateDto>.Fail("تاريخ السريان مطلوب");
        if (r.Value < 0 || r.Value > MaxAmount) return Result<PayElementRateDto>.Fail("القيمة خارج النطاق");
        if (element.Method == PayElementMethod.PercentOfBasic && r.Value > 100) return Result<PayElementRateDto>.Fail("النسبة لا تتجاوز 100");
        if (await db.PayElementRates.AnyAsync(x => x.PayElementId == r.PayElementId && x.Category == cat && x.EffectiveFrom == r.EffectiveFrom, ct))
            return Result<PayElementRateDto>.Fail("يوجد سعر بنفس التاريخ والفئة");
        var row = new PayElementRate { PayElementId = r.PayElementId, Category = cat, EffectiveFrom = r.EffectiveFrom, Value = r.Value };
        db.PayElementRates.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PayElementRate", row.Id, element.Name, newValues: $"{cat} {r.EffectiveFrom:yyyy-MM-dd} {r.Value}", ct: ct);
        return Result<PayElementRateDto>.Ok(new PayElementRateDto(row.Id, row.PayElementId, cat?.ToString(), row.EffectiveFrom, row.Value));
    }

    public async Task<IReadOnlyList<SalaryScaleRateDto>> ScaleAsync(CancellationToken ct = default)
    {
        var grades = await db.Lookups.AsNoTracking().Where(l => l.Category == LookupCategories.Grade).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        return (await db.SalaryScaleRates.AsNoTracking().OrderBy(s => s.GradeId).ThenByDescending(s => s.EffectiveFrom).ToListAsync(ct))
            .Select(s => new SalaryScaleRateDto(s.Id, s.GradeId, grades.GetValueOrDefault(s.GradeId) ?? string.Empty,
                s.Category?.ToString(), s.EffectiveFrom, s.BasicAmount)).ToList();
    }

    public async Task<Result<SalaryScaleRateDto>> AddScaleRateAsync(AddSalaryScaleRateRequest r, CancellationToken ct = default)
    {
        if (!await LookupService.IsValidAsync(db, r.GradeId, LookupCategories.Grade, ct)) return Result<SalaryScaleRateDto>.Fail("الدرجة غير صالحة");
        if (!TryCategory(r.Category, out var cat)) return Result<SalaryScaleRateDto>.Fail("الفئة غير صالحة");
        if (r.EffectiveFrom == default) return Result<SalaryScaleRateDto>.Fail("تاريخ السريان مطلوب");
        if (r.BasicAmount <= 0 || r.BasicAmount > MaxAmount) return Result<SalaryScaleRateDto>.Fail("المبلغ خارج النطاق");
        if (await db.SalaryScaleRates.AnyAsync(x => x.GradeId == r.GradeId && x.Category == cat && x.EffectiveFrom == r.EffectiveFrom, ct))
            return Result<SalaryScaleRateDto>.Fail("يوجد مبلغ بنفس الدرجة والتاريخ والفئة");
        var row = new SalaryScaleRate { GradeId = r.GradeId, Category = cat, EffectiveFrom = r.EffectiveFrom, BasicAmount = Money.Round(r.BasicAmount) };
        db.SalaryScaleRates.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "SalaryScaleRate", row.Id, $"grade {r.GradeId}", newValues: $"{cat} {r.EffectiveFrom:yyyy-MM-dd} {row.BasicAmount}", ct: ct);
        var name = await db.Lookups.Where(l => l.Id == r.GradeId).Select(l => l.Name).FirstAsync(ct);
        return Result<SalaryScaleRateDto>.Ok(new SalaryScaleRateDto(row.Id, row.GradeId, name, cat?.ToString(), row.EffectiveFrom, row.BasicAmount));
    }

    // ---- employee-specific amounts ----

    public async Task<Result<IReadOnlyList<EmployeePayElementDto>>> EmployeeElementsAsync(long employeeId, CancellationToken ct = default)
    {
        if (!await InScopeAsync(employeeId, ct)) return Result<IReadOnlyList<EmployeePayElementDto>>.Fail("الموظف غير موجود");
        var names = await db.PayElements.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        IReadOnlyList<EmployeePayElementDto> rows = (await db.EmployeePayElements.AsNoTracking()
                .Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.FromDate).ToListAsync(ct))
            .Select(x => new EmployeePayElementDto(x.Id, x.EmployeeId, x.PayElementId, names.GetValueOrDefault(x.PayElementId) ?? string.Empty,
                x.Amount, x.FromDate, x.ToDate)).ToList();
        return Result<IReadOnlyList<EmployeePayElementDto>>.Ok(rows);
    }

    public async Task<Result<EmployeePayElementDto>> AddEmployeeElementAsync(AddEmployePayElementRequest r, CancellationToken ct = default)
    {
        if (!await InScopeAsync(r.EmployeeId, ct)) return Result<EmployeePayElementDto>.Fail("الموظف غير موجود");
        var element = await db.PayElements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == r.PayElementId && e.IsActive, ct);
        if (element is null) return Result<EmployeePayElementDto>.Fail("عنصر الراتب غير موجود");
        if (element.Method is not (PayElementMethod.Basic or PayElementMethod.Fixed))
            return Result<EmployeePayElementDto>.Fail("المبلغ الخاص بالموظف للأساسي والعناصر الثابتة فقط");
        if (r.Amount < 0 || r.Amount > MaxAmount) return Result<EmployeePayElementDto>.Fail("المبلغ خارج النطاق");
        if (r.FromDate == default || r.ToDate < r.FromDate) return Result<EmployeePayElementDto>.Fail("التواريخ غير صالحة");
        var overlap = await db.EmployeePayElements.AnyAsync(x => x.EmployeeId == r.EmployeeId && x.PayElementId == r.PayElementId
            && (x.ToDate == null || x.ToDate >= r.FromDate) && (r.ToDate == null || x.FromDate <= r.ToDate), ct);
        if (overlap) return Result<EmployeePayElementDto>.Fail("يتداخل مع مبلغ قائم لنفس العنصر. أنهِ القائم أولاً");
        var row = new EmployeePayElement { EmployeeId = r.EmployeeId, PayElementId = r.PayElementId, Amount = Money.Round(r.Amount), FromDate = r.FromDate, ToDate = r.ToDate };
        db.EmployeePayElements.Add(row);
        await db.SaveChangesAsync(ct);
        // The amount itself is payroll data: audit says what changed, not the figure (standards §7.5).
        await audit.WriteAsync("Created", "EmployeePayElement", row.Id, element.Name, ct: ct);
        return Result<EmployeePayElementDto>.Ok(new EmployeePayElementDto(row.Id, row.EmployeeId, row.PayElementId, element.Name, row.Amount, row.FromDate, row.ToDate));
    }

    public async Task<Result<EmployeePayElementDto>> EndEmployeeElementAsync(long id, EndEmployeePayElementRequest r, CancellationToken ct = default)
    {
        var row = await db.EmployeePayElements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || !await InScopeAsync(row.EmployeeId, ct)) return Result<EmployeePayElementDto>.Fail("السجل غير موجود");
        if (r.ToDate < row.FromDate) return Result<EmployeePayElementDto>.Fail("تاريخ الانتهاء قبل البداية");
        row.ToDate = r.ToDate;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Ended", "EmployeePayElement", row.Id, $"{r.ToDate:yyyy-MM-dd}", ct: ct);
        var name = await db.PayElements.Where(e => e.Id == row.PayElementId).Select(e => e.Name).FirstAsync(ct);
        return Result<EmployeePayElementDto>.Ok(new EmployeePayElementDto(row.Id, row.EmployeeId, row.PayElementId, name, row.Amount, row.FromDate, row.ToDate));
    }

    // ---- tax ----

    public async Task<IReadOnlyList<TaxBracketDto>> TaxAsync(CancellationToken ct = default) =>
        (await db.TaxBrackets.AsNoTracking().OrderByDescending(t => t.EffectiveFrom).ThenBy(t => t.FromAmount).ToListAsync(ct))
        .Select(t => new TaxBracketDto(t.Id, t.EffectiveFrom, t.FromAmount, t.ToAmount, t.Rate)).ToList();

    public async Task<Result<IReadOnlyList<TaxBracketDto>>> SaveTaxTableAsync(SaveTaxTableRequest r, CancellationToken ct = default)
    {
        if (r.EffectiveFrom == default) return Result<IReadOnlyList<TaxBracketDto>>.Fail("تاريخ السريان مطلوب");
        if (r.Brackets is null || r.Brackets.Count is 0 or > 20) return Result<IReadOnlyList<TaxBracketDto>>.Fail("الشرائح بين 1 و20");
        var ordered = r.Brackets.OrderBy(b => b.FromAmount).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            if (b.FromAmount < 0 || b.Rate is < 0 or > 100 || b.ToAmount <= b.FromAmount)
                return Result<IReadOnlyList<TaxBracketDto>>.Fail($"الشريحة {i + 1} غير صالحة");
            if (i < ordered.Count - 1 && b.ToAmount != ordered[i + 1].FromAmount)
                return Result<IReadOnlyList<TaxBracketDto>>.Fail("الشرائح يجب أن تتصل دون فجوة أو تداخل");
        }
        if (await db.TaxBrackets.AnyAsync(t => t.EffectiveFrom == r.EffectiveFrom, ct))
            return Result<IReadOnlyList<TaxBracketDto>>.Fail("يوجد جدول بنفس تاريخ السريان. أضف جدولاً بتاريخ أحدث");
        foreach (var b in ordered)
            db.TaxBrackets.Add(new TaxBracket { EffectiveFrom = r.EffectiveFrom, FromAmount = b.FromAmount, ToAmount = b.ToAmount, Rate = b.Rate });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "TaxTable", 0, $"{r.EffectiveFrom:yyyy-MM-dd}", newValues: CoreGuards.Json(ordered), ct: ct);
        return Result<IReadOnlyList<TaxBracketDto>>.Ok(await TaxAsync(ct));
    }

    internal static bool TryCategory(string? value, out EmployeeCategory? category)
    {
        category = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Enum.TryParse<EmployeeCategory>(value, true, out var c) || !Enum.IsDefined(c)) return false;
        category = c;
        return true;
    }

    private async Task<bool> InScopeAsync(long employeeId, CancellationToken ct) =>
        await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).AnyAsync(e => e.Id == employeeId, ct);
}
