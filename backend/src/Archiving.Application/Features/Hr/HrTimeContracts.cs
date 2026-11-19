using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;

namespace Archiving.Application.Features.Hr;

// HR phase 2: settings, holidays, leave, shifts, attendance. Enum values travel as their names.

// ---- Settings & calendar ----

public sealed record HrSettingsDto(string WeekendDays, long? DefaultShiftTypeId, long? AbsenceLeaveTypeId);
public sealed record SaveHrSettingsRequest(string WeekendDays, long? DefaultShiftTypeId, long? AbsenceLeaveTypeId);

public sealed record HolidayDto(long Id, DateOnly Date, string Name, string RowVersion);
public sealed record SaveHolidayRequest(DateOnly Date, string Name, string? RowVersion = null);

public interface IHrCalendarService
{
    Task<HrSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<Result<HrSettingsDto>> SaveSettingsAsync(SaveHrSettingsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<HolidayDto>> HolidaysAsync(int year, CancellationToken ct = default);
    Task<Result<HolidayDto>> AddHolidayAsync(SaveHolidayRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteHolidayAsync(long id, CancellationToken ct = default);
    /// <summary>The direct manager of an employee, from the org structure (unit manager seat, walking up).</summary>
    Task<Result<EmployeeDto?>> DirectManagerAsync(long employeeId, CancellationToken ct = default);
}

// ---- Leave ----

public sealed record LeaveTypeDto(long Id, string Code, string Name, string Kind, string Unit, bool DeductsBalance,
    bool CountWorkingDaysOnly, bool RequiresSubstitute, decimal AnnualEntitlement, decimal? MaxCarryOver,
    bool AppliesToMilitary, bool AppliesToCivil, bool AppliesToContract, long? WorkflowDefinitionId,
    string? WorkflowDefinitionName, bool IsActive, string RowVersion);

public sealed record SaveLeaveTypeRequest(string Code, string Name, string Kind, string Unit, bool DeductsBalance,
    bool CountWorkingDaysOnly, bool RequiresSubstitute, decimal AnnualEntitlement, decimal? MaxCarryOver,
    bool AppliesToMilitary, bool AppliesToCivil, bool AppliesToContract, long? WorkflowDefinitionId,
    bool IsActive = true, string? RowVersion = null);

public sealed record LeaveRequestDto(long Id, string? Number, long EmployeeId, string? EmployeeName, long LeaveTypeId,
    string LeaveTypeName, string Kind, string Unit, DateOnly FromDate, DateOnly ToDate, string? FromTime, string? ToTime,
    decimal Quantity, long? SubstituteEmployeeId, string? SubstituteName, string Reason, string Status,
    long? ApprovalRequestId, DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt, string RowVersion);

/// <summary>EmployeeId null = the caller's own employee record (self-service).</summary>
public sealed record SaveLeaveRequest(long? EmployeeId, long LeaveTypeId, DateOnly FromDate, DateOnly ToDate,
    string? FromTime, string? ToTime, long? SubstituteEmployeeId, string Reason, string? RowVersion = null);

/// <summary>The instant check (فحص فوري): what the request would take and what is left.</summary>
public sealed record LeaveCheckDto(decimal Quantity, decimal? Balance, decimal? BalanceAfter, bool Sufficient,
    IReadOnlyList<string> Problems);

public sealed record LeaveBalanceDto(long LeaveTypeId, string LeaveTypeName, string Unit, int Year,
    decimal Entitled, decimal Taken, decimal Pending, decimal Balance);

public sealed record LeaveMovementDto(long Id, long LeaveTypeId, string LeaveTypeName, int Year, decimal Quantity,
    string Kind, string SourceType, long? SourceId, string? Note, DateTime CreatedAt);

public sealed record LeaveAdjustmentRequest(long EmployeeId, long LeaveTypeId, int Year, decimal Quantity, string Kind, string Note);

public sealed record LeaveGrantResult(int Granted, int CarriedOver, int Skipped);

public sealed record LeaveListQuery(long? EmployeeId = null, string? Status = null, int Page = 1, int PageSize = 20);

public interface ILeaveTypeService
{
    Task<IReadOnlyList<LeaveTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<LeaveTypeDto>> CreateAsync(SaveLeaveTypeRequest request, CancellationToken ct = default);
    Task<Result<LeaveTypeDto>> UpdateAsync(long id, SaveLeaveTypeRequest request, CancellationToken ct = default);
}

public interface ILeaveService
{
    Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveRequestDto>> MineAsync(CancellationToken ct = default);
    Task<Result<LeaveCheckDto>> CheckAsync(SaveLeaveRequest request, long? excludeId, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> CreateAsync(SaveLeaveRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> UpdateAsync(long id, SaveLeaveRequest request, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> SubmitAsync(long id, CancellationToken ct = default);
    Task<Result<LeaveRequestDto>> CancelAsync(long id, CancellationToken ct = default);

    /// <summary>EmployeeId null = the caller's own balances.</summary>
    Task<Result<IReadOnlyList<LeaveBalanceDto>>> BalancesAsync(long? employeeId, int year, CancellationToken ct = default);
    Task<Result<IReadOnlyList<LeaveMovementDto>>> MovementsAsync(long employeeId, int year, CancellationToken ct = default);
    /// <summary>Opening balance or a correction. Never edits a movement; adds one, with a mandatory note.</summary>
    Task<Result<LeaveMovementDto>> AdjustAsync(LeaveAdjustmentRequest request, CancellationToken ct = default);
    /// <summary>Grants every active type's annual entitlement for the year and moves last year's balance
    /// within the carry-over cap. Idempotent per employee, type and year.</summary>
    Task<Result<LeaveGrantResult>> GrantYearAsync(int year, CancellationToken ct = default);
}

// ---- Shifts ----

public sealed record ShiftTypeDto(long Id, string Code, string Name, string StartTime, string EndTime, int GraceMinutes,
    bool IsNight, bool IsActive, string RowVersion);
public sealed record SaveShiftTypeRequest(string Code, string Name, string StartTime, string EndTime, int GraceMinutes,
    bool IsNight, bool IsActive = true, string? RowVersion = null);

public sealed record CoverageRuleDto(long Id, long SiteId, long ShiftTypeId, int MinCount);
public sealed record SaveCoverageRuleRequest(long SiteId, long ShiftTypeId, int MinCount);

/// <summary>Kind: Shift (with ShiftTypeId), Rest or Leave (read-only, from an approved leave).</summary>
public sealed record RosterCellDto(long EmployeeId, DateOnly Date, string Kind, long? ShiftTypeId);
public sealed record RosterEmployeeDto(long Id, string EmployeeNumber, string FullName);
public sealed record CoverageCellDto(DateOnly Date, long ShiftTypeId, int Count, int MinCount);
public sealed record RosterDto(long SiteId, int Year, int Month, string Status, DateTime? PublishedAt,
    IReadOnlyList<RosterEmployeeDto> Employees, IReadOnlyList<RosterCellDto> Cells, IReadOnlyList<CoverageCellDto> Coverage);

/// <summary>Kind "Clear" removes the cell. Leave cells cannot be overwritten.</summary>
public sealed record RosterCellChange(long EmployeeId, DateOnly Date, string Kind, long? ShiftTypeId);
public sealed record SaveRosterCellsRequest(IReadOnlyList<RosterCellChange> Cells);

public interface IShiftService
{
    Task<IReadOnlyList<ShiftTypeDto>> TypesAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<ShiftTypeDto>> CreateTypeAsync(SaveShiftTypeRequest request, CancellationToken ct = default);
    Task<Result<ShiftTypeDto>> UpdateTypeAsync(long id, SaveShiftTypeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CoverageRuleDto>> CoverageRulesAsync(long siteId, CancellationToken ct = default);
    Task<Result<CoverageRuleDto>> SaveCoverageRuleAsync(SaveCoverageRuleRequest request, CancellationToken ct = default);
    Task<Result<RosterDto>> RosterAsync(long siteId, int year, int month, CancellationToken ct = default);
    Task<Result<RosterDto>> SaveCellsAsync(long siteId, int year, int month, SaveRosterCellsRequest request, CancellationToken ct = default);
    /// <summary>Publishes the month and notifies every employee on it.</summary>
    Task<Result<RosterDto>> PublishAsync(long siteId, int year, int month, CancellationToken ct = default);
}

// ---- Attendance ----

/// <summary>PunchedAt is ISO 8601; without an offset it is read as Asia/Hebron wall-clock time.</summary>
public sealed record PunchImportLine(string EmployeeNumber, string PunchedAt, string? DeviceCode);
public sealed record ImportPunchesRequest(string Source, IReadOnlyList<PunchImportLine> Lines);
public sealed record ImportPunchesResult(int Imported, int Duplicates, IReadOnlyList<string> Errors);

public sealed record ProcessAttendanceRequest(DateOnly From, DateOnly To);
public sealed record ProcessAttendanceResult(int Days, int Exceptions);

public sealed record AttendanceDayDto(long EmployeeId, string EmployeeNumber, string EmployeeName, DateOnly Date,
    string? ShiftName, string? FirstIn, string? LastOut, IReadOnlyList<string> Sources, string State,
    IReadOnlyList<AttendanceExceptionDto> Exceptions);

public sealed record AttendanceExceptionDto(long Id, long EmployeeId, string EmployeeName, DateOnly Date, string Kind,
    int Minutes, string Status, string? Resolution, string? ResolutionNote, DateTime? ResolvedAt);

/// <summary>CorrectionTime (HH:mm, Asia/Hebron) is required for PunchCorrected; Note always.</summary>
public sealed record ResolveExceptionRequest(string Resolution, string Note, string? CorrectionTime = null);

public sealed record AttendancePeriodDto(int Year, int Month, bool IsClosed, DateTime? ClosedAt, int PendingExceptions);

public interface IAttendanceService
{
    Task<Result<ImportPunchesResult>> ImportAsync(ImportPunchesRequest request, CancellationToken ct = default);
    Task<Result<ProcessAttendanceResult>> ProcessAsync(ProcessAttendanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceDayDto>> DayAsync(DateOnly date, long? siteId, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceExceptionDto>> ExceptionsAsync(string? status, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<Result<AttendanceExceptionDto>> ResolveAsync(long id, ResolveExceptionRequest request, CancellationToken ct = default);
    Task<AttendancePeriodDto> PeriodAsync(int year, int month, CancellationToken ct = default);
    /// <summary>Refused while any exception in the month is pending.</summary>
    Task<Result<AttendancePeriodDto>> CloseAsync(int year, int month, CancellationToken ct = default);
    /// <summary>The caller's own days in a month (self-service «دوامي»).</summary>
    Task<Result<IReadOnlyList<AttendanceDayDto>>> MineAsync(int year, int month, CancellationToken ct = default);
}
