using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Per-document retention tracking. Created when a policy is assigned (or by the background job),
/// it drives expiry detection and the two-step disposition workflow.</summary>
public class DocumentRetention : BaseEntity
{
    public long DocumentId { get; set; }
    public long? RetentionPolicyId { get; set; }
    /// <summary>The resolved date the retention clock started (per the policy's trigger type).</summary>
    public DateOnly TriggerDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public DocumentRetentionStatus Status { get; set; } = DocumentRetentionStatus.Active;
    /// <summary>Preserved across renewals for audit (the first/original expiry).</summary>
    public DateOnly? OriginalExpiryDate { get; set; }

    public Document Document { get; set; } = null!;
    public RetentionPolicy? RetentionPolicy { get; set; }
}

/// <summary>A two-step disposition request (Destroy or Renew) raised when a document reaches end-of-retention.
/// Verification (Records Officer) → Final Approval (Legal/Department Head). The row is retained permanently.</summary>
public class DispositionRequest : BaseEntity
{
    public long DocumentId { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DispositionAction RequestedAction { get; set; } = DispositionAction.Destroy;
    public string Reason { get; set; } = string.Empty;
    public long RequestedByUserId { get; set; }
    public DispositionStatus Status { get; set; } = DispositionStatus.PendingVerification;

    // Step 1 — verification (Records Officer)
    public long? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerificationNotes { get; set; }

    // Step 2 — final approval (Legal / Department Head)
    public long? FinalApprovedByUserId { get; set; }
    public DateTime? FinalApprovedAtUtc { get; set; }
    public string? FinalApprovalNotes { get; set; }

    // Rejection (at either step)
    public long? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    // Renew outcome
    public DateOnly? NewExpiryDate { get; set; }

    // Destroy outcome
    public DestructionMethod Method { get; set; } = DestructionMethod.CryptoShred;
    public string? CustomMethod { get; set; }
    public long? CertificateId { get; set; }

    public Document Document { get; set; } = null!;
}

/// <summary>Certificate of Destruction, signed by both the verifier and the final approver. Batch-capable
/// (one certificate may cover several documents/requests destroyed together).</summary>
public class DispositionCertificate : BaseEntity
{
    public long DispositionRequestId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    /// <summary>Comma-separated document ids covered by this certificate (batch-capable).</summary>
    public string DocumentIds { get; set; } = string.Empty;
    public string DestructionMethod { get; set; } = string.Empty;
    public long? VerifiedByUserId { get; set; }
    public long? FinalApprovedByUserId { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? PdfStorageKey { get; set; }
}
