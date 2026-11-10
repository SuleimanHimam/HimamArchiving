using Archiving.Application.Common.Models;
using Archiving.Application.Features.Groups;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Manages distribution groups (employees + departments) used as internal-correspondence recipients.</summary>
public interface IGroupService
{
    /// <param name="memberOnly">Only groups the caller belongs to — directly, or through their org unit.
    /// This is what the compose screen asks for: you may address a group you are part of.</param>
    Task<IReadOnlyList<GroupDto>> ListAsync(bool activeOnly, bool memberOnly = false, CancellationToken ct = default);

    /// <summary>True when the user is a member of the group, directly or via their org unit.</summary>
    Task<bool> IsMemberAsync(long groupId, long userId, CancellationToken ct = default);

    /// <summary>Active groups a given user belongs to, directly or through their org unit.</summary>
    Task<IReadOnlyList<GroupDto>> ListForUserAsync(long userId, CancellationToken ct = default);
    Task<Result<GroupDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<GroupDto>> CreateAsync(SaveGroupRequest request, CancellationToken ct = default);
    Task<Result<GroupDto>> UpdateAsync(long id, SaveGroupRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);
}
