using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.IncomingMail;
using Archiving.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/incoming-mail")]
[Authorize]
public sealed class IncomingMailController(
    IIncomingMailService service,
    IAuthorizationService authz) : ControllerBase
{
    [HttpGet]
    [HasPermission("IncomingMail.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] IncomingMailStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(new IncomingMailQuery(search, status, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("IncomingMail.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    [HasPermission("IncomingMail.Create")]
    public async Task<IActionResult> Create([FromBody] CreateIncomingMailRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:long}/actions")]
    public async Task<IActionResult> Act(long id, [FromBody] IncomingMailActionRequest request, CancellationToken ct)
    {
        // Each routing action maps to its own RBAC permission.
        var permission = request.Action switch
        {
            IncomingMailActionType.Forward => "IncomingMail.Forward",
            IncomingMailActionType.Approve => "IncomingMail.Approve",
            IncomingMailActionType.Archive => "IncomingMail.Archive",
            _ => "IncomingMail.Edit", // Hold / Close
        };

        var check = await authz.AuthorizeAsync(User, null, new PermissionRequirement(permission));
        if (!check.Succeeded) return Forbid();

        var result = await service.ActAsync(id, request, ct);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
