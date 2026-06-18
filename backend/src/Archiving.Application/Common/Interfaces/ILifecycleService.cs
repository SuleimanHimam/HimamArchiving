using Archiving.Application.Common.Models;
using Archiving.Application.Features.Lifecycle;

namespace Archiving.Application.Common.Interfaces;

public interface ILifecycleService
{
    // Retention policies
    Task<IReadOnlyList<RetentionPolicyDto>> ListPoliciesAsync(CancellationToken ct = default);
    Task<Result<RetentionPolicyDto>> CreatePolicyAsync(CreateRetentionPolicyRequest request, CancellationToken ct = default);

    // Documents nearing/past expiry
    Task<IReadOnlyList<ExpiringDocumentDto>> ExpiringAsync(int withinDays, CancellationToken ct = default);

    // Disposal flow
    Task<IReadOnlyList<DisposalRequestDto>> ListDisposalRequestsAsync(CancellationToken ct = default);
    Task<Result<DisposalRequestDto>> RequestDisposalAsync(CreateDisposalRequestRequest request, CancellationToken ct = default);
    Task<Result<DisposalRequestDto>> DecideAsync(long id, DisposalDecisionRequest request, CancellationToken ct = default);
    Task<Result<DisposalRequestDto>> ExecuteAsync(long id, CancellationToken ct = default);
}
