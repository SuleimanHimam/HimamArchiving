using Archiving.Domain.Core;

namespace Archiving.Domain.Hr;

// Pure HR calculations: no database, no clock, so every rule has a unit test with plain numbers.
// The figures they work on (weekend days, rates, brackets, percentages) are passed in from settings.

public static class WorkCalendar
{
    public static bool IsWorkingDay(DateOnly d, IReadOnlySet<DayOfWeek> weekend, IReadOnlySet<DateOnly> holidays) =>
        !weekend.Contains(d.DayOfWeek) && !holidays.Contains(d);

    /// <summary>Days in [from, to]. Working-days mode skips weekend days and holidays.</summary>
    public static int CountDays(DateOnly from, DateOnly to, bool workingOnly,
        IReadOnlySet<DayOfWeek> weekend, IReadOnlySet<DateOnly> holidays)
    {
        if (to < from) return 0;
        if (!workingOnly) return to.DayNumber - from.DayNumber + 1;
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWorkingDay(d, weekend, holidays)) n++;
        return n;
    }

    /// <summary>Hours between two times on one day, to two decimals. Zero or negative spans are invalid (0).</summary>
    public static decimal Hours(TimeOnly from, TimeOnly to) =>
        to <= from ? 0 : Math.Round((decimal)(to - from).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);

    public static IReadOnlySet<DayOfWeek> ParseWeekend(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out var d) ? d : (DayOfWeek?)null)
            .Where(d => d is not null).Select(d => d!.Value).ToHashSet();

    public static bool Overlaps(DateOnly aFrom, DateOnly aTo, DateOnly bFrom, DateOnly bTo) => aFrom <= bTo && bFrom <= aTo;
}

public sealed record ShiftWindow(DateTime StartLocal, DateTime EndLocal, int GraceMinutes);

public sealed record AttendanceFinding(AttendanceExceptionKind Kind, int Minutes);

public static class AttendanceClassifier
{
    /// <summary>Lower bound for counting a punch toward a shift, before its start.</summary>
    public static readonly TimeSpan EarlyWindow = TimeSpan.FromHours(4);
    /// <summary>Upper bound after its end.</summary>
    public static readonly TimeSpan LateWindow = TimeSpan.FromHours(6);

    public static ShiftWindow Window(DateOnly date, TimeOnly start, TimeOnly end, int grace)
    {
        var s = date.ToDateTime(start);
        var e = end <= start ? date.AddDays(1).ToDateTime(end) : date.ToDateTime(end);
        return new ShiftWindow(s, e, grace);
    }

    /// <summary>
    /// Findings for one scheduled shift from the local punch times. No punch → absent; one punch → missing
    /// punch; otherwise first punch after start + grace → late, last punch before end − grace → early leave.
    /// </summary>
    public static IReadOnlyList<AttendanceFinding> Classify(ShiftWindow w, IEnumerable<DateTime> punchesLocal)
    {
        var inWindow = punchesLocal.Where(p => p >= w.StartLocal - EarlyWindow && p <= w.EndLocal + LateWindow)
            .Order().ToList();
        var shiftMinutes = (int)(w.EndLocal - w.StartLocal).TotalMinutes;
        if (inWindow.Count == 0) return [new(AttendanceExceptionKind.Absent, shiftMinutes)];
        if (inWindow.Count == 1) return [new(AttendanceExceptionKind.MissingPunch, 0)];

        var found = new List<AttendanceFinding>();
        var late = (int)Math.Floor((inWindow[0] - w.StartLocal).TotalMinutes);
        if (late > w.GraceMinutes) found.Add(new(AttendanceExceptionKind.Late, late));
        var early = (int)Math.Floor((w.EndLocal - inWindow[^1]).TotalMinutes);
        if (early > w.GraceMinutes) found.Add(new(AttendanceExceptionKind.EarlyLeave, early));
        return found;
    }
}

/// <summary>Picks the dated value in force on a day; a category-specific row wins over an all-categories row.</summary>
public static class DatedRate
{
    public static T? Pick<T>(IEnumerable<T> rows, DateOnly on, EmployeeCategory category,
        Func<T, DateOnly> from, Func<T, EmployeeCategory?> cat) where T : class =>
        rows.Where(r => from(r) <= on && (cat(r) == category || cat(r) is null))
            .OrderByDescending(r => cat(r) is not null).ThenByDescending(from).FirstOrDefault();
}

public static class Money
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

