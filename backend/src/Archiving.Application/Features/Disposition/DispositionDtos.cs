namespace Archiving.Application.Features.Disposition;

/// <summary>A two-step disposition request projected for the UI, with both approval stages' state.</summary>
public record DispositionRequestDto(
    long Id,
    long DocumentId,
    string? DocumentNumber,
    string? DocumentTitle,
    int RequestedAction,
    string RequestedActionLabel,
    string Reason,
    long RequestedByUserId,
    string? RequestedByName,
    DateTime RequestedAtUtc,
    int Status,
    string StatusLabel,
    long? VerifiedByUserId,
    string? VerifiedByName,
    DateTime? VerifiedAtUtc,
    string? VerificationNotes,
    long? FinalApprovedByUserId,
    string? FinalApprovedByName,
    DateTime? FinalApprovedAtUtc,
    string? FinalApprovalNotes,
    long? RejectedByUserId,
    string? RejectedByName,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    DateOnly? NewExpiryDate,
    int Method,
    string? CustomMethod,
    long? CertificateId,
    string? CertificateNumber,
    DateOnly? ExpiryDate,
    string? BoxCode);

/// <summary>Manually raise a disposition request (the daily job raises them automatically on expiry).</summary>
public record CreateDispositionRequest(long DocumentId, int RequestedAction, string Reason, int Method = 0, string? CustomMethod = null);

/// <summary>Step 1 — Records Officer. Decision: "Verify" | "Reject". Notes mandatory.</summary>
public record VerifyDispositionRequest(string Decision, string Notes);

/// <summary>Step 2 — Legal / Department Head. Decision: "Approve" | "Reject". Notes mandatory; NewExpiryDate for Renew.</summary>
public record FinalApproveDispositionRequest(string Decision, string Notes, DateOnly? NewExpiryDate = null);

/// <summary>Reject at whichever step the request currently sits. Reason mandatory.</summary>
public record RejectDispositionRequest(string Reason);

/// <summary>Certificate of Destruction, signed by both the verifier and the final approver.</summary>
public record DispositionCertificateDto(
    long RequestId,
    string CertificateNumber,
    IReadOnlyList<long> DocumentIds,
    IReadOnlyList<string> DocumentNumbers,
    string DestructionMethod,
    string? VerifiedByName,
    string? FinalApprovedByName,
    DateTime GeneratedAtUtc);
