using Archiving.Application.Common.Models;
using Archiving.Application.Features.Users;

namespace Archiving.Application.Common.Interfaces;

public interface IUserAdminService
{
    Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken ct = default);
    Task<Result<AdminUserListItem>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<AdminUserListItem>> SetRolesAsync(long userId, SetUserRolesRequest request, CancellationToken ct = default);
    Task<Result<AdminUserListItem>> SetActiveAsync(long userId, bool isActive, CancellationToken ct = default);
    Task<Result<bool>> ResetPasswordAsync(long userId, string newPassword, CancellationToken ct = default);

    /// <summary>Grants a user extra departments to act in. System Administrator only.</summary>
    Task<Result<AdminUserListItem>> SetDepartmentsAsync(long userId, SetUserDepartmentsRequest request, CancellationToken ct = default);

    // Role management
    Task<IReadOnlyList<PermissionInfoDto>> ListPermissionsAsync(CancellationToken ct = default);

    // ---- Permissions given to one person, on top of their roles ----

    /// <summary>What this person may do and where each part of it comes from, plus what the caller
    /// is allowed to change here.</summary>
    Task<Result<UserPermissionsDto>> GetUserPermissionsAsync(long userId, CancellationToken ct = default);

    /// <summary>Replace this person's personal grants. Additive only — role permissions are not
    /// touched — and a caller may only grant what they hold themselves.</summary>
    Task<Result<UserPermissionsDto>> SetUserPermissionsAsync(
        long userId, SetUserPermissionsRequest request, CancellationToken ct = default);

    // ---- Sidebar sections hidden for one person ----

    Task<Result<UserNavSectionsDto>> GetUserNavSectionsAsync(long userId, CancellationToken ct = default);

    /// <summary>Replace the sections hidden for this person. Presentation only — a hidden section is
    /// still reachable by URL if their permissions allow it.</summary>
    Task<Result<UserNavSectionsDto>> SetUserNavSectionsAsync(
        long userId, SetUserNavSectionsRequest request, CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> GetRolePermissionsAsync(long roleId, CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> SetRolePermissionsAsync(long roleId, SetRolePermissionsRequest req, CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> ResetRolePermissionsAsync(long roleId, CancellationToken ct = default);
    Task<Result<AdminRoleDto>> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default);
    Task<Result<bool>> DeleteRoleAsync(long roleId, CancellationToken ct = default);
}
