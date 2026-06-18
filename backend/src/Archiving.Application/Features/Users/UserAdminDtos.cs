using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Users;

public sealed record AdminRoleDto(long Id, string Name, string? Description);

public sealed record AdminUserListItem(
    long Id,
    string FullName,
    string Email,
    string JobTitle,
    string Clearance,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<long> RoleIds);

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    string? JobTitle,
    ConfidentialityLevel Clearance,
    long? OrgUnitId,
    IReadOnlyList<long> RoleIds);

public sealed record SetUserRolesRequest(IReadOnlyList<long> RoleIds);

public sealed record SetUserActiveRequest(bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);
