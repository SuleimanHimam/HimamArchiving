using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Audit-trail integrity — ISO 15489 / 16363 tamper-evidence.</summary>
[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController(IAuditVerificationService service) : ControllerBase
{
    /// <summary>Verifies the audit hash chain end-to-end; reports the first broken entry if any.</summary>
    [HttpGet("verify")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Verify(CancellationToken ct) => Ok(await service.VerifyChainAsync(ct));

    /// <summary>One-time baseline reseal of the audit chain (e.g. after a hashing-scheme correction).
    /// Admin-gated and itself recorded in the audit log.</summary>
    [HttpPost("reseal")]
    [HasPermission("Audit.Edit")]
    public async Task<IActionResult> Reseal(CancellationToken ct)
        => Ok(new { resealed = await service.ResealAsync(ct) });
}
