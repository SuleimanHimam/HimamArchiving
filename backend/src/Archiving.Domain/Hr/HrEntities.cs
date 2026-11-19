using Archiving.Domain.Core;

namespace Archiving.Domain.Hr;

/// <summary>
/// What an approved personnel action changes. One value per effect documented in the operations
/// sequence (§8.3). The action types themselves (names, codes, approval path) are admin data; only
/// the effects are code.
/// </summary>
public enum PersonnelActionEffect
{
    Hire = 0,        // creates the employee file
    Confirm = 1,     // appointment type
    Promote = 2,     // grade (salary follows in payroll)
    Transfer = 3,    // organisational unit; the linked login moves with it
    Second = 4,      // temporary work place: recorded
    Allowance = 5,   // salary element: recorded until payroll exists
    Penalty = 6,     // penalty record
    Suspend = 7,     // status Suspended, login disabled
    EndService = 8,  // status Terminated, clearance opened; login disabled when it is issued
}

/// <summary>An action type the HR administrator manages: name, code, effect and approval path.</summary>
public class PersonnelActionType : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PersonnelActionEffect Effect { get; set; }
    /// <summary>The approval path, built in the workflow designer. Null = not configured: submit is refused.</summary>
    public long? WorkflowDefinitionId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The personnel file around a <see cref="Employee"/>; the core row is never copied.</summary>
public class EmployeeProfile : CoreEntity
{
    public long EmployeeId { get; set; }
    public DateOnly? BirthDate { get; set; }
    public long? MaritalStatusId { get; set; }      // lookup marital_status
    public string? Address { get; set; }
    public long? AppointmentTypeId { get; set; }    // lookup appointment_type
    public long? GradeId { get; set; }              // lookup grade
    /// <summary>Encrypted national id (see <c>ISensitiveDataProtector</c>). Never stored in clear.</summary>
    public byte[]? NationalIdEnc { get; set; }
    public byte[]? MobileEnc { get; set; }
}

public class EmployeeFamilyMember : CoreEntity
{
    public long EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public long RelationshipId { get; set; }        // lookup relationship
    public DateOnly? BirthDate { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeQualification : CoreEntity
{
    public long EmployeeId { get; set; }
    public long QualificationTypeId { get; set; }   // lookup qualification_type
    public string Title { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public int? Year { get; set; }
}

public class EmployeeBankAccount : CoreEntity
{
    public long EmployeeId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public byte[] AccountEnc { get; set; } = [];    // IBAN / account number, encrypted
    public bool IsPrimary { get; set; } = true;
}

public enum PersonnelActionStatus
{
    Draft = 0,
    Submitted = 1,
    Executed = 2,
    Rejected = 3,
    Returned = 4,
    Cancelled = 5,
}

/// <summary>
/// One administrative action on an employee. The target values travel in <see cref="PayloadJson"/>;
/// <see cref="BeforeJson"/>/<see cref="AfterJson"/> record what the effect changed.
/// </summary>
public class PersonnelAction : CoreEntity
{
    public string? Number { get; set; }             // HR-MV-YYYY-NNNNN, issued on execution
    public long ActionTypeId { get; set; }
    /// <summary>Null only for a hire before it is executed: the employee is created by the effect.</summary>
    public long? EmployeeId { get; set; }
    public PersonnelActionStatus Status { get; set; } = PersonnelActionStatus.Draft;
    public DateOnly EffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public long? ApprovalRequestId { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? DecisionNote { get; set; }

    public PersonnelActionType ActionType { get; set; } = null!;
}

public enum ClearanceStatus { Open = 0, Issued = 1 }

/// <summary>براءة الذمة: opened by an approved end of service, issued only when every party has cleared.</summary>
public class Clearance : CoreEntity
{
    public string Number { get; set; } = string.Empty;   // CLR-YYYY-NNN
    public long EmployeeId { get; set; }
    public long PersonnelActionId { get; set; }
    public ClearanceStatus Status { get; set; } = ClearanceStatus.Open;
    public DateTime? IssuedAt { get; set; }

    public ICollection<ClearanceItem> Items { get; set; } = new List<ClearanceItem>();
}

public class ClearanceItem : CoreEntity
{
    public long ClearanceId { get; set; }
    public long PartyId { get; set; }                  // lookup clearance_party
    public bool IsCleared { get; set; }
    public long? ClearedBy { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string? Note { get; set; }

    public Clearance Clearance { get; set; } = null!;
}
