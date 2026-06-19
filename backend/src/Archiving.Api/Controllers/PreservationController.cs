using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Preservation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Preservation configuration — the OAIS Designated Community (ISO 14721).</summary>
[ApiController]
[Route("api/preservation")]
[Authorize]
public sealed class PreservationController(
    IDesignatedCommunityService community,
    IPreservationPolicyService policy) : ControllerBase
{
    [HttpGet("designated-community")]
    [HasPermission("Preservation.View")]
    public async Task<IActionResult> GetCommunity(CancellationToken ct) => Ok(await community.GetAsync(ct));

    [HttpPut("designated-community")]
    [HasPermission("Preservation.Edit")]
    public async Task<IActionResult> UpdateCommunity([FromBody] DesignatedCommunityDto req, CancellationToken ct)
        => Ok(await community.UpdateAsync(req, ct));

    [HttpGet("policy")]
    [HasPermission("Preservation.View")]
    public async Task<IActionResult> GetPolicy(CancellationToken ct) => Ok(await policy.GetAsync(ct));

    [HttpPut("policy")]
    [HasPermission("Preservation.Edit")]
    public async Task<IActionResult> UpdatePolicy([FromBody] PreservationPolicyDto req, CancellationToken ct)
        => Ok(await policy.UpdateAsync(req, ct));
}
