using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Inbound official correspondence. Routed to a position/org-unit (not a person) so work survives staff changes.</summary>
public class IncomingMail : SoftDeleteEntity
{
    public string TransactionNumber { get; set; } = string.Empty; // auto-generated, unique
    public string SenderEntity { get; set; } = string.Empty;       // sending organization
    public string? SenderName { get; set; }
    public string? SenderReference { get; set; }                   // sender's own reference number
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public long? DocumentTypeId { get; set; }
    public long? CategoryId { get; set; }
    public ConfidentialityLevel Confidentiality { get; set; } = ConfidentialityLevel.Internal;
    public Priority Priority { get; set; } = Priority.Normal;
    public string? Keywords { get; set; }
    public IncomingMailStatus Status { get; set; } = IncomingMailStatus.New;

    // Position-based routing (work follows the seat)
    public long? AssignedToPositionId { get; set; }
    public long? AssignedToOrgUnitId { get; set; }
    public long? AssignedToUserId { get; set; }                    // optional explicit assignee

    // Reference / reply chain (link related letters)
    public long? ParentMailId { get; set; }

    public long? WorkflowInstanceId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long? ClosedBy { get; set; }

    public DocumentType? DocumentType { get; set; }
    public DocumentCategory? Category { get; set; }
    public Position? AssignedToPosition { get; set; }
    public OrgUnit? AssignedToOrgUnit { get; set; }
    public IncomingMail? ParentMail { get; set; }
    public ICollection<MailAttachment> Attachments { get; set; } = new List<MailAttachment>();
}

/// <summary>Outbound official letter. Auto-archived on dispatch.</summary>
public class OutgoingMail : SoftDeleteEntity
{
    public string LetterNumber { get; set; } = string.Empty;       // official numbering, unique
    public string RecipientEntity { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public long? LetterTemplateId { get; set; }                    // letterhead/footer template for printing
    public long? SignatoryPositionId { get; set; }                 // the seat that signs
    public ConfidentialityLevel Confidentiality { get; set; } = ConfidentialityLevel.Internal;
    public Priority Priority { get; set; } = Priority.Normal;
    public OutgoingMailStatus Status { get; set; } = OutgoingMailStatus.Draft;
    public DateTime? SentDate { get; set; }
    public long? InReplyToIncomingMailId { get; set; }             // reply chain
    public long? WorkflowInstanceId { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public LetterTemplate? LetterTemplate { get; set; }
    public Position? SignatoryPosition { get; set; }
    public IncomingMail? InReplyToIncomingMail { get; set; }
    public ICollection<MailAttachment> Attachments { get; set; } = new List<MailAttachment>();
}

public class MailAttachment : BaseEntity
{
    public long MailId { get; set; }
    public MailDirection Direction { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
}

/// <summary>Reusable letterhead/footer + numbering pattern for printing outgoing mail.</summary>
public class LetterTemplate : BaseEntity
{
    public long InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HeaderHtml { get; set; }                        // letterhead
    public string? FooterHtml { get; set; }
    public string? BodyTemplate { get; set; }
    public string? NumberingPattern { get; set; }                  // e.g. "OUT-{yyyy}-{seq:0000}"
    public bool IsActive { get; set; } = true;

    public Institution Institution { get; set; } = null!;
}
