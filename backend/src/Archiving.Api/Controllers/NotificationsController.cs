using Archiving.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Each user's own notification feed — scoped to the caller, so no module permission is required.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        => Ok(await service.ListMineAsync(unreadOnly, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new { count = await service.UnreadCountAsync(ct) });

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        var r = await service.MarkReadAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => Ok(new { updated = (await service.MarkAllReadAsync(ct)).Value });
}
