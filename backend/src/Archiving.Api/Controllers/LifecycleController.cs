using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Lifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/lifecycle")]
[Authorize]
public sealed class LifecycleController(ILifecycleService service) : ControllerBase
{
    // ---- Retention policies ----
    [HttpGet("policies")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Policies(CancellationToken ct) => Ok(await service.ListPoliciesAsync(ct));

    [HttpPost("policies")]
    [HasPermission("Settings.Create")]
    public async Task<IActionResult> CreatePolicy([FromBody] CreateRetentionPolicyRequest req, CancellationToken ct)
    {
        var r = await service.CreatePolicyAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ---- Expiring documents ----
    [HttpGet("expiring")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Expiring([FromQuery] int withinDays = 30, CancellationToken ct = default)
        => Ok(await service.ExpiringAsync(withinDays, ct));

    // ---- Disposal flow ----
    [HttpGet("disposal-requests")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> DisposalRequests(CancellationToken ct) => Ok(await service.ListDisposalRequestsAsync(ct));

    [HttpPost("disposal-requests")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> RequestDisposal([FromBody] CreateDisposalRequestRequest req, CancellationToken ct)
    {
        var r = await service.RequestDisposalAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("disposal-requests/{id:long}/decision")]
    [HasPermission("Documents.Approve")]
    public async Task<IActionResult> Decide(long id, [FromBody] DisposalDecisionRequest req, CancellationToken ct)
    {
        var r = await service.DecideAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("disposal-requests/{id:long}/execute")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Execute(long id, CancellationToken ct)
    {
        var r = await service.ExecuteAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }
}
