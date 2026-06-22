using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

public enum LegalHoldScope { Document = 0, Folder = 1, OrgUnit = 2, Query = 3 }

public enum DestructionStatus
{
    Draft = 0, PendingReview = 1, PendingApproval = 2, Approved = 3, Rejected = 4,
    Scheduled = 5, Executing = 6, Completed = 7, Cancelled = 8, Failed = 9
}

public enum DestructionMethod
{
    // Digital
    CryptoShred = 0, SecureOverwrite = 1, DeleteFixityVoid = 2,
    // Physical
    Shredding = 3, Incineration = 4, Pulping = 5, Degaussing = 6,
    // Free-text method described by the requester
    Other = 7
}

/// <summary>A legal/litigation hold that makes the in-scope records ineligible for destruction
/// until released. While any active hold matches a document, it can never be destroyed.</summary>
public class LegalHold : BaseEntity
{
    public string Reason { get; set; } = string.Empty;
    public LegalHoldScope Scope { get; set; }
    public long? DocumentId { get; set; }       // Scope = Document
    public long? FolderId { get; set; }         // Scope = Folder
    public long? OrgUnitId { get; set; }        // Scope = OrgUnit
    public string? QueryExpression { get; set; } // Scope = Query (reserved for later)
    public long PlacedByUserId { get; set; }
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public long? ReleasedByUserId { get; set; }
    public DateTime? ReleasedAtUtc { get; set; } // null => still active
}

/// <summary>A controlled, auditable request to destroy one or more records that have met retention.</summary>
public class DestructionRequest : BaseEntity
{
    public DestructionStatus Status { get; set; } = DestructionStatus.Draft;
    public string Reason { get; set; } = string.Empty;
    public long? RetentionBasisId { get; set; }  // RetentionPolicy authorizing disposal
    public long RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public long? ExecutedByUserId { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? ScheduledForUtc { get; set; }
    public long? CertificateId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public ICollection<DestructionItem> Items { get; set; } = new List<DestructionItem>();
}

/// <summary>A single document line on a destruction request, with its method and (post-execution) outcome.</summary>
public class DestructionItem : BaseEntity
{
    public long DestructionRequestId { get; set; }
    public long DocumentId { get; set; }
    public DestructionMethod Method { get; set; } = DestructionMethod.CryptoShred;
    public string? CustomMethod { get; set; }     // free-text method when Method == Other
    public string? ChecksumBefore { get; set; }  // captured at execution as proof-of-prior-existence
    public string? Outcome { get; set; }          // per-item result after execution
    public DestructionRequest Request { get; set; } = null!;
}

/// <summary>An admin-managed destruction method label, shown in the request form. The actual digital
/// technique is always crypto-shred/secure-overwrite; this catalog lets admins name the methods
/// (e.g. physical methods) used in their organization.</summary>
public class DestructionMethodOption : BaseEntity
{
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The Certificate of Destruction (rendered to PDF/A in a later phase).</summary>
public class DestructionCertificate : BaseEntity
{
    public long DestructionRequestId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string? PdfStorageKey { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
}
