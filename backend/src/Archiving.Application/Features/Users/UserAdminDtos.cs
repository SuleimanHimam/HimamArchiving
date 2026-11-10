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
    IReadOnlyList<long> RoleIds,
    /// <summary>The department the user is filed under — where their records are created.</summary>
    long? OrgUnitId = null,
    string? OrgUnitName = null,
    /// <summary>Further departments the System Administrator has put this user in charge of,
    /// beyond <see cref="OrgUnitId"/>. Empty for everyone who covers only their own.</summary>
    IReadOnlyList<long>? ExtraOrgUnitIds = null,
    IReadOnlyList<string>? ExtraOrgUnitNames = null);

/// <summary>The extra departments a user may act in. Replaces the whole set, so an empty list
/// takes every grant away.</summary>
public sealed record SetUserDepartmentsRequest(IReadOnlyList<long> OrgUnitIds, string? Note = null);

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

/// <summary>What one person may do, split by where it comes from — so the screen can show that a
/// permission is part of being a «موظف» (and not the head's to take away) rather than an exception
/// somebody granted.</summary>
public sealed record UserPermissionsDto(
    long UserId,
    string FullName,
    /// <summary>Codes the person's roles carry. Fixed for everyone holding those roles.</summary>
    IReadOnlyList<string> RolePermissionCodes,
    /// <summary>Codes granted to this person alone, on top of their roles.</summary>
    IReadOnlyList<string> ExtraPermissionCodes,
    /// <summary>Codes the *caller* may add or remove here. A head may only pass on what they hold
    /// themselves, so this is smaller for them than for the administrator — the screen greys out the
    /// rest rather than letting someone discover the limit by being refused.</summary>
    IReadOnlyList<string> GrantableCodes);

/// <summary>Replace the set of permissions granted to this person directly. Codes their roles
/// already carry are ignored — those are not this screen's to give or take.</summary>
public sealed record SetUserPermissionsRequest(IReadOnlyList<string> PermissionCodes, string? Note = null);

/// <summary>Which sidebar sections this person does not see, and which are already hidden for
/// everybody — the two are shown apart so a head can tell "I hid this" from "nobody has this".</summary>
public sealed record UserNavSectionsDto(
    long UserId,
    string FullName,
    /// <summary>Hidden for this person alone.</summary>
    IReadOnlyList<string> Hidden,
    /// <summary>Hidden institution-wide by the administrator. Not this screen's to change, and
    /// shown so a head does not try to "unhide" something no one can see.</summary>
    IReadOnlyList<string> HiddenForEveryone);

public sealed record SetUserNavSectionsRequest(IReadOnlyList<string> Hidden);
