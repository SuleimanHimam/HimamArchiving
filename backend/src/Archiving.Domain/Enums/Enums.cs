namespace Archiving.Domain.Enums;

/// <summary>Confidentiality classification carried by every document and transaction.
/// Access requires the actor's clearance to be >= the item's level.</summary>
public enum ConfidentialityLevel
{
    Public = 0,            // عام / عادي
    Internal = 1,          // داخلي / مقيّد
    Confidential = 2,      // سري
    HighlyConfidential = 3 // سري للغاية
}

/// <summary>State of full-text extraction (embedded text or OCR) for a stored file.</summary>
public enum TextExtractionStatus
{
    Pending = 0,   // queued for the background extractor
    Done = 1,      // text extracted (or none found) successfully
    Failed = 2,    // extraction/OCR threw; left for retry/inspection
    Skipped = 3    // file type not text-extractable (e.g. zip)
}

/// <summary>Granular RBAC action verbs applied per resource/module.</summary>
public enum PermissionAction
{
    View = 0,
    Create = 1,
    Edit = 2,
    Delete = 3,
    Print = 4,
    Archive = 5,
    Approve = 6,
    Forward = 7
}

/// <summary>Flexible organizational hierarchy node kind.</summary>
public enum OrgUnitType
{
    Institution = 0,   // مؤسسة
    Directorate = 1,   // إدارة / مديرية
    Department = 2,    // قسم
    Unit = 3,          // وحدة
    Committee = 4,     // لجنة
    Team = 5           // فريق عمل
}

public enum DocumentStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
    PendingDisposal = 3,
    Disposed = 4
}

/// <summary>Lifecycle status of an inbound transaction.</summary>
public enum IncomingMailStatus
{
    New = 0,
    Assigned = 1,
    InProgress = 2,
    OnHold = 3,     // suspended / held
    Closed = 4,
    Archived = 5
}

public enum OutgoingMailStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Sent = 3,
    Archived = 4
}

public enum MailDirection
{
    Incoming = 0,
    Outgoing = 1
}

public enum Priority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum WorkflowStatus
{
    Running = 0,
    Completed = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>What a workflow stage routes to. Position-based is preferred so work follows the seat, not the person.</summary>
public enum StageAssigneeType
{
    Position = 0,
    OrgUnit = 1,
    Role = 2,
    User = 3,
    DirectManager = 4
}

[Flags]
public enum WorkflowActionType
{
    None = 0,
    Approve = 1,
    Reject = 2,
    Forward = 4,
    Hold = 8,
    Return = 16,
    Comment = 32,
    Close = 64
}

public enum WorkflowTaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Overdue = 3,
    Escalated = 4,
    Reassigned = 5
}

public enum NotificationType
{
    Info = 0,
    Warning = 1,
    Urgent = 2,
    Task = 3,
    Escalation = 4
}

[Flags]
public enum NotificationChannel
{
    None = 0,
    InApp = 1,
    Email = 2,
    Sms = 4,
    Push = 8
}

public enum PhysicalLocationType
{
    Building = 0,
    Room = 1,
    Cabinet = 2,
    Shelf = 3,
    Box = 4
}

public enum DisposalAction
{
    Destroy = 0,
    Transfer = 1,
    Review = 2,
    Retain = 3
}

public enum DisposalRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Executed = 3
}

/// <summary>Retention warning checkpoints before expiry.</summary>
public enum RetentionAlertStage
{
    Days30 = 0,
    Days15 = 1,
    Days7 = 2,
    Expired = 3
}

public enum MfaMethod
{
    None = 0,
    Totp = 1,
    Email = 2,
    Sms = 3
}

public enum Gender
{
    NotSpecified = 0,
    Male = 1,   // ذكر
    Female = 2  // أنثى
}

/// <summary>Restricts where document/attachment uploads may originate (some clients allow scanner-only).</summary>
[Flags]
public enum UploadSource
{
    None = 0,
    Scanner = 1,
    Pdf = 2,
    Image = 4,
    AnyFile = 8,
    All = Scanner | Pdf | Image | AnyFile
}
