using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Retention/disposal rule for a document type.</summary>
public class RetentionPolicy : BaseEntity
{
    public long DocumentTypeId { get; set; }
    public int RetentionMonths { get; set; }
    public DisposalAction DisposalAction { get; set; } = DisposalAction.Review;
    public bool RequiresApproval { get; set; } = true;
    public string? Description { get; set; }

    public DocumentType DocumentType { get; set; } = null!;
}

/// <summary>Pre-expiry warning generated at 30/15/7 days and on expiry.</summary>
public class RetentionAlert : BaseEntity
{
    public long DocumentId { get; set; }
    public RetentionAlertStage Stage { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? NotifiedAt { get; set; }

    public Document Document { get; set; } = null!;
}

/// <summary>Disposal workflow record. The row is retained permanently even after the document is disposed
/// (permanent historical archive).</summary>
public class DisposalRequest : BaseEntity
{
    public long DocumentId { get; set; }
    public DisposalAction Action { get; set; } = DisposalAction.Destroy;
    public DisposalRequestStatus Status { get; set; } = DisposalRequestStatus.Pending;
    public long RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? Justification { get; set; }

    public Document Document { get; set; } = null!;
}
