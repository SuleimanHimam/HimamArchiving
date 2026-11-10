using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Delegation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Temporary position delegations (substitution during absence).</summary>
[ApiController]
[Route("api/delegations")]
[Authorize]
public sealed class DelegationsController(IDelegationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDelegationDto dto, CancellationToken ct)
    {
        var r = await service.CreateDelegationAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
    {
        var r = await service.RevokeDelegationAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    [HttpGet("from-position/{positionId:long}")]
    public async Task<IActionResult> FromPosition(long positionId, CancellationToken ct)
        => Ok(await service.GetDelegationsFromPositionAsync(positionId, ct));

    [HttpGet("to-position/{positionId:long}")]
    public async Task<IActionResult> ToPosition(long positionId, CancellationToken ct)
        => Ok(await service.GetDelegationsToPositionAsync(positionId, ct));

    [HttpGet("my")]
    public async Task<IActionResult> My(CancellationToken ct)
        => Ok(await service.GetMyDelegationsAsync(ct));
}
