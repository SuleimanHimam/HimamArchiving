using Archiving.Application.Common.Models;

namespace Archiving.Application.Features.Hr;

// HR phase 3: payroll settings, the payroll month, disbursement decision, payslips.
// Category null on a dated row = every category.

public sealed record PayElementDto(long Id, string Code, string Name, string Kind, string Method, string Source, bool IsTaxable,
    bool IsPreTax, bool AppliesToMilitary, bool AppliesToCivil, bool AppliesToContract, int SortOrder, bool IsActive, string RowVersion);

public sealed record SavePayElementRequest(string Code, string Name, string Kind, string Method, string Source, bool IsTaxable,
    bool IsPreTax, bool AppliesToMilitary, bool AppliesToCivil, bool AppliesToContract, int SortOrder, bool IsActive = true,
    string? RowVersion = null);

public sealed record PayElementRateDto(long Id, long PayElementId, string? Category, DateOnly EffectiveFrom, decimal Value);
public sealed record AddPayElementRateRequest(long PayElementId, string? Category, DateOnly EffectiveFrom, decimal Value);

public sealed record SalaryScaleRateDto(long Id, long GradeId, string GradeName, string? Category, DateOnly EffectiveFrom, decimal BasicAmount);
public sealed record AddSalaryScaleRateRequest(long GradeId, string? Category, DateOnly EffectiveFrom, decimal BasicAmount);

public sealed record EmployeePayElementDto(long Id, long EmployeeId, long PayElementId, string PayElementName, decimal Amount,
    DateOnly FromDate, DateOnly? ToDate);
public sealed record AddEmployePayElementRequest(long EmployeeId, long PayElementId, decimal Amount, DateOnly FromDate, DateOnly? ToDate);
public sealed record EndEmployeePayElementRequest(DateOnly ToDate);

public sealed record TaxBracketDto(long Id, DateOnly EffectiveFrom, decimal FromAmount, decimal? ToAmount, decimal Rate);
public sealed record TaxBracketLine(decimal FromAmount, decimal? ToAmount, decimal Rate);
/// <summary>A full bracket table from a date. Earlier tables stay for earlier months.</summary>
public sealed record SaveTaxTableRequest(DateOnly EffectiveFrom, IReadOnlyList<TaxBracketLine> Brackets);

