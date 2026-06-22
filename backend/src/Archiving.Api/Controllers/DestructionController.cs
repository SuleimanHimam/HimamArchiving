using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Destruction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Controlled record destruction (disposition). Phase 1: request lifecycle + eligibility.
/// Execution (the irreversible step, MFA step-up) is delivered in a later phase.</summary>
[ApiController]
[Route("api/destruction")]
[Authorize]
public sealed class DestructionController(
    IDestructionService svc, IDestructionEligibilityService eligibility, ICertificateService certificates) : ControllerBase
{
    [HttpGet("requests")]
    [HasPermission("Destruction.View")]
    public async Task<IActionResult> List([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await svc.ListAsync(new DestructionRequestQuery(status, page, pageSize), ct));

    [HttpGet("requests/{id:long}")]
    [HasPermission("Destruction.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await svc.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost("requests")]
    [HasPermission("Destruction.Create")]
    public async Task<IActionResult> Create([FromBody] CreateDestructionRequest req, CancellationToken ct)
    {
        var r = await svc.CreateAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("requests/{id:long}/submit")]
    [HasPermission("Destruction.Create")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        var r = await svc.SubmitAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("requests/{id:long}/approve")]
    [HasPermission("Destruction.Approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] DestructionDecisionRequest req, CancellationToken ct)
    {
        var r = await svc.ApproveAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("requests/{id:long}/reject")]
    [HasPermission("Destruction.Approve")]
    public async Task<IActionResult> Reject(long id, [FromBody] DestructionDecisionRequest req, CancellationToken ct)
    {
        var r = await svc.RejectAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("requests/{id:long}/cancel")]
    [HasPermission("Destruction.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var r = await svc.CancelAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("requests/{id:long}/execute")]
    [HasPermission("Destruction.Delete")]
    public async Task<IActionResult> Execute(long id, [FromBody] ExecuteDestructionRequest req, CancellationToken ct)
    {
        // A manager/admin authorized to approve destruction may destroy directly (waives the two-person rule).
        var canOverride = User.IsInRole("System Administrator") || User.HasClaim("permission", "Destruction.Approve");
        var r = await svc.ExecuteAsync(id, req, canOverride, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("requests/{id:long}/certificate")]
    [HasPermission("Destruction.View")]
    public async Task<IActionResult> Certificate(long id, CancellationToken ct)
    {
        var cert = await certificates.OpenAsync(id, ct);
        if (cert is null) return NotFound(new { error = "لا توجد شهادة لهذا الطلب" });
        return File(cert.Value.Stream, "application/pdf", cert.Value.FileName);
    }

    [HttpGet("eligibility/{documentId:long}")]
    [HasPermission("Destruction.View")]
    public async Task<IActionResult> Eligibility(long documentId, CancellationToken ct)
        => Ok(await eligibility.CheckAsync(documentId, ct));
}
