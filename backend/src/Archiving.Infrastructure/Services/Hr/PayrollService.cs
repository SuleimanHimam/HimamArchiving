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
/// The payroll month (presentation slides 16–18): eight stages in order, the person who calculates is not
/// the one who audits or approves, a difference report against the last month before approval, what is
/// earned is calculated in full and the disbursement decision sets what is paid, the rest accumulates as
/// the employee's entitlement balance. A closed month never changes (standards §8 rule 7).
/// </summary>
public sealed class PayrollService(AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit) : IPayrollService
{
    private const string NotFound = "شهر الرواتب غير موجود";
    internal const string ApprovePermission = "HrPayroll.Approve";

    // ---- periods ----

    public async Task<IReadOnlyList<PayrollPeriodDto>> PeriodsAsync(CancellationToken ct = default)
    {
        var periods = await db.PayrollPeriods.AsNoTracking().Include(p => p.Log)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).Take(60).ToListAsync(ct);
        var list = new List<PayrollPeriodDto>();
        foreach (var p in periods) list.Add(await ToDtoAsync(p, ct));
        return list;
    }

    public async Task<Result<PayrollPeriodDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.AsNoTracking().Include(x => x.Log).FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? Result<PayrollPeriodDto>.Fail(NotFound) : Result<PayrollPeriodDto>.Ok(await ToDtoAsync(p, ct));
    }

    public async Task<Result<PayrollPeriodDto>> OpenAsync(OpenPayrollRequest r, CancellationToken ct = default)
    {
        if (!ShiftService.ValidMonth(r.Year, r.Month)) return Result<PayrollPeriodDto>.Fail("الشهر غير صالح");
        if (await db.PayrollPeriods.AnyAsync(p => p.Year == r.Year && p.Month == r.Month, ct))
            return Result<PayrollPeriodDto>.Fail("الشهر مفتوح مسبقاً");
        var p = new PayrollPeriod { Year = r.Year, Month = r.Month, Stage = PayrollStage.Open };
        p.Log.Add(Log(PayrollStage.Open, false, null));
        db.PayrollPeriods.Add(p);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Opened", "PayrollPeriod", p.Id, $"{r.Year}-{r.Month:00}", ct: ct);
        return await GetAsync(p.Id, ct);
    }

    // ---- inputs ----

    public async Task<Result<IReadOnlyList<PayrollInputDto>>> InputsAsync(long periodId, CancellationToken ct = default)
    {
        if (!await db.PayrollPeriods.AnyAsync(p => p.Id == periodId, ct)) return Result<IReadOnlyList<PayrollInputDto>>.Fail(NotFound);
        var rows = await db.PayrollInputs.AsNoTracking().Where(i => i.PayrollPeriodId == periodId).OrderBy(i => i.Id).ToListAsync(ct);
        var names = await HrCommon.NamesAsync(db, rows.Select(i => i.EmployeeId), ct);
        var elements = await db.PayElements.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        IReadOnlyList<PayrollInputDto> list = rows.Select(i => new PayrollInputDto(i.Id, i.EmployeeId, names.GetValueOrDefault(i.EmployeeId) ?? string.Empty,
            i.PayElementId, elements.GetValueOrDefault(i.PayElementId) ?? string.Empty, i.Quantity, i.Note)).ToList();
        return Result<IReadOnlyList<PayrollInputDto>>.Ok(list);
    }

    public async Task<Result<PayrollInputDto>> AddInputAsync(long periodId, AddPayrollInputRequest r, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<PayrollInputDto>.Fail(NotFound);
        if (p.Stage is not (PayrollStage.Open or PayrollStage.Inputs)) return Result<PayrollInputDto>.Fail("المدخلات في مرحلة الفتح أو المدخلات فقط");
        var employee = await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(e => e.Id == r.EmployeeId, ct);
        if (employee is null) return Result<PayrollInputDto>.Fail("الموظف غير موجود");
        var element = await db.PayElements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == r.PayElementId && e.IsActive, ct);
        if (element is null || element.Method != PayElementMethod.PerUnit || element.Source != PayElementSource.Manual)
            return Result<PayrollInputDto>.Fail("المدخلات اليدوية لعناصر «بالوحدة» ذات المصدر اليدوي فقط");
        if (r.Quantity <= 0 || r.Quantity > 100_000) return Result<PayrollInputDto>.Fail("الكمية خارج النطاق");
        var note = CoreGuards.CleanOrNull(r.Note);
        if (note?.Length > 500) return Result<PayrollInputDto>.Fail("الملاحظة أطول من 500 حرف");
        var row = new PayrollInput { PayrollPeriodId = periodId, EmployeeId = employee.Id, PayElementId = element.Id, Quantity = r.Quantity, Note = note };
        db.PayrollInputs.Add(row);
        if (p.Stage == PayrollStage.Open) MoveTo(p, PayrollStage.Inputs, false, "أول مُدخل");
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PayrollInput", row.Id, $"{element.Name}: {employee.FullName}", ct: ct);
        return Result<PayrollInputDto>.Ok(new PayrollInputDto(row.Id, employee.Id, employee.FullName, element.Id, element.Name, row.Quantity, row.Note));
    }

    public async Task<Result<bool>> DeleteInputAsync(long periodId, long inputId, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<bool>.Fail(NotFound);
        if (p.Stage is not (PayrollStage.Open or PayrollStage.Inputs)) return Result<bool>.Fail("المدخلات في مرحلة الفتح أو المدخلات فقط");
        var row = await db.PayrollInputs.FirstOrDefaultAsync(i => i.Id == inputId && i.PayrollPeriodId == periodId, ct);
        if (row is null) return Result<bool>.Fail("المُدخل غير موجود");
        db.PayrollInputs.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "PayrollInput", row.Id, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- stages ----

    public async Task<Result<PayrollPeriodDto>> AdvanceAsync(long periodId, AdvancePayrollRequest r, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.Include(x => x.Log).FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<PayrollPeriodDto>.Fail(NotFound);
        var note = CoreGuards.CleanOrNull(r.Note);
        if (note?.Length > 1000) return Result<PayrollPeriodDto>.Fail("الملاحظة أطول من 1000 حرف");
        return await db.InTransactionAsync(() => AdvanceCoreAsync(p, note, ct), x => x.Succeeded, ct);
    }

    private async Task<Result<PayrollPeriodDto>> AdvanceCoreAsync(PayrollPeriod p, string? note, CancellationToken ct)
    {
        // Preparing the month needs Edit; auditing, approving and closing need Approve.
        var needed = p.Stage is PayrollStage.Reviewed or PayrollStage.Audited or PayrollStage.Disbursed
            ? ApprovePermission : "HrPayroll.Edit";
        if (!await scope.HasPermissionAsync(needed, ct)) return Result<PayrollPeriodDto>.Fail("ليست لديك صلاحية هذه المرحلة");

        switch (p.Stage)
        {
            case PayrollStage.Open:
                MoveTo(p, PayrollStage.Inputs, false, note);
                break;
            case PayrollStage.Inputs:
            {
                var calc = await CalculateCoreAsync(p, ct);
                if (calc.Employees == 0) return Result<PayrollPeriodDto>.Fail("لا يوجد موظفون للاحتساب");
                MoveTo(p, PayrollStage.Calculated, false, note);
                break;
            }
            case PayrollStage.Calculated:
                MoveTo(p, PayrollStage.Reviewed, false, note);
                break;
            case PayrollStage.Reviewed:
            case PayrollStage.Audited:
            {
                // Segregation of duties (plan §8 phase 3): whoever calculated does not audit or approve.
                var calculator = p.Log.Where(l => l.Stage == PayrollStage.Calculated && !l.IsReturn).OrderByDescending(l => l.At).FirstOrDefault()?.UserId;
                if (calculator is not null && calculator == user.UserId)
                    return Result<PayrollPeriodDto>.Fail("من احتسب الرواتب لا يدققها ولا يعتمدها");
                MoveTo(p, p.Stage == PayrollStage.Reviewed ? PayrollStage.Audited : PayrollStage.Approved, false, note);
                break;
            }
            case PayrollStage.Approved:
                return Result<PayrollPeriodDto>.Fail("المرحلة التالية تتم بتطبيق قرار الصرف");
            case PayrollStage.Disbursed:
                MoveTo(p, PayrollStage.Closed, false, note);
                break;
            default:
                return Result<PayrollPeriodDto>.Fail("الشهر مقفل ولا يُعدّل");
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Advanced", "PayrollPeriod", p.Id, $"{p.Year}-{p.Month:00} → {p.Stage}", newValues: note, ct: ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<Result<PayrollCalculationDto>> CalculateAsync(long periodId, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.Include(x => x.Log).FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<PayrollCalculationDto>.Fail(NotFound);
        if (p.Stage != PayrollStage.Calculated) return Result<PayrollCalculationDto>.Fail("إعادة الاحتساب في مرحلة الاحتساب فقط");
        return await db.InTransactionAsync(async () =>
        {
            var calc = await CalculateCoreAsync(p, ct);
            p.Log.Add(Log(PayrollStage.Calculated, false, "إعادة احتساب"));
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Recalculated", "PayrollPeriod", p.Id, $"{p.Year}-{p.Month:00}", ct: ct);
            return Result<PayrollCalculationDto>.Ok(calc with { Period = (await GetAsync(p.Id, ct)).Value! });
        }, x => x.Succeeded, ct);
    }

    public async Task<Result<PayrollPeriodDto>> ReturnAsync(long periodId, ReturnPayrollRequest r, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.Include(x => x.Log).FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<PayrollPeriodDto>.Fail(NotFound);
        var reason = CoreGuards.Clean(r.Reason);
        if (reason.Length is 0 or > 1000) return Result<PayrollPeriodDto>.Fail("سبب الإرجاع مطلوب");
        if (p.Stage is not (PayrollStage.Calculated or PayrollStage.Reviewed or PayrollStage.Audited))
            return Result<PayrollPeriodDto>.Fail("يُرجع الشهر من الاحتساب أو المراجعة أو التدقيق فقط");
        MoveTo(p, PayrollStage.Inputs, true, reason);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Returned", "PayrollPeriod", p.Id, $"{p.Year}-{p.Month:00}", newValues: reason, ct: ct);
        return await GetAsync(p.Id, ct);
    }

    /// <summary>
    /// Replaces the month's results: for each active employee, each element applying to the category,
    /// resolved on the month's last day. Missing basic salary is reported, not guessed.
    /// </summary>
    private async Task<PayrollCalculationDto> CalculateCoreAsync(PayrollPeriod p, CancellationToken ct)
    {
        var (first, last) = ShiftService.MonthRange(p.Year, p.Month);
        var warnings = new List<string>();
        var employees = await db.Employees.AsNoTracking().Where(e => e.Status == EmployeeStatus.Active).OrderBy(e => e.EmployeeNumber).ToListAsync(ct);
        var empIds = employees.Select(e => e.Id).ToList();
        var grades = await db.EmployeeProfiles.AsNoTracking().Where(x => empIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, x => x.GradeId, ct);
        var elements = await db.PayElements.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.SortOrder).ToListAsync(ct);
        var rates = await db.PayElementRates.AsNoTracking().Where(r => r.EffectiveFrom <= last).ToListAsync(ct);
        var scale = await db.SalaryScaleRates.AsNoTracking().Where(r => r.EffectiveFrom <= last).ToListAsync(ct);
        var own = await db.EmployeePayElements.AsNoTracking()
            .Where(x => empIds.Contains(x.EmployeeId) && x.FromDate <= last && (x.ToDate == null || x.ToDate >= last)).ToListAsync(ct);
        var inputs = await db.PayrollInputs.AsNoTracking().Where(i => i.PayrollPeriodId == p.Id).ToListAsync(ct);
        var taxDate = await db.TaxBrackets.AsNoTracking().Where(t => t.EffectiveFrom <= last).MaxAsync(t => (DateOnly?)t.EffectiveFrom, ct);
        var brackets = taxDate is { } td
            ? (await db.TaxBrackets.AsNoTracking().Where(t => t.EffectiveFrom == td).ToListAsync(ct)).Select(t => (t.FromAmount, t.ToAmount, t.Rate)).ToList()
            : [];
        var nightTypes = await db.ShiftTypes.AsNoTracking().Where(t => t.IsNight).Select(t => t.Id).ToListAsync(ct);
        var nights = await db.ShiftAssignments.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeId) && a.Date >= first && a.Date <= last && a.Kind == RosterCellKind.Shift
                && a.ShiftTypeId != null && nightTypes.Contains(a.ShiftTypeId.Value))
            .GroupBy(a => a.EmployeeId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var absence = await db.AttendanceExceptions.AsNoTracking()
            .Where(x => empIds.Contains(x.EmployeeId) && x.Date >= first && x.Date <= last
                && x.Status == AttendanceExceptionStatus.Resolved && x.Resolution == AttendanceResolution.DeductSalary)
            .GroupBy(x => x.EmployeeId).Select(g => new { g.Key, M = g.Sum(x => x.Minutes) }).ToDictionaryAsync(x => x.Key, x => x.M, ct);

        var old = await db.PayrollResults.Include(r => r.Lines).Where(r => r.PayrollPeriodId == p.Id).ToListAsync(ct);
        foreach (var r in old) { db.PayrollResultLines.RemoveRange(r.Lines); db.PayrollResults.Remove(r); }
        // Soft-delete the previous run first: the unique (period, employee) index ignores deleted rows only once saved.
        if (old.Count > 0) await db.SaveChangesAsync(ct);

        foreach (var e in employees)
        {
            var mine = elements.Where(x => x.AppliesTo(e.Category)).ToList();
            var lines = new List<PayLineInput>();
            foreach (var x in mine)
            {
                var ownAmount = own.FirstOrDefault(o => o.EmployeeId == e.Id && o.PayElementId == x.Id)?.Amount;
                var rate = DatedRate.Pick(rates.Where(r => r.PayElementId == x.Id), last, e.Category, r => r.EffectiveFrom, r => r.Category)?.Value;
                switch (x.Method)
                {
                    case PayElementMethod.Basic:
                    {
                        var basic = ownAmount ?? (grades.GetValueOrDefault(e.Id) is { } g
                            ? DatedRate.Pick(scale.Where(s => s.GradeId == g), last, e.Category, s => s.EffectiveFrom, s => s.Category)?.BasicAmount
                            : null);
                        if (basic is null) { warnings.Add($"{e.EmployeeNumber} {e.FullName}: لا راتب أساسي (درجة بلا سلم أو مبلغ خاص)"); continue; }
                        lines.Add(new(x.Id, x.Code, x.Name, x.Kind, x.Method, x.IsTaxable, x.IsPreTax, basic.Value, 1));
                        break;
                    }
                    case PayElementMethod.Fixed when (ownAmount ?? rate) is { } amount:
                        lines.Add(new(x.Id, x.Code, x.Name, x.Kind, x.Method, x.IsTaxable, x.IsPreTax, amount, 1));
                        break;
                    case PayElementMethod.PercentOfBasic when rate is { } pct:
                        lines.Add(new(x.Id, x.Code, x.Name, x.Kind, x.Method, x.IsTaxable, x.IsPreTax, pct, 1));
                        break;
                    case PayElementMethod.PerUnit when rate is { } unit:
                    {
                        var qty = x.Source switch
                        {
                            PayElementSource.NightShifts => nights.GetValueOrDefault(e.Id),
                            PayElementSource.AbsenceMinutes => absence.GetValueOrDefault(e.Id),
                            _ => inputs.Where(i => i.EmployeeId == e.Id && i.PayElementId == x.Id).Sum(i => i.Quantity),
                        };
                        if (qty != 0) lines.Add(new(x.Id, x.Code, x.Name, x.Kind, x.Method, x.IsTaxable, x.IsPreTax, unit, qty));
                        break;
                    }
                    case PayElementMethod.Tax:
                        lines.Add(new(x.Id, x.Code, x.Name, x.Kind, x.Method, x.IsTaxable, x.IsPreTax, 0, 1));
                        break;
                }
            }
            var calc = PayrollCalculator.Calculate(lines, brackets);
            var result = new PayrollResult
            {
                PayrollPeriodId = p.Id, EmployeeId = e.Id, Category = e.Category,
                Gross = calc.Gross, Deductions = calc.Deductions, Net = calc.Net,
            };
            foreach (var l in calc.Lines)
                result.Lines.Add(new PayrollResultLine
                {
                    PayElementId = l.ElementId, Code = l.Code, Name = l.Name, Kind = l.Kind, Quantity = l.Quantity, Rate = l.Rate, Amount = l.Amount,
                });
            db.PayrollResults.Add(result);
        }
        return new PayrollCalculationDto(employees.Count, warnings, null!);
    }

    // ---- results & payslips ----

    public async Task<Result<IReadOnlyList<PayrollResultDto>>> ResultsAsync(long periodId, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<IReadOnlyList<PayrollResultDto>>.Fail(NotFound);
        var ids = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Select(e => e.Id);
        var rows = await db.PayrollResults.AsNoTracking().Where(r => r.PayrollPeriodId == periodId && ids.Contains(r.EmployeeId)).ToListAsync(ct);
        var prev = await PreviousPeriodIdAsync(p, ct);
        var prevNet = prev is { } pid
            ? await db.PayrollResults.AsNoTracking().Where(r => r.PayrollPeriodId == pid).ToDictionaryAsync(r => r.EmployeeId, r => r.Net, ct)
            : [];
        var empIds = rows.Select(r => r.EmployeeId).ToList();
        var emps = await db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        IReadOnlyList<PayrollResultDto> list = rows.OrderBy(r => emps[r.EmployeeId].EmployeeNumber).Select(r =>
        {
            decimal? before = prevNet.TryGetValue(r.EmployeeId, out var n) ? n : null;
            return new PayrollResultDto(r.EmployeeId, emps[r.EmployeeId].EmployeeNumber, emps[r.EmployeeId].FullName, r.Category.ToString(),
                r.Gross, r.Deductions, r.Net, r.Paid, r.Paid is { } paid ? r.Net - paid : null, before, before is { } b ? r.Net - b : null);
        }).ToList();
        return Result<IReadOnlyList<PayrollResultDto>>.Ok(list);
    }

    public async Task<Result<PayslipDto>> PayslipAsync(long periodId, long employeeId, CancellationToken ct = default)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me?.Id != employeeId && !await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).AnyAsync(e => e.Id == employeeId, ct))
            return Result<PayslipDto>.Fail("القسيمة غير موجودة");
        var slip = await BuildPayslipAsync(periodId, employeeId, ct);
        if (slip is null) return Result<PayslipDto>.Fail("القسيمة غير موجودة");
        // Opening a payslip is viewing salary data: audited (standards §5.3).
        await audit.WriteAsync("Viewed", "Payslip", employeeId, $"{slip.Year}-{slip.Month:00}", ct: ct);
        return Result<PayslipDto>.Ok(slip);
    }

    private async Task<PayslipDto?> BuildPayslipAsync(long periodId, long employeeId, CancellationToken ct)
    {
        var p = await db.PayrollPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId, ct);
        var r = await db.PayrollResults.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.PayrollPeriodId == periodId && x.EmployeeId == employeeId, ct);
        if (p is null || r is null) return null;
        var e = await db.Employees.AsNoTracking().FirstAsync(x => x.Id == employeeId, ct);
        var balance = await db.EntitlementMovements.AsNoTracking().Where(m => m.EmployeeId == employeeId).SumAsync(m => m.Amount, ct);
        return new PayslipDto(p.Id, p.Year, p.Month, p.Stage.ToString(), e.Id, e.EmployeeNumber, e.FullName, r.Category.ToString(),
            r.Lines.OrderBy(l => l.Kind).ThenBy(l => l.Id).Select(l => new PayslipLineDto(l.Code, l.Name, l.Kind.ToString(), l.Quantity, l.Rate, l.Amount)).ToList(),
            r.Gross, r.Deductions, r.Net, r.Paid, r.Paid is { } paid ? r.Net - paid : null, balance);
    }

    public async Task<IReadOnlyList<PayslipDto>> MyPayslipsAsync(CancellationToken ct = default)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me is null) return [];
        var periods = await db.PayrollPeriods.AsNoTracking()
            .Where(p => p.Stage >= PayrollStage.Disbursed && db.PayrollResults.Any(r => r.PayrollPeriodId == p.Id && r.EmployeeId == me.Id))
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).Take(24).Select(p => p.Id).ToListAsync(ct);
        var list = new List<PayslipDto>();
        foreach (var id in periods) if (await BuildPayslipAsync(id, me.Id, ct) is { } s) list.Add(s);
        return list;
    }

    // ---- disbursement decision ----

    public async Task<Result<IReadOnlyList<DisbursementRuleDto>>> DisbursementRulesAsync(long periodId, CancellationToken ct = default)
    {
        if (!await db.PayrollPeriods.AnyAsync(p => p.Id == periodId, ct)) return Result<IReadOnlyList<DisbursementRuleDto>>.Fail(NotFound);
        IReadOnlyList<DisbursementRuleDto> rules = (await db.DisbursementRules.AsNoTracking().Where(r => r.PayrollPeriodId == periodId).ToListAsync(ct))
            .Select(r => new DisbursementRuleDto(r.Category.ToString(), r.PercentOfSalary, r.MinimumAmount, r.PercentOfAccumulated)).ToList();
        return Result<IReadOnlyList<DisbursementRuleDto>>.Ok(rules);
    }

    public async Task<Result<DisbursementSimulationDto>> SimulateAsync(long periodId, DisbursementRequest r, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<DisbursementSimulationDto>.Fail(NotFound);
        if (p.Stage < PayrollStage.Calculated) return Result<DisbursementSimulationDto>.Fail("احسب الرواتب أولاً");
        var (rules, error) = ParseRules(r);
        if (rules is null) return Result<DisbursementSimulationDto>.Fail(error!);
        return Result<DisbursementSimulationDto>.Ok(await SimulateCoreAsync(periodId, rules, r.AvailableLiquidity, ct));
    }

    public async Task<Result<PayrollPeriodDto>> ApplyDisbursementAsync(long periodId, DisbursementRequest r, CancellationToken ct = default)
    {
        var p = await db.PayrollPeriods.Include(x => x.Log).FirstOrDefaultAsync(x => x.Id == periodId, ct);
        if (p is null) return Result<PayrollPeriodDto>.Fail(NotFound);
        if (p.Stage != PayrollStage.Approved) return Result<PayrollPeriodDto>.Fail("قرار الصرف بعد اعتماد الرواتب");
        var (rules, error) = ParseRules(r);
        if (rules is null) return Result<PayrollPeriodDto>.Fail(error!);
        var sim = await SimulateCoreAsync(periodId, rules, r.AvailableLiquidity, ct);
        if (!sim.WithinLiquidity) return Result<PayrollPeriodDto>.Fail("إجمالي الصرف يتجاوز السيولة المتوفرة");

        return await db.InTransactionAsync(async () =>
        {
            var old = await db.DisbursementRules.Where(x => x.PayrollPeriodId == periodId).ToListAsync(ct);
            db.DisbursementRules.RemoveRange(old);
            if (old.Count > 0) await db.SaveChangesAsync(ct);
            foreach (var (cat, rule) in rules)
                db.DisbursementRules.Add(new DisbursementRule
                {
                    PayrollPeriodId = periodId, Category = cat, PercentOfSalary = rule.PercentOfSalary,
                    MinimumAmount = rule.MinimumAmount, PercentOfAccumulated = rule.PercentOfAccumulated,
                });
            var results = await db.PayrollResults.Where(x => x.PayrollPeriodId == periodId).ToDictionaryAsync(x => x.EmployeeId, ct);
            foreach (var line in sim.Employees)
            {
                results[line.EmployeeId].Paid = line.Paid;
                db.EntitlementMovements.Add(new EntitlementMovement { EmployeeId = line.EmployeeId, PayrollPeriodId = periodId, Kind = EntitlementMovementKind.Accrued, Amount = line.Net, Note = $"{p.Year}-{p.Month:00}" });
                db.EntitlementMovements.Add(new EntitlementMovement { EmployeeId = line.EmployeeId, PayrollPeriodId = periodId, Kind = EntitlementMovementKind.Paid, Amount = -line.Paid, Note = $"{p.Year}-{p.Month:00}" });
            }
            MoveTo(p, PayrollStage.Disbursed, false, r.AvailableLiquidity is { } liq ? $"السيولة {liq:N2}" : null);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Disbursed", "PayrollPeriod", p.Id, $"{p.Year}-{p.Month:00}",
                newValues: CoreGuards.Json(sim.Categories), ct: ct);
            return await GetAsync(p.Id, ct);
        }, x => x.Succeeded, ct);
    }

    private static (Dictionary<EmployeeCategory, DisbursementRuleDto>?, string?) ParseRules(DisbursementRequest r)
    {
        if (r.Rules is null || r.Rules.Count == 0) return (null, "قواعد القرار مطلوبة لكل فئة");
        if (r.AvailableLiquidity is < 0) return (null, "السيولة لا تكون سالبة");
        var map = new Dictionary<EmployeeCategory, DisbursementRuleDto>();
        foreach (var x in r.Rules)
        {
            if (!Enum.TryParse<EmployeeCategory>(x.Category, true, out var c) || !Enum.IsDefined(c)) return (null, "فئة غير صالحة");
            if (x.PercentOfSalary is < 0 or > 100 || x.PercentOfAccumulated is < 0 or > 100 || x.MinimumAmount < 0)
                return (null, "النسب بين 0 و100 والحد الأدنى غير سالب");
            if (!map.TryAdd(c, x)) return (null, "الفئة مكررة");
        }
        return (map, null);
    }

    private async Task<DisbursementSimulationDto> SimulateCoreAsync(long periodId, Dictionary<EmployeeCategory, DisbursementRuleDto> rules,
        decimal? liquidity, CancellationToken ct)
    {
        var results = await db.PayrollResults.AsNoTracking().Where(x => x.PayrollPeriodId == periodId).ToListAsync(ct);
        var ids = results.Select(x => x.EmployeeId).ToList();
        var acc = await db.EntitlementMovements.AsNoTracking()
            .Where(m => ids.Contains(m.EmployeeId) && m.PayrollPeriodId != periodId)
            .GroupBy(m => m.EmployeeId).Select(g => new { g.Key, Sum = g.Sum(m => m.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var names = await HrCommon.NamesAsync(db, ids, ct);
        var lines = results.Select(x =>
        {
            var before = acc.GetValueOrDefault(x.EmployeeId);
            // A category with no rule this month is paid nothing; everything carries.
            var paid = rules.TryGetValue(x.Category, out var rule)
                ? DisbursementCalculator.Paid(x.Net, before, rule.PercentOfSalary, rule.MinimumAmount, rule.PercentOfAccumulated)
                : 0;
            return new DisbursementEmployeeLine(x.EmployeeId, names.GetValueOrDefault(x.EmployeeId) ?? string.Empty, x.Category.ToString(),
                x.Net, before, paid, before + x.Net - paid);
        }).ToList();
        var cats = lines.GroupBy(l => l.Category).Select(g => new DisbursementCategoryTotal(g.Key, g.Count(), g.Sum(l => l.Net),
            g.Sum(l => l.Paid), g.Sum(l => l.Net - l.Paid))).ToList();
        var totalPaid = lines.Sum(l => l.Paid);
        return new DisbursementSimulationDto(cats, totalPaid, lines.Sum(l => l.Net - l.Paid), liquidity,
            liquidity is null || totalPaid <= liquidity, lines);
    }

    public async Task<Result<EntitlementStatementDto>> EntitlementsAsync(long? employeeId, CancellationToken ct = default)
    {
        long id;
        if (employeeId is null)
        {
            var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
            if (me is null) return Result<EntitlementStatementDto>.Fail("حسابك غير مرتبط بسجل موظف");
            id = me.Id;
        }
        else
        {
            if (!await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).AnyAsync(e => e.Id == employeeId, ct))
                return Result<EntitlementStatementDto>.Fail("الموظف غير موجود");
            id = employeeId.Value;
        }
        var periods = await db.PayrollPeriods.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.Year, p.Month), ct);
        var rows = await db.EntitlementMovements.AsNoTracking().Where(m => m.EmployeeId == id).OrderBy(m => m.Id).ToListAsync(ct);
        var list = rows.Select(m =>
        {
            var ym = m.PayrollPeriodId is { } pid && periods.TryGetValue(pid, out var v) ? v : ((int, int)?)null;
            return new EntitlementMovementDto(m.Id, ym?.Item1, ym?.Item2, m.Kind.ToString(), m.Amount, m.Note, m.CreatedAt);
        }).ToList();
        return Result<EntitlementStatementDto>.Ok(new EntitlementStatementDto(id, rows.Sum(m => m.Amount), list));
    }

    // ---- helpers ----

    private PayrollStageLog Log(PayrollStage stage, bool isReturn, string? note) =>
        new() { Stage = stage, UserId = user.UserId, At = DateTime.UtcNow, IsReturn = isReturn, Note = note };

    private void MoveTo(PayrollPeriod p, PayrollStage stage, bool isReturn, string? note)
    {
        p.Stage = stage;
        p.Log.Add(Log(stage, isReturn, note));
    }

    private async Task<long?> PreviousPeriodIdAsync(PayrollPeriod p, CancellationToken ct) =>
        await db.PayrollPeriods.AsNoTracking()
            .Where(x => (x.Year < p.Year || (x.Year == p.Year && x.Month < p.Month)) && db.PayrollResults.Any(r => r.PayrollPeriodId == x.Id))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);

    private async Task<PayrollPeriodDto> ToDtoAsync(PayrollPeriod p, CancellationToken ct)
    {
        var totals = await db.PayrollResults.AsNoTracking().Where(r => r.PayrollPeriodId == p.Id)
            .GroupBy(r => 1).Select(g => new { N = g.Count(), G = g.Sum(r => r.Gross), D = g.Sum(r => r.Deductions), Net = g.Sum(r => r.Net), P = g.Sum(r => r.Paid) })
            .FirstOrDefaultAsync(ct);
        var userIds = p.Log.Where(l => l.UserId is not null).Select(l => l.UserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return new PayrollPeriodDto(p.Id, p.Year, p.Month, p.Stage.ToString(), totals?.N ?? 0, totals?.G ?? 0, totals?.D ?? 0,
            totals?.Net ?? 0, p.Stage >= PayrollStage.Disbursed ? totals?.P : null,
            p.Log.OrderBy(l => l.At).ThenBy(l => l.Id).Select(l => new PayrollStageLogDto(l.Stage.ToString(),
                l.UserId is { } u ? users.GetValueOrDefault(u) : null, l.At, l.IsReturn, l.Note)).ToList(),
            HrCommon.B64(p.RowVersion));
    }
}
