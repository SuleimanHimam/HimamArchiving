using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.OutgoingMail;
using Archiving.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/outgoing-mail")]
[Authorize]
public sealed class OutgoingMailController(
    IOutgoingMailService service,
    IAuthorizationService authz) : ControllerBase
{
    [HttpGet]
    [HasPermission("OutgoingMail.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] OutgoingMailStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(new OutgoingMailQuery(search, status, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("OutgoingMail.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await service.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost]
    [HasPermission("OutgoingMail.Create")]
    public async Task<IActionResult> Create([FromBody] CreateOutgoingMailRequest req, CancellationToken ct)
    {
        var r = await service.CreateAsync(req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value)
            : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("OutgoingMail.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateOutgoingMailRequest req, CancellationToken ct)
    {
        var r = await service.UpdateAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/actions")]
    public async Task<IActionResult> Act(long id, [FromBody] OutgoingMailActionRequest req, CancellationToken ct)
    {
        // Approval requires the Approve permission; dispatch maps to Archive (it auto-archives); the rest to Edit.
        var permission = req.Action switch
        {
            OutgoingMailActionType.Approve => "OutgoingMail.Approve",
            OutgoingMailActionType.Send => "OutgoingMail.Archive",
            _ => "OutgoingMail.Edit",
        };

        var check = await authz.AuthorizeAsync(User, null, new PermissionRequirement(permission));
        if (!check.Succeeded) return Forbid();

        var r = await service.ActAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }
}
