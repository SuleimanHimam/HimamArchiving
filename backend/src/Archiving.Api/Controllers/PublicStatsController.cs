using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record PublicStatsDto(int TodayTransactions, int PendingApproval, int Overdue);

/// <summary>Anonymous, aggregate-only counters shown on the login screen — no titles or content, just totals.</summary>
[ApiController]
[Route("api/public/stats")]
[AllowAnonymous]
public sealed class PublicStatsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var todayIncoming  = await db.IncomingMails.CountAsync(m => m.CreatedAt >= todayStart && m.CreatedAt < tomorrowStart, ct);
        var todayOutgoing  = await db.OutgoingMails.CountAsync(m => m.CreatedAt >= todayStart && m.CreatedAt < tomorrowStart, ct);
        var todayDocuments = await db.Documents.CountAsync(d => d.CreatedAt >= todayStart && d.CreatedAt < tomorrowStart, ct);

        var pendingApproval = await db.OutgoingMails.CountAsync(m => m.Status == OutgoingMailStatus.PendingApproval, ct);

        var openTaskStatuses = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Escalated };
        var overdue = await db.WorkflowTasks.CountAsync(t => openTaskStatuses.Contains(t.Status) && t.DueAt < DateTime.UtcNow, ct);

        return Ok(new PublicStatsDto(
            TodayTransactions: todayIncoming + todayOutgoing + todayDocuments,
            PendingApproval: pendingApproval,
            Overdue: overdue));
    }
}
