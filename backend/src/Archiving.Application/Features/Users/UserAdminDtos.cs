using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Users;

public sealed record AdminRoleDto(long Id, string Name, string? Description);

public sealed record AdminUserListItem(
    long Id,
    string FullName,
    string? FirstName,
    string? SecondName,
    string? ThirdName,
    string? FamilyName,
    string Gender,
    string? NationalId,
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
    string? FirstName,
    string? SecondName,
    string? ThirdName,
    string? FamilyName,
    Gender Gender,
    string? NationalId,
    string? JobTitle,
    ConfidentialityLevel Clearance,
    long? OrgUnitId,
    IReadOnlyList<long> RoleIds);

public sealed record SetUserRolesRequest(IReadOnlyList<long> RoleIds);

public sealed record SetUserActiveRequest(bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);

// Role management
public sealed record CreateRoleRequest(string Name, string? Description);
public sealed record SetRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);
public sealed record RolePermissionsDto(long Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> PermissionCodes);
public sealed record PermissionInfoDto(string Code, string Resource, string Action);
