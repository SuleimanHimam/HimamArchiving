using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Fixity (file integrity) verification — ISO 16363.</summary>
[ApiController]
[Route("api/fixity")]
[Authorize]
public sealed class FixityController(IFixityService service) : ControllerBase
{
    /// <summary>Re-verify a single attachment against its ingest checksum.</summary>
    [HttpPost("verify/{attachmentId:long}")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Verify(long attachmentId, CancellationToken ct)
    {
        var r = await service.VerifyAttachmentAsync(attachmentId, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Recent fixity checks (newest first).</summary>
    [HttpGet("checks")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Checks([FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await service.RecentChecksAsync(take, ct));

    /// <summary>Run a fixity sweep now over the least-recently-checked files; returns failures found.</summary>
    [HttpPost("sweep")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Sweep([FromQuery] int max = 50, CancellationToken ct = default)
        => Ok(new { failures = await service.SweepAsync(max, ct) });
}
