using Archiving.Application.Common.Models;
using Archiving.Application.Features.Delegation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Manages temporary position delegations and resolves which positions a user effectively covers.</summary>
public interface IDelegationService
{
    Task<Result<DelegationDto>> CreateDelegationAsync(CreateDelegationDto dto, CancellationToken ct = default);
    Task<Result<bool>> RevokeDelegationAsync(long delegationId, CancellationToken ct = default);

    Task<IReadOnlyList<DelegationDto>> GetDelegationsFromPositionAsync(long positionId, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationDto>> GetDelegationsToPositionAsync(long positionId, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationDto>> GetMyDelegationsAsync(CancellationToken ct = default);

    /// <summary>A user's own position ids plus any positions currently delegated to them.</summary>
    Task<List<long>> GetEffectivePositionIdsForUserAsync(long userId, CancellationToken ct = default);

    /// <summary>Only the positions currently delegated to the user (excludes their own) — used to tag tasks
    /// as arriving via delegation.</summary>
    Task<List<long>> GetDelegatedPositionIdsForUserAsync(long userId, CancellationToken ct = default);
}
