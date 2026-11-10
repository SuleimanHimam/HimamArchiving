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

/// <summary>Who an outgoing letter is addressed to. Internal kinds (Employee/Department/Archive) auto-generate
/// an incoming item for the recipient on dispatch; External keeps the legacy free-text recipient.</summary>
public enum RecipientKind
{
    External = 0,
    Employee = 1,
    Department = 2,
    Archive = 3,
    Group = 4      // a distribution group; dispatch fans out one incoming item per member
}

/// <summary>What a correspondence-group member points at.</summary>
public enum GroupMemberType
{
    User = 0,
    OrgUnit = 1
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
    Disposed = 4,
    PendingArchive = 5    // submitted to the Archive department; awaiting review + physical filing
}

/// <summary>How a document was archived: filled in manually (standalone) or created/linked from an
/// a manual data-entry archiving path.</summary>
public enum ArchiveMode
{
    Manual = 0
}

/// <summary>Ownership level of a print/report design. Resolution when printing: User → OrgUnit → Default.</summary>
public enum ReportTemplateScope
{
    Default = 0,   // institution-wide fallback
    OrgUnit = 1,   // a specific department's design
    User = 2       // a specific user's personal design
}

/// <summary>What a print/report design applies to.</summary>
public enum ReportTemplateTarget
{
    Correspondence = 0,   // incoming/outgoing letters
    Document = 1,         // documents/reports
    All = 2               // any printable item
}

/// <summary>
/// Lifecycle of a slot-based letterhead. Only <see cref="Active"/> is ever rendered onto
/// paper: a department edit sits at <see cref="PendingApproval"/> until the approval workflow
/// clears it, so an unreviewed change can never reach an external recipient.
/// </summary>
public enum LetterheadStatus
{
    Draft = 0,
    PendingApproval = 1,
    Active = 2,
    /// <summary>A legacy free-form design that could not be mapped onto slots automatically.
    /// Kept intact (see ReportTemplate.LegacyLayoutJson) for an admin to re-author by hand —
    /// never rendered, never deleted.</summary>
    NeedsReview = 3,
    Rejected = 4
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
    Cancelled = 3,
    /// <summary>Parked with the originator after a RequestChanges. Resumes on resubmission; the run
    /// is the same instance, so nothing already approved has to be approved again.</summary>
    ChangesRequested = 4
}

/// <summary>What a rejection does to the run. Configured per stage, because a finance rejection
/// usually ends the request while a clerical one often just bounces it back one desk.</summary>
public enum RejectBehavior
{
    /// <summary>Stop the run and send the rejection, with its reason, to the original requester.</summary>
    StopAndReturnToRequester = 0,
    /// <summary>Hand the file back to the previous approver, who can correct and re-approve.</summary>
    ReturnToPreviousStage = 1
}

/// <summary>Where a run picks up after the requester resubmits following a RequestChanges.</summary>
public enum ReturnResumeMode
{
    /// <summary>Back to the stage that asked for the changes. Earlier approvals stand.</summary>
    ResumeAtSameStage = 0,
    /// <summary>Back to the first stage. Everything is approved again, on the same instance.</summary>
    RestartFromStart = 1
}

/// <summary>Why a piece of internal correspondence exists inside a workflow run.</summary>
public enum WorkflowMailRole
{
    /// <summary>The correspondence the requester composed, which started the run.</summary>
    Origin = 0,
    /// <summary>Generated by the engine to put one stage's task in front of its assignee.</summary>
    Step = 1,
    /// <summary>The closing message — approved, rejected, or returned for changes — to the requester.</summary>
    Result = 2
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
    Close = 64,
    /// <summary>Send the request back to the originator to be corrected and resubmitted. Unlike
    /// <see cref="Return"/>, which hands the file to the previous approver, this parks the run and
    /// waits on the requester — the history of what was already approved is kept.</summary>
    RequestChanges = 128
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
    Expired = 3,
    Days90 = 4,
    Days60 = 5
}

// ── Retention & Disposition (two-step) ───────────────────────────────────────

/// <summary>Where a document's retention clock starts. Different categories trigger differently.</summary>
public enum RetentionTriggerType
{
    CreationDate = 0,
    LastModified = 1,
    CustomEventDate = 2,
    ContractEndDate = 3,
    // Event-based anchors (RM-03, v1.1)
    ArchiveDate = 4,
    ClosureDate = 5,
    CaseClosure = 6,
    EmployeeExit = 7,
    FiscalYearEnd = 8
}

/// <summary>Default disposition action a record series routes to when retention expires (RM-05/06, v1.1).</summary>
public enum SeriesDispositionDefault
{
    Review = 0,    // route to the disposition workflow; a human decides
    Archive = 1,
    Destroy = 2,
    Transfer = 3   // transfer to the archive authority
}

/// <summary>What happens by default when a document reaches the end of its retention period.</summary>
public enum RetentionDefaultAction
{
    RequireDecision = 0,
    AutoRenew = 1
}

/// <summary>Per-document retention tracking status.</summary>
public enum DocumentRetentionStatus
{
    Active = 0,
    PendingReview = 1,
    Renewed = 2,
    ApprovedForDestruction = 3,
    Destroyed = 4,
    OnLegalHold = 5
}

/// <summary>The action a disposition request asks for.</summary>
public enum DispositionAction
{
    Destroy = 0,
    Renew = 1
}

