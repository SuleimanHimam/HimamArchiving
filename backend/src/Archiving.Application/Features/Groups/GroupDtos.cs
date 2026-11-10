using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Groups;

public sealed record GroupMemberDto(
    long Id,
    string MemberType,      // User | OrgUnit
    long? UserId,
    string? UserName,
    long? OrgUnitId,
    string? OrgUnitName);

public sealed record GroupDto(
    long Id,
    string Name,
    string? Description,
    bool IsActive,
    int MemberCount,
    IReadOnlyList<GroupMemberDto> Members);

public sealed record GroupMemberInput(GroupMemberType MemberType, long? UserId, long? OrgUnitId);

public sealed record SaveGroupRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<GroupMemberInput> Members);