public static class TaxCalculator
{
    /// <summary>Progressive monthly tax: each bracket's rate applies to the part of taxable pay inside it.</summary>
    public static decimal Monthly(decimal taxable, IEnumerable<(decimal From, decimal? To, decimal Rate)> brackets)
    {
        if (taxable <= 0) return 0;
        decimal tax = 0;
        foreach (var (from, to, rate) in brackets.OrderBy(b => b.From))
        {
            if (taxable <= from) break;
            var top = to is { } t ? Math.Min(taxable, t) : taxable;
            if (top > from) tax += (top - from) * rate / 100m;
        }
        return Money.Round(tax);
    }
}

/// <summary>One element as the calculator sees it, already resolved for one employee and month.</summary>
public sealed record PayLineInput(long ElementId, string Code, string Name, PayElementKind Kind, PayElementMethod Method,
    bool IsTaxable, bool IsPreTax, decimal Rate, decimal Quantity);

public sealed record PayLineResult(long ElementId, string Code, string Name, PayElementKind Kind, decimal Quantity,
    decimal Rate, decimal Amount);

public sealed record PayCalculation(IReadOnlyList<PayLineResult> Lines, decimal Gross, decimal Deductions, decimal Net);

public static class PayrollCalculator
{
    /// <summary>
    /// Basic first, then the other earnings and the non-tax deductions, then tax on
    /// (taxable earnings − pre-tax deductions). Every amount is rounded to the agora.
    /// </summary>
    public static PayCalculation Calculate(IReadOnlyList<PayLineInput> elements,
        IEnumerable<(decimal From, decimal? To, decimal Rate)> taxBrackets)
    {
        var lines = new List<PayLineResult>();
        var basic = elements.Where(e => e.Method == PayElementMethod.Basic).Sum(e => Money.Round(e.Rate));
        foreach (var e in elements.Where(e => e.Method == PayElementMethod.Basic))
            lines.Add(new(e.ElementId, e.Code, e.Name, e.Kind, 1, e.Rate, Money.Round(e.Rate)));

        foreach (var e in elements.Where(e => e.Method is not (PayElementMethod.Basic or PayElementMethod.Tax)))
        {
            var (qty, amount) = e.Method switch
            {
                PayElementMethod.Fixed => (1m, e.Rate),
                PayElementMethod.PercentOfBasic => (1m, basic * e.Rate / 100m),
                PayElementMethod.PerUnit => (e.Quantity, e.Rate * e.Quantity),
                _ => (0m, 0m),
            };
            amount = Money.Round(amount);
            if (amount != 0) lines.Add(new(e.ElementId, e.Code, e.Name, e.Kind, qty, e.Rate, amount));
        }

        var byId = elements.ToDictionary(e => e.ElementId);
        var taxable = lines.Where(l => l.Kind == PayElementKind.Earning && byId[l.ElementId].IsTaxable).Sum(l => l.Amount)
                    - lines.Where(l => l.Kind == PayElementKind.Deduction && byId[l.ElementId].IsPreTax).Sum(l => l.Amount);
        var brackets = taxBrackets.ToList();
        foreach (var e in elements.Where(e => e.Method == PayElementMethod.Tax))
        {
            var tax = TaxCalculator.Monthly(taxable, brackets);
            if (tax != 0) lines.Add(new(e.ElementId, e.Code, e.Name, PayElementKind.Deduction, 1, Money.Round(taxable), tax));
        }

        var gross = lines.Where(l => l.Kind == PayElementKind.Earning).Sum(l => l.Amount);
        var deductions = lines.Where(l => l.Kind == PayElementKind.Deduction).Sum(l => l.Amount);
        return new PayCalculation(lines, gross, deductions, gross - deductions);
    }
}

public static class DisbursementCalculator
{
    /// <summary>
    /// What is paid this month: the larger of the minimum and the salary share, plus a share of what was
    /// already owed, never more than what is owed in total (net + accumulated) and never negative.
    /// </summary>
    public static decimal Paid(decimal net, decimal accumulatedBefore, decimal percentOfSalary, decimal minimum,
        decimal percentOfAccumulated)
    {
        var owed = Math.Max(0, net) + Math.Max(0, accumulatedBefore);
        var fromSalary = Math.Max(minimum, Math.Max(0, net) * percentOfSalary / 100m);
        var fromAccumulated = Math.Max(0, accumulatedBefore) * percentOfAccumulated / 100m;
        return Money.Round(Math.Clamp(fromSalary + fromAccumulated, 0, owed));
    }
}
