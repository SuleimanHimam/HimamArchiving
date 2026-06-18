using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Periodically scans for overdue workflow tasks (SLA breaches) and escalates them:
/// reassigns to the stage's escalation seat (or the unit's manager), flags the task, and notifies.
/// Sweep cadence is configurable via <c>Escalation:IntervalSeconds</c> (default 300).</summary>
public sealed class EscalationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<EscalationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Escalation:IntervalSeconds") ?? 300;
        var interval = TimeSpan.FromSeconds(Math.Max(10, seconds));

        // Small initial delay so the app finishes migrating/seeding first.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await EscalateOverdueAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Escalation sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EscalateOverdueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var now = DateTime.UtcNow;

        var open = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress };
        var overdue = await db.WorkflowTasks
            .Include(t => t.Stage)
            .Include(t => t.WorkflowInstance)
            .Where(t => open.Contains(t.Status) && !t.IsEscalated && t.DueAt < now)
            .ToListAsync(ct);

        if (overdue.Count == 0) return;

        foreach (var task in overdue)
        {
            var target = await ResolveEscalationPositionAsync(db, task, ct);

            task.Status = WorkflowTaskStatus.Escalated;
            task.IsEscalated = true;
            task.EscalatedAt = now;

            if (target is { } pos)
            {
                task.AssignedToPositionId = pos;
                var occupant = await db.Positions.Where(p => p.Id == pos)
                    .Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
                task.AssignedToUserId = occupant;
                task.WorkflowInstance.CurrentAssigneePositionId = pos;

                if (occupant is { } uid)
                    db.Notifications.Add(new Notification
                    {
                        RecipientUserId = uid,
                        Title = $"تصعيد مهمة متأخرة: {task.Stage.Name}",
                        Body = "تم تصعيد مهمة تجاوزت المدة المحددة إلى مكتبك.",
                        Type = NotificationType.Escalation,
                        Channels = NotificationChannel.InApp,
                        EntityType = task.WorkflowInstance.EntityType,
                        EntityId = task.WorkflowInstance.EntityId,
                        IsEscalation = true,
                    });
            }

            await audit.WriteAsync("Escalated", task.WorkflowInstance.EntityType,
                task.WorkflowInstance.EntityId, task.Stage.Name,
                newValues: $"SLA breach; escalated to position {target?.ToString() ?? "(unresolved)"}", ct: ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Escalated {Count} overdue workflow task(s)", overdue.Count);
    }

    /// <summary>Escalation target: the stage's explicit seat, else the manager of the
    /// assignee position's org unit.</summary>
    private static async Task<long?> ResolveEscalationPositionAsync(AppDbContext db, WorkflowTask task, CancellationToken ct)
    {
        if (task.Stage.EscalateToPositionId is { } explicitPos) return explicitPos;
        if (task.AssignedToPositionId is not { } assignedPos) return null;

        var orgUnitId = await db.Positions.Where(p => p.Id == assignedPos)
            .Select(p => (long?)p.OrgUnitId).FirstOrDefaultAsync(ct);
        if (orgUnitId is null) return null;

        return await db.OrgUnits.Where(u => u.Id == orgUnitId)
            .Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct);
    }
}
