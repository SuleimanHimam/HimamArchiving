using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(IUserAdminService service, IOnlineUserTracker online) : ControllerBase
{
    [HttpGet("online")]
    public IActionResult Online() => Ok(online.GetOnline());
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
