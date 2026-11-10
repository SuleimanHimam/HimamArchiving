using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public sealed class GroupsController(IGroupService service) : ControllerBase
{
    // Any authenticated user may list/read groups (needed when composing internal mail).
    // memberOnly=true narrows it to the groups the caller belongs to — what the compose screen
    // asks for, since a group may only be addressed by someone inside it.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false,
        [FromQuery] bool memberOnly = false, CancellationToken ct = default)
        => Ok(await service.ListAsync(activeOnly, memberOnly, ct));

    /// <summary>Which groups a given user belongs to — for the user-administration screen, where
    /// "who is in what" is otherwise only visible by opening every group in turn. Reading another
    /// user's membership needs Users.View; your own is available via ?memberOnly=true above.</summary>
    [HttpGet("of-user/{userId:long}")]
    [HasPermission("Users.View")]
    public async Task<IActionResult> OfUser(long userId, CancellationToken ct)
        => Ok(await service.ListForUserAsync(userId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await service.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> Create([FromBody] SaveGroupRequest req, CancellationToken ct)
    {
        var r = await service.CreateAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveGroupRequest req, CancellationToken ct)
    {
        var r = await service.UpdateAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Organization.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await service.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}
