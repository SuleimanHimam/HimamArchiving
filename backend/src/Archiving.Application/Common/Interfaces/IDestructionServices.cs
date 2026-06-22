using Archiving.Application.Common.Models;
using Archiving.Application.Features.Destruction;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Decides whether a record may be destroyed (retention met, no legal hold, no open workflow).</summary>
public interface IDestructionEligibilityService
{
    Task<DestructionEligibilityDto> CheckAsync(long documentId, CancellationToken ct = default);
}

/// <summary>Renders + stores the Certificate of Destruction (PDF/A) for an executed request.</summary>
public interface ICertificateService
{
    Task<long> IssueAsync(long destructionRequestId, CancellationToken ct = default);
    Task<(Stream Stream, string FileName)?> OpenAsync(long destructionRequestId, CancellationToken ct = default);
}

/// <summary>Place / release / list legal holds. Eligibility checks consume this.</summary>
public interface ILegalHoldService
{
    Task<IReadOnlyList<LegalHoldDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<LegalHoldDto>> PlaceAsync(PlaceLegalHoldRequest request, CancellationToken ct = default);
    Task<Result<bool>> ReleaseAsync(long id, CancellationToken ct = default);
}

/// <summary>Create / submit / approve / reject / cancel destruction requests.
/// Execution (the irreversible step) is intentionally NOT part of this phase.</summary>
public interface IDestructionService
{
    Task<PagedResult<DestructionRequestDto>> ListAsync(DestructionRequestQuery query, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> CreateAsync(CreateDestructionRequest request, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> SubmitAsync(long id, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> ApproveAsync(long id, DestructionDecisionRequest request, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> RejectAsync(long id, DestructionDecisionRequest request, CancellationToken ct = default);
    Task<Result<DestructionRequestDto>> CancelAsync(long id, CancellationToken ct = default);

    /// <summary>Irreversibly destroy the approved request's content (crypto-shred), tombstone the records,
    /// issue the certificate, and audit. Enforces segregation of duties + MFA step-up. When
    /// <paramref name="canOverride"/> (a manager/admin authorized to approve), the two-person rule is
    /// waived and a not-yet-approved request is auto-approved by the executor.</summary>
    Task<Result<DestructionRequestDto>> ExecuteAsync(long id, ExecuteDestructionRequest request, bool canOverride = false, CancellationToken ct = default);
}
