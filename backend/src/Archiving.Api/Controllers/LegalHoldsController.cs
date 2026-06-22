using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Destruction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/legal-holds")]
[Authorize]
public sealed class LegalHoldsController(ILegalHoldService svc) : ControllerBase
{
    [HttpGet]
    [HasPermission("LegalHold.View")]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await svc.ListAsync(activeOnly, ct));

    [HttpPost]
    [HasPermission("LegalHold.Edit")]
    public async Task<IActionResult> Place([FromBody] PlaceLegalHoldRequest req, CancellationToken ct)
    {
        var r = await svc.PlaceAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/release")]
    [HasPermission("LegalHold.Edit")]
    public async Task<IActionResult> Release(long id, CancellationToken ct)
    {
        var r = await svc.ReleaseAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}
