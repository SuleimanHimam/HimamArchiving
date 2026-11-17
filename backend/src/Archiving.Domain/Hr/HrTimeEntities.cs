using Archiving.Domain.Core;

namespace Archiving.Domain.Hr;

// Phase 2: leave, holidays, shifts and attendance. Every number the Authority has not fixed in writing
// (entitlements, carry-over caps, grace minutes, weekend days, coverage minimums) is a column or a
// setting row, never a constant in code.

/// <summary>Keyed HR setting (weekend days, default shift, …). Keys are listed in <see cref="HrSettingKeys"/>.</summary>
public class HrSetting : CoreEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class HrSettingKeys
{
    /// <summary>Comma-separated <see cref="DayOfWeek"/> names that are not working days, e.g. "Friday".</summary>
    public const string WeekendDays = "weekend_days";
    /// <summary>Shift type id used for employees with no roster entry on a working day.</summary>
    public const string DefaultShiftTypeId = "default_shift_type_id";
    /// <summary>Leave type id an attendance exception is deducted from when resolved as "deduct from leave".</summary>
    public const string AbsenceLeaveTypeId = "absence_leave_type_id";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        WeekendDays, DefaultShiftTypeId, AbsenceLeaveTypeId,
    };
}

/// <summary>An official holiday: not a working day for leave counting and attendance.</summary>
public class PublicHoliday : CoreEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

public enum LeaveKind
{
    Leave = 0,      // إجازة: whole days
    Departure = 1,  // مغادرة: hours within one day
    Mission = 2,    // مهمة رسمية: justifies absence, never deducts
}

public enum LeaveUnit { Days = 0, Hours = 1 }

/// <summary>A leave type the HR administrator defines. All quantities are data.</summary>
public class LeaveType : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LeaveKind Kind { get; set; }
    public LeaveUnit Unit { get; set; }
    /// <summary>When false, the request is recorded and approved but no balance is checked or deducted.</summary>
    public bool DeductsBalance { get; set; } = true;
    /// <summary>Days only: count working days (skip weekend days and holidays) rather than calendar days.</summary>
    public bool CountWorkingDaysOnly { get; set; } = true;
    public bool RequiresSubstitute { get; set; }
    /// <summary>Granted once per year by the "grant entitlement" process. 0 = no automatic grant.</summary>
    public decimal AnnualEntitlement { get; set; }
    /// <summary>Most that moves to next year; null = everything carries, 0 = nothing carries.</summary>
    public decimal? MaxCarryOver { get; set; }
    public bool AppliesToMilitary { get; set; } = true;
    public bool AppliesToCivil { get; set; } = true;
    public bool AppliesToContract { get; set; } = true;
    public long? WorkflowDefinitionId { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AppliesTo(EmployeeCategory c) => c switch
    {
        EmployeeCategory.Military => AppliesToMilitary,
        EmployeeCategory.Civil => AppliesToCivil,
        EmployeeCategory.Contract => AppliesToContract,
        _ => false,
    };
}

public enum LeaveRequestStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, Returned = 4, Cancelled = 5 }

public class LeaveRequest : CoreEntity
{
    public string? Number { get; set; }               // HR-LV-YYYY-NNNNN, issued on approval
    public long EmployeeId { get; set; }
    public long LeaveTypeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public TimeOnly? FromTime { get; set; }           // departures only
    public TimeOnly? ToTime { get; set; }
    /// <summary>Days or hours (the type's unit), computed by the server.</summary>
    public decimal Quantity { get; set; }
    public long? SubstituteEmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Draft;
    public long? ApprovalRequestId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public LeaveType LeaveType { get; set; } = null!;
}

public enum LeaveMovementKind
{
    Entitlement = 0,   // annual grant
    Opening = 1,       // launch balance
    Taken = 2,         // approved request (negative)
    Adjustment = 3,    // HR correction with a reason
    CarryOver = 4,     // moved in from last year
    Expiry = 5,        // dropped at year end (negative)
    Attendance = 6,    // attendance exception deducted from leave (negative)
    Reversal = 7,      // cancels an earlier movement
}

/// <summary>The only way a leave balance changes. Balance = sum of movements (standards §8.1).</summary>
public class LeaveBalanceMovement : CoreEntity
{
    public long EmployeeId { get; set; }
    public long LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Quantity { get; set; }             // signed
    public LeaveMovementKind Kind { get; set; }
    public string SourceType { get; set; } = string.Empty;   // "LeaveRequest", "AttendanceException", "Manual", …
    public long? SourceId { get; set; }
    public string? Note { get; set; }
}

// ---- Shifts ----

public class ShiftType : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }           // Asia/Hebron wall clock
    public TimeOnly EndTime { get; set; }             // earlier than StartTime = ends next day
    public int GraceMinutes { get; set; }
    /// <summary>Counted for the night-shift allowance in payroll.</summary>
    public bool IsNight { get; set; }
    public bool IsActive { get; set; } = true;

    public bool CrossesMidnight => EndTime <= StartTime;
}

public enum RosterCellKind { Shift = 0, Rest = 1, Leave = 2 }

/// <summary>One employee on one day of a roster. Leave cells are written only by an approved leave.</summary>
public class ShiftAssignment : CoreEntity
{
    public long EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public RosterCellKind Kind { get; set; }
    public long? ShiftTypeId { get; set; }
    public long? SiteId { get; set; }
    public long? LeaveRequestId { get; set; }
}

public enum RosterStatus { Draft = 0, Published = 1 }

public class ShiftRoster : CoreEntity
{
    public long SiteId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public RosterStatus Status { get; set; } = RosterStatus.Draft;
    public DateTime? PublishedAt { get; set; }
}

/// <summary>Minimum staff on one shift at one site; the coverage row turns red below it.</summary>
public class CoverageRule : CoreEntity
{
    public long SiteId { get; set; }
    public long ShiftTypeId { get; set; }
    public int MinCount { get; set; }
}

// ---- Attendance ----

public enum PunchSource { Device = 0, Mobile = 1, Import = 2, Correction = 3 }

/// <summary>A raw punch. Never edited or deleted: a correction is a new punch with its reason.</summary>
public class AttendancePunch : CoreEntity
{
    public long EmployeeId { get; set; }
    public DateTime PunchedAt { get; set; }           // UTC
    public PunchSource Source { get; set; }
    public string? DeviceCode { get; set; }
    public string? Note { get; set; }
}

public enum AttendanceExceptionKind { Late = 0, EarlyLeave = 1, MissingPunch = 2, Absent = 3 }
public enum AttendanceExceptionStatus { Pending = 0, Resolved = 1 }

public enum AttendanceResolution
{
    Justified = 0,       // mission or approved departure: no deduction
    DeductLeave = 1,     // from the leave type set in HrSettingKeys.AbsenceLeaveTypeId
    DeductSalary = 2,    // minutes go to payroll
    PunchCorrected = 3,  // a correction punch was recorded
}

public class AttendanceException : CoreEntity
{
    public long EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceExceptionKind Kind { get; set; }
    public int Minutes { get; set; }
    public long? ShiftTypeId { get; set; }
    public AttendanceExceptionStatus Status { get; set; } = AttendanceExceptionStatus.Pending;
    public AttendanceResolution? Resolution { get; set; }
    public string? ResolutionNote { get; set; }
    public long? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>A closed attendance month: no exception in it can be resolved or reprocessed.</summary>
public class AttendancePeriod : CoreEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime ClosedAt { get; set; }
    public long? ClosedBy { get; set; }
}
