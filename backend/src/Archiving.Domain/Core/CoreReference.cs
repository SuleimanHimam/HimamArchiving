namespace Archiving.Domain.Core;

/// <summary>
/// An admin-managed reference value (marital status, grade, clearance party, …). Values the Authority
/// has not supplied in writing live here as data, so they change from the settings screen, not in code.
/// </summary>
public class Lookup : CoreEntity
{
    public string Category { get; set; } = string.Empty;   // e.g. LookupCategories.Grade
    public string Code { get; set; } = string.Empty;       // unique within the category
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The lookup categories the platform reads. A new category needs only a constant here.</summary>
public static class LookupCategories
{
    public const string MaritalStatus = "marital_status";
    public const string AppointmentType = "appointment_type";
    public const string Grade = "grade";
    public const string QualificationType = "qualification_type";
    public const string Relationship = "relationship";
    public const string ClearanceParty = "clearance_party";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        MaritalStatus, AppointmentType, Grade, QualificationType, Relationship, ClearanceParty,
    };
}

public enum ApprovalRequestStatus
{
    Pending = 0,    // in the approval path
    Approved = 1,   // last level approved, effect applied
    Rejected = 2,
    Returned = 3,   // sent back to the requester for changes
    Cancelled = 4,
}

/// <summary>
/// A request that needs approval (standards §5.1). The path itself runs on the existing workflow engine;
/// this row ties a run to the module record it decides on and to the effect that runs when it ends.
/// </summary>
public class ApprovalRequest : CoreEntity
{
    public string Number { get; set; } = string.Empty;         // REQ-YYYY-NNNNN
    public string RequestType { get; set; } = string.Empty;    // e.g. "hr.action"
    public string SourceModule { get; set; } = string.Empty;   // e.g. "HR"
    public long SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long? RequesterUserId { get; set; }
    public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;
    public long? WorkflowInstanceId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}
