using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Users;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(
    IUserAdminService service, IOnlineUserTracker online,
    IAccessScope scope, ICurrentUser currentUser, AppDbContext db) : ControllerBase
{
    /// <summary>Who is signed in right now. This is presence data about other people — names and
    /// last-seen times — so it needs the same permission as seeing the staff list at all. It was
    /// open to every authenticated caller and rendered on everyone's dashboard.</summary>
    [HttpGet("online")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> Online(CancellationToken ct)
    {
        var present = online.GetOnline();
        if (await scope.IsSystemAdminAsync(ct)) return Ok(present);

        // Presence is scoped like the staff list itself: a department head sees who in *their*
        // department is online, not a live roster of the institution.
        var depts = await scope.OrgUnitIdsAsync(ct);
        var ids = present.Select(u => u.Id).ToList();
        var mine = depts.Count == 0
            ? []
            : await db.Users.Where(u => ids.Contains(u.Id) && u.OrgUnitId != null && depts.Contains(u.OrgUnitId.Value))
                            .Select(u => u.Id).ToListAsync(ct);
        var visible = mine.ToHashSet();
        return Ok(present.Where(u => visible.Contains(u.Id) || u.Id == currentUser.UserId).ToList());
    }
    [HttpGet]
    [HasPermission("Users.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListUsersAsync(ct));

    [HttpGet("roles")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await service.ListRolesAsync(ct));

    [HttpPost]
    [HasPermission("Users.Create")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var r = await service.CreateUserAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}/roles")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetRoles(long id, [FromBody] SetUserRolesRequest req, CancellationToken ct)
    {
        var r = await service.SetRolesAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/active")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetActive(long id, [FromBody] SetUserActiveRequest req, CancellationToken ct)
    {
        var r = await service.SetActiveAsync(id, req.IsActive, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/reset-password")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var r = await service.ResetPasswordAsync(id, req.NewPassword, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    /// <summary>Put a manager in charge of departments beyond their own. The service refuses this
    /// for anyone but the System Administrator — the permission alone is not enough, because
    /// <c>Users.Edit</c> is exactly what a department head holds.</summary>
    [HttpPut("{id:long}/departments")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetDepartments(long id, [FromBody] SetUserDepartmentsRequest req, CancellationToken ct)
    {
        var r = await service.SetDepartmentsAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>What this person may do, split into what their roles carry and what was granted to
    /// them personally — plus which of it this caller may change.</summary>
    [HttpGet("{id:long}/permissions")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> GetUserPermissions(long id, CancellationToken ct)
    {
        var r = await service.GetUserPermissionsAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Add or remove permissions for one person in the caller's own department. The service
    /// refuses anything the caller does not hold themselves, so <c>Users.Edit</c> alone is not a way
    /// to grow the rights of the department — only to spread the caller's own.</summary>
    [HttpPut("{id:long}/permissions")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetUserPermissions(long id, [FromBody] SetUserPermissionsRequest req, CancellationToken ct)
    {
        var r = await service.SetUserPermissionsAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Sidebar sections hidden for one person, plus the ones hidden institution-wide.</summary>
    [HttpGet("{id:long}/nav-sections")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> GetUserNavSections(long id, CancellationToken ct)
    {
        var r = await service.GetUserNavSectionsAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Hide or restore sections for one person in the caller's own department. Presentation
    /// only — what the API will answer is still decided by their permissions.</summary>
    [HttpPut("{id:long}/nav-sections")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetUserNavSections(long id, [FromBody] SetUserNavSectionsRequest req, CancellationToken ct)
    {
        var r = await service.SetUserNavSectionsAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ── Role management ────────────────────────────────────────────────────

    [HttpGet("permissions")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> Permissions(CancellationToken ct)
        => Ok(await service.ListPermissionsAsync(ct));

    [HttpGet("roles/{roleId:long}/permissions")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> GetRolePermissions(long roleId, CancellationToken ct)
    {
        var r = await service.GetRolePermissionsAsync(roleId, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPut("roles/{roleId:long}/permissions")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> SetRolePermissions(long roleId, [FromBody] SetRolePermissionsRequest req, CancellationToken ct)
    {
        var r = await service.SetRolePermissionsAsync(roleId, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("roles/{roleId:long}/permissions/reset")]
    [HasPermission("Users.Edit")]
    public async Task<IActionResult> ResetRolePermissions(long roleId, CancellationToken ct)
    {
        var r = await service.ResetRolePermissionsAsync(roleId, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("roles")]
    [HasPermission("Users.Create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req, CancellationToken ct)
    {
        var r = await service.CreateRoleAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("roles/{roleId:long}")]
    [HasPermission("Users.Delete")]
    public async Task<IActionResult> DeleteRole(long roleId, CancellationToken ct)
    {
        var r = await service.DeleteRoleAsync(roleId, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}
