using Archiving.Api.Authorization;
using Archiving.Application.Features.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers.Core;

// Every action declares its permission explicitly; CorePermissionTests fails the build otherwise.

[ApiController]
[Route("api/core/lookups")]
[Authorize]
public sealed class LookupsController(ILookupService lookups) : ControllerBase
{
    [HttpGet]
    [HasPermission("Lookups.View")]
    public async Task<IActionResult> List([FromQuery] string? category, [FromQuery] bool activeOnly, CancellationToken ct) =>
        Ok(await lookups.ListAsync(category, activeOnly, ct));

    [HttpPost]
    [HasPermission("Lookups.Edit")]
    public async Task<IActionResult> Create([FromBody] SaveLookupRequest request, CancellationToken ct)
    {
        var r = await lookups.CreateAsync(request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Lookups.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveLookupRequest request, CancellationToken ct)
    {
        var r = await lookups.UpdateAsync(id, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Lookups.Edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await lookups.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}

[ApiController]
[Route("api/core/approvals")]
[Authorize]
public sealed class ApprovalsController(IApprovalService approvals) : ControllerBase
{
    /// <summary>The caller's own requests (طلباتي).</summary>
    [HttpGet("mine")]
    [HasPermission("Approvals.View")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await approvals.ListMineAsync(ct));

    /// <summary>صندوق الموافقات: open approval tasks assigned to the caller's seat or account.
    /// Decisions post to <c>api/workflow/tasks/{taskId}/actions</c>, which re-checks ownership.</summary>
    [HttpGet("inbox")]
    [HasPermission("Approvals.View")]
    public async Task<IActionResult> Inbox(CancellationToken ct) => Ok(await approvals.InboxAsync(ct));
}
