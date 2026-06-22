using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Disposition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/disposition-requests")]
[Authorize]
public sealed class DispositionRequestsController(IDispositionService svc) : ControllerBase
{
    /// <summary>Queues: ?stage=Verification (Records Officer) or ?stage=FinalApproval (Legal/Dept Head).</summary>
    [HttpGet]
    [HasPermission("Disposition.View")]
    public async Task<IActionResult> List([FromQuery] string? stage = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await svc.ListAsync(stage, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [HasPermission("Disposition.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await svc.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost]
    [HasPermission("Disposition.Create")]
    public async Task<IActionResult> Create([FromBody] CreateDispositionRequest req, CancellationToken ct)
    {
        var r = await svc.CreateAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Step 1 — Records Officer. Body decision: "Verify" | "Reject".</summary>
    [HttpPost("{id:long}/verify")]
    [HasPermission("Disposition.Edit")]
    public async Task<IActionResult> Verify(long id, [FromBody] VerifyDispositionRequest req, CancellationToken ct)
    {
        var r = await svc.VerifyAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Step 2 — Legal / Department Head. Body decision: "Approve" | "Reject".</summary>
    [HttpPost("{id:long}/final-approve")]
    [HasPermission("Disposition.Approve")]
    public async Task<IActionResult> FinalApprove(long id, [FromBody] FinalApproveDispositionRequest req, CancellationToken ct)
    {
        var r = await svc.FinalApproveAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/reject")]
    [HasPermission("Disposition.Edit")]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectDispositionRequest req, CancellationToken ct)
    {
        var r = await svc.RejectAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Certificate of Destruction data (the UI renders/prints it to PDF).</summary>
    [HttpGet("{id:long}/certificate")]
    [HasPermission("Disposition.View")]
    public async Task<IActionResult> Certificate(long id, CancellationToken ct)
    {
        var r = await svc.GetCertificateAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }
}