/// <summary>Two-step disposition workflow state.</summary>
public enum DispositionStatus
{
    PendingVerification = 0,
    PendingFinalApproval = 1,
    Approved = 2,
    Rejected = 3,
    Completed = 4
}

// ── Inter-department correspondence ──────────────────────────────────────────

/// <summary>Which kinds of tasks a position delegation covers (Phase 5).</summary>
public enum DelegationScope
{
    All = 0,
    WorkflowOnly = 1,
    MailOnly = 2,
    SpecificWorkflowType = 3
}

/// <summary>What an escalation rule does when it fires (Phase 6).</summary>
public enum EscalationAction
{
    NotifyOnly = 0,          // notify the escalation target
    ReassignTask = 1,        // move the task to the escalation target
    NotifyAndReassign = 2    // both
}

/// <summary>Kind of external organization (EXT, v1.1).</summary>
public enum ExternalOrgType
{
    Government = 0,
    Ministry = 1,
    Company = 2,
    Authority = 3,
    Ngo = 4,
    Individual = 5,
    Other = 6
}

/// <summary>Communication channel kind for an external contact (EXT, v1.1).</summary>
public enum ContactChannelType
{
    Email = 0,
    Phone = 1,
    Mobile = 2,
    Fax = 3,
    Address = 4
}

/// <summary>Lifecycle of a webhook delivery attempt (Phase 11).</summary>
public enum DeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    Retrying = 3
}

/// <summary>How often a numbering template's sequence resets (Phase 9).</summary>
public enum ResetFrequency
{
    Never = 0,
    Yearly = 1,
    Monthly = 2
}

/// <summary>How a workflow stage participates in routing (Phase 7).</summary>
public enum StageType
{
    Sequential = 0,   // the default: runs after the previous stage
    Parallel = 1,     // runs concurrently with others in the same ParallelGroupId
    Conditional = 2   // runs only if its ConditionExpression evaluates true, else it is skipped
}

/// <summary>How a stage resolves its assignee at runtime (Phase 7).</summary>
public enum DynamicAssignmentType
{
    Static = 0,                   // fixed position (existing behavior)
    OrgHierarchy = 1,             // the Nth-level supervisor of the originator
    DocumentOwnerDepartment = 2,  // a role within the document creator's department
    DepartmentHead = 3            // the head of a specified or the originator's department
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

// ── Correspondence module ─────────────────────────────────────────────────────
// Classification reuses ConfidentialityLevel (Public/Internal/Confidential/HighlyConfidential);
// Priority reuses Priority (Low/Normal/High/Urgent); target-type reuses RecipientKind;
// distribution groups reuse CorrespondenceGroup. Only the enums genuinely new to the
// module are declared here.

/// <summary>Flow direction of a correspondence item. Internal is split so an internal letter
/// produces a tracked outgoing on the sender side and a generated incoming on each recipient side.</summary>
public enum CorrespondenceDirection
{
    Incoming = 0,
    Outgoing = 1,
    InternalIncoming = 2,
    InternalOutgoing = 3
}

/// <summary>Lifecycle status of a correspondence item. The approval-related states are projected
/// read-only from the workflow engine; there is no user-facing workflow UI.</summary>
public enum CorrespondenceStatus
{
    Draft = 0,
    Registered = 1,
    UnderReview = 2,
    Approved = 3,
    Sent = 4,
    Replied = 5,
    OnHold = 6,
    Overdue = 7,
    Closed = 8,
    Archived = 9
}

/// <summary>How a recipient is addressed — mirrors an email client's To / Cc / Bcc.</summary>
public enum CorrespondenceAddressing
{
    To = 0,
    Cc = 1,
    Bcc = 2
}

/// <summary>Relationship between two correspondence items (self-referential graph).</summary>
public enum CorrespondenceLinkType
{
    Reply = 0,
    Forward = 1,
    Reference = 2,
    Related = 3
}

/// <summary>Per-resolved-user delivery/read state. Group targets expand into one row per member.</summary>
public enum CorrespondenceReadStatus
{
    Undelivered = 0,
    Delivered = 1,
    Read = 2
}

/// <summary>OCR/text-extraction state of a correspondence attachment (parallels TextExtractionStatus).</summary>
public enum CorrespondenceOcrStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    NotApplicable = 4
}

/// <summary>Direction of an external Diwan letter — into the institution (وارد) or out of it (صادر).</summary>
public enum DiwanDirection
{
    Incoming = 0,   // وارد خارجي
    Outgoing = 1    // صادر خارجي
}

/// <summary>The action a correspondence directs its recipients to take ("تصنيف/نوع الإجراء" on the register
/// form). Combined with a response period, it drives the no-reply reminder sweep.</summary>
public enum CorrespondenceActionType
{
    ForAction = 0,      // للإجراء اللازم
    ForSigning = 1,     // للتوقيع
    ForReview = 2,      // للمراجعة والدراسة
    ForInfo = 3,        // للعلم والإحاطة
    ForReply = 4,       // للرد
    ForApproval = 5,    // للاعتماد
    ForArchive = 6      // للحفظ
}

/// <summary>Unit a retention period is expressed in. The exact unit is kept so a period of
/// days stays precise; DocumentType.RetentionMonths is derived from it for the older
/// consumers (lifecycle, disposition) that reason in whole months.</summary>
public enum RetentionUnit
{
    Days = 0,
    Months = 1,
    Years = 2
}