public interface IPayrollSettingsService
{
    Task<IReadOnlyList<PayElementDto>> ElementsAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<PayElementDto>> CreateElementAsync(SavePayElementRequest request, CancellationToken ct = default);
    Task<Result<PayElementDto>> UpdateElementAsync(long id, SavePayElementRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PayElementRateDto>> RatesAsync(long? payElementId, CancellationToken ct = default);
    Task<Result<PayElementRateDto>> AddRateAsync(AddPayElementRateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SalaryScaleRateDto>> ScaleAsync(CancellationToken ct = default);
    Task<Result<SalaryScaleRateDto>> AddScaleRateAsync(AddSalaryScaleRateRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmployeePayElementDto>>> EmployeeElementsAsync(long employeeId, CancellationToken ct = default);
    Task<Result<EmployeePayElementDto>> AddEmployeeElementAsync(AddEmployePayElementRequest request, CancellationToken ct = default);
    Task<Result<EmployeePayElementDto>> EndEmployeeElementAsync(long id, EndEmployeePayElementRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaxBracketDto>> TaxAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<TaxBracketDto>>> SaveTaxTableAsync(SaveTaxTableRequest request, CancellationToken ct = default);
}

public sealed record PayrollStageLogDto(string Stage, string? UserName, DateTime At, bool IsReturn, string? Note);

public sealed record PayrollPeriodDto(long Id, int Year, int Month, string Stage, int Employees, decimal Gross,
    decimal Deductions, decimal Net, decimal? Paid, IReadOnlyList<PayrollStageLogDto> Log, string RowVersion);

public sealed record OpenPayrollRequest(int Year, int Month);
public sealed record AdvancePayrollRequest(string? Note);
public sealed record ReturnPayrollRequest(string Reason);

public sealed record PayrollInputDto(long Id, long EmployeeId, string EmployeeName, long PayElementId, string PayElementName,
    decimal Quantity, string? Note);
public sealed record AddPayrollInputRequest(long EmployeeId, long PayElementId, decimal Quantity, string? Note);

public sealed record PayrollResultDto(long EmployeeId, string EmployeeNumber, string EmployeeName, string Category,
    decimal Gross, decimal Deductions, decimal Net, decimal? Paid, decimal? Carried, decimal? PreviousNet, decimal? Difference);

public sealed record PayrollCalculationDto(int Employees, IReadOnlyList<string> Warnings, PayrollPeriodDto Period);

public sealed record PayslipLineDto(string Code, string Name, string Kind, decimal Quantity, decimal Rate, decimal Amount);
public sealed record PayslipDto(long PeriodId, int Year, int Month, string Stage, long EmployeeId, string EmployeeNumber,
    string EmployeeName, string Category, IReadOnlyList<PayslipLineDto> Lines, decimal Gross, decimal Deductions, decimal Net,
    decimal? Paid, decimal? Carried, decimal EntitlementBalance);

public sealed record DisbursementRuleDto(string Category, decimal PercentOfSalary, decimal MinimumAmount, decimal PercentOfAccumulated);
public sealed record DisbursementRequest(IReadOnlyList<DisbursementRuleDto> Rules, decimal? AvailableLiquidity);
public sealed record DisbursementCategoryTotal(string Category, int Employees, decimal Net, decimal Paid, decimal Carried);
public sealed record DisbursementEmployeeLine(long EmployeeId, string EmployeeName, string Category, decimal Net,
    decimal AccumulatedBefore, decimal Paid, decimal BalanceAfter);
public sealed record DisbursementSimulationDto(IReadOnlyList<DisbursementCategoryTotal> Categories, decimal TotalPaid,
    decimal TotalCarried, decimal? AvailableLiquidity, bool WithinLiquidity, IReadOnlyList<DisbursementEmployeeLine> Employees);

public sealed record EntitlementMovementDto(long Id, int? Year, int? Month, string Kind, decimal Amount, string? Note, DateTime CreatedAt);
public sealed record EntitlementStatementDto(long EmployeeId, decimal Balance, IReadOnlyList<EntitlementMovementDto> Movements);

public interface IPayrollService
{
    Task<IReadOnlyList<PayrollPeriodDto>> PeriodsAsync(CancellationToken ct = default);
    Task<Result<PayrollPeriodDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<PayrollPeriodDto>> OpenAsync(OpenPayrollRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PayrollInputDto>>> InputsAsync(long periodId, CancellationToken ct = default);
    Task<Result<PayrollInputDto>> AddInputAsync(long periodId, AddPayrollInputRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteInputAsync(long periodId, long inputId, CancellationToken ct = default);

    /// <summary>Moves to the next stage. Entering "Calculated" runs the calculation; "Disbursed" goes
    /// through <see cref="ApplyDisbursementAsync"/>. Auditor and approver must differ from the calculator.</summary>
    Task<Result<PayrollPeriodDto>> AdvanceAsync(long periodId, AdvancePayrollRequest request, CancellationToken ct = default);
    /// <summary>Recalculates while the month is in the calculated stage.</summary>
    Task<Result<PayrollCalculationDto>> CalculateAsync(long periodId, CancellationToken ct = default);
    /// <summary>Sends a calculated/reviewed/audited month back to inputs with a reason.</summary>
    Task<Result<PayrollPeriodDto>> ReturnAsync(long periodId, ReturnPayrollRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PayrollResultDto>>> ResultsAsync(long periodId, CancellationToken ct = default);
    Task<Result<PayslipDto>> PayslipAsync(long periodId, long employeeId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DisbursementRuleDto>>> DisbursementRulesAsync(long periodId, CancellationToken ct = default);
    Task<Result<DisbursementSimulationDto>> SimulateAsync(long periodId, DisbursementRequest request, CancellationToken ct = default);
    /// <summary>Saves the rules, sets what each employee is paid, books the entitlement movements, stage → Disbursed.</summary>
    Task<Result<PayrollPeriodDto>> ApplyDisbursementAsync(long periodId, DisbursementRequest request, CancellationToken ct = default);

    Task<Result<EntitlementStatementDto>> EntitlementsAsync(long? employeeId, CancellationToken ct = default);
    /// <summary>The caller's own payslips, from disbursed months only.</summary>
    Task<IReadOnlyList<PayslipDto>> MyPayslipsAsync(CancellationToken ct = default);
}
