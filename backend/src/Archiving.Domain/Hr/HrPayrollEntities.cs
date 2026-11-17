using Archiving.Domain.Core;

namespace Archiving.Domain.Hr;

// Phase 3: payroll. The engine is code; every rate, percentage and bracket is dated data entered by the
// payroll officer, so the Authority's approved figures go in from the settings screen, not a release.

public enum PayElementKind { Earning = 0, Deduction = 1 }

public enum PayElementMethod
{
    Basic = 0,           // the grade's scale amount, or the employee's own amount (contracts)
    Fixed = 1,           // the dated rate, or the employee's own amount
    PercentOfBasic = 2,  // rate % × basic
    PerUnit = 3,         // rate × quantity (night shifts, overtime hours, absence minutes, manual)
    Tax = 4,             // monthly brackets on taxable earnings minus pre-tax deductions
}

/// <summary>Where a <see cref="PayElementMethod.PerUnit"/> element takes its quantity from.</summary>
public enum PayElementSource
{
    Manual = 0,            // payroll input entered in the "inputs" stage
    NightShifts = 1,       // published roster cells on night shift types in the month
    AbsenceMinutes = 2,    // attendance exceptions resolved as "deduct from salary" in the month
}

public class PayElement : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayElementKind Kind { get; set; }
    public PayElementMethod Method { get; set; }
    public PayElementSource Source { get; set; }
    public bool IsTaxable { get; set; }
    /// <summary>Deductions only: taken off the taxable amount before tax (e.g. a pension contribution).</summary>
    public bool IsPreTax { get; set; }
    public bool AppliesToMilitary { get; set; } = true;
    public bool AppliesToCivil { get; set; } = true;
    public bool AppliesToContract { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AppliesTo(EmployeeCategory c) => c switch
    {
        EmployeeCategory.Military => AppliesToMilitary,
        EmployeeCategory.Civil => AppliesToCivil,
        EmployeeCategory.Contract => AppliesToContract,
        _ => false,
    };
}

/// <summary>A dated rate of an element (amount, percent or unit price). Category null = every category.</summary>
public class PayElementRate : CoreEntity
{
    public long PayElementId { get; set; }
    public EmployeeCategory? Category { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Value { get; set; }
}

/// <summary>Salary scale: the basic amount of a grade from a date. Category null = every category.</summary>
public class SalaryScaleRate : CoreEntity
{
    public long GradeId { get; set; }               // lookup grade
    public EmployeeCategory? Category { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal BasicAmount { get; set; }
}

/// <summary>An element amount specific to one employee (a contract salary, a fixed allowance), with dates.</summary>
public class EmployeePayElement : CoreEntity
{
    public long EmployeeId { get; set; }
    public long PayElementId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

/// <summary>A monthly tax bracket from a date. Rate is a percent of the part of taxable pay inside the bracket.</summary>
public class TaxBracket : CoreEntity
{
    public DateOnly EffectiveFrom { get; set; }
    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>The eight stages of a payroll month (presentation slide 16), in order.</summary>
public enum PayrollStage
{
    Open = 0,
    Inputs = 1,
    Calculated = 2,
    Reviewed = 3,
    Audited = 4,
    Approved = 5,
    Disbursed = 6,
    Closed = 7,
}

public class PayrollPeriod : CoreEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollStage Stage { get; set; } = PayrollStage.Open;

    public ICollection<PayrollStageLog> Log { get; set; } = new List<PayrollStageLog>();
}

public class PayrollStageLog : CoreEntity
{
    public long PayrollPeriodId { get; set; }
    public PayrollStage Stage { get; set; }
    public long? UserId { get; set; }
    public DateTime At { get; set; }
    /// <summary>True when the period was sent back to this stage.</summary>
    public bool IsReturn { get; set; }
    public string? Note { get; set; }
}

public class PayrollInput : CoreEntity
{
    public long PayrollPeriodId { get; set; }
    public long EmployeeId { get; set; }
    public long PayElementId { get; set; }
    public decimal Quantity { get; set; }
    public string? Note { get; set; }
}

public class PayrollResult : CoreEntity
{
    public long PayrollPeriodId { get; set; }
    public long EmployeeId { get; set; }
    public EmployeeCategory Category { get; set; }
    public decimal Gross { get; set; }
    public decimal Deductions { get; set; }
    public decimal Net { get; set; }
    /// <summary>Set by the disbursement decision.</summary>
    public decimal? Paid { get; set; }

    public ICollection<PayrollResultLine> Lines { get; set; } = new List<PayrollResultLine>();
}

public class PayrollResultLine : CoreEntity
{
    public long PayrollResultId { get; set; }
    public long PayElementId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayElementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>قرار الصرف: one rule per category for a payroll month. Percentages are 0–100.</summary>
public class DisbursementRule : CoreEntity
{
    public long PayrollPeriodId { get; set; }
    public EmployeeCategory Category { get; set; }
    public decimal PercentOfSalary { get; set; }
    public decimal MinimumAmount { get; set; }
    public decimal PercentOfAccumulated { get; set; }
}

public enum EntitlementMovementKind { Accrued = 0, Paid = 1, Imported = 2 }

/// <summary>The employee's unpaid entitlement balance = sum of these movements (standards §8.1).</summary>
public class EntitlementMovement : CoreEntity
{
    public long EmployeeId { get; set; }
    public long? PayrollPeriodId { get; set; }
    public EntitlementMovementKind Kind { get; set; }
    public decimal Amount { get; set; }             // signed: accrued +, paid −
    public string? Note { get; set; }
}
