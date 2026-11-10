using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>المراسلات — INTERNAL correspondence between employees/departments inside the institution.
/// Behaves like internal mail: addressed to internal recipients (To/Cc/Bcc), per-user read tracking, and
/// threaded replies (Gmail-style conversation). Soft-deleted; can be filed into the Documents archive.</summary>
public class InternalMail : SoftDeleteEntity
{
    public long InstitutionId { get; set; }

    // ── Identity ──
    public string MailNumber { get; set; } = string.Empty;             // auto: INT-{year}-{seq}
    public string? ReferenceNumber { get; set; }

    // ── Sender (the composing user) ──
    public long SenderUserId { get; set; }
    public string? SenderName { get; set; }                            // snapshot
    public long? SenderOrgUnitId { get; set; }                         // the sender's department

    // ── Content ──
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Body { get; set; }                                  // rich HTML
    public string? Keywords { get; set; }

    // ── Dates (UTC) ──
    public DateTime MailDate { get; set; } = DateTime.UtcNow;
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? ResponseDeadline { get; set; }
    public int? ResponsePeriodDays { get; set; }
    public bool RemindOnNoReply { get; set; }
    public DateTime? NoReplyNotifiedAt { get; set; }

    // ── Classification & directed action ──
    /// <summary>تصنيف البريد — what kind of message this is (قسيمة راتب، سند قبض، سند دفع …).
    /// Also selects the letterhead it prints on. Null when the sender did not classify it.</summary>
    public long? CategoryId { get; set; }
    public CorrespondenceActionType? ActionType { get; set; }
    public ConfidentialityLevel Classification { get; set; } = ConfidentialityLevel.Internal;
    public Priority Priority { get; set; } = Priority.Normal;
    public CorrespondenceStatus Status { get; set; } = CorrespondenceStatus.Draft;

    // ── Archive linkage ──
    public string? FileNumber { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public long? ArchivedDocumentId { get; set; }

    // ── Threading (conversation) ──
    public long? ReplyToId { get; set; }                               // the message this replies to
    public long? ParentId { get; set; }                                // thread root

    // ── Workflow ──
    /// <summary>The run this message belongs to — set on the originating correspondence and on every
    /// message the engine generates for it, so the whole approval chain is one conversation.</summary>
    public long? WorkflowInstanceId { get; set; }

    /// <summary>On a step message, the task waiting on this recipient. This is what turns an ordinary
    /// inbox row into an actionable one: non-null means «بانتظار إجرائك».</summary>
    public long? WorkflowTaskId { get; set; }

    /// <summary>Why this message exists inside the run. Null on ordinary correspondence.</summary>
    public WorkflowMailRole? WorkflowRole { get; set; }

    public MailCategory? Category { get; set; }
    public ICollection<InternalRecipient> Recipients { get; set; } = new List<InternalRecipient>();
    public ICollection<InternalRead> Reads { get; set; } = new List<InternalRead>();
    public ICollection<InternalAttachment> Attachments { get; set; } = new List<InternalAttachment>();
}

/// <summary>An internal addressing row: who a message goes to and in what capacity (To/Cc/Bcc).
/// Target is an internal employee, department, or committee/group — never an external party.</summary>
public class InternalRecipient : BaseEntity
{
    public long InternalMailId { get; set; }
    public CorrespondenceAddressing Addressing { get; set; } = CorrespondenceAddressing.To;
    public RecipientKind TargetKind { get; set; } = RecipientKind.Department;   // Employee | Department | Group
    public long? TargetId { get; set; }
    public string? TargetName { get; set; }

    public InternalMail InternalMail { get; set; } = null!;
}

/// <summary>Per-user delivery + read tracking. Group/department recipients expand into one row per member.</summary>
public class InternalRead : BaseEntity
{
    public long InternalMailId { get; set; }
    public long UserId { get; set; }
    public CorrespondenceReadStatus ReadStatus { get; set; } = CorrespondenceReadStatus.Undelivered;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FirstReadAt { get; set; }
    public DateTime? LastReadAt { get; set; }

    public InternalMail InternalMail { get; set; } = null!;
}

/// <summary>A file attached to an internal message (stored via IFileStorage).</summary>
public class InternalAttachment : BaseEntity
{
    public long InternalMailId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int Version { get; set; } = 1;
    public string? OcrText { get; set; }
    public CorrespondenceOcrStatus OcrStatus { get; set; } = CorrespondenceOcrStatus.Pending;

    public InternalMail InternalMail { get; set; } = null!;
}
