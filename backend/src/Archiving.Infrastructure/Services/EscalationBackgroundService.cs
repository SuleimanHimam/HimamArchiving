using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Periodically escalates overdue work. For workflow tasks it uses the configurable, multi-level
/// <see cref="EscalationRule"/> engine (Phase 6) when rules exist, falling back to a single-level escalation
/// otherwise. It also flags overdue correspondence (publishing <see cref="CorrespondenceOverdueEvent"/>).
/// Sweep cadence via <c>Escalation:IntervalSeconds</c> (default 300).</summary>
public sealed class EscalationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<EscalationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Escalation:IntervalSeconds") ?? 300;
        var interval = TimeSpan.FromSeconds(Math.Max(10, seconds));
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await EscalateOverdueWorkflowAsync(stoppingToken);
                await NotifyUnansweredCorrespondenceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Escalation sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EscalateOverdueWorkflowAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var now = DateTime.UtcNow;

        var rules = await db.EscalationRules.Where(r => r.IsActive).ToListAsync(ct);

        var open = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress };
        var overdue = await db.WorkflowTasks
            .Include(t => t.Stage)
            .Include(t => t.WorkflowInstance)
            .Where(t => open.Contains(t.Status) && t.DueAt < now)
            .ToListAsync(ct);
        if (overdue.Count == 0) return;

        /* Never chase anyone about a document that no longer exists. Deleting one now cancels its
           workflows (DocumentService.DeleteAsync), but instances orphaned before that still sit in
           the table, and a sweep that only asks "is this task open" would keep escalating them
           forever — each notification a dead link. The query filter hides soft-deleted rows, so a
           document that does not come back is one that is gone. */
        var documentIds = overdue.Where(t => t.WorkflowInstance.EntityType == "Document")
            .Select(t => t.WorkflowInstance.EntityId).Distinct().ToList();
        if (documentIds.Count > 0)
        {
            var alive = (await db.Documents.Where(d => documentIds.Contains(d.Id))
                .Select(d => d.Id).ToListAsync(ct)).ToHashSet();
            overdue = overdue
                .Where(t => t.WorkflowInstance.EntityType != "Document" || alive.Contains(t.WorkflowInstance.EntityId))
                .ToList();
            if (overdue.Count == 0) return;
        }

        var escalatedCount = 0;
        foreach (var task in overdue)
        {
            var defId = task.WorkflowInstance.WorkflowDefinitionId;
            var matching = rules
                .Where(r => (r.WorkflowDefinitionId == null || r.WorkflowDefinitionId == defId)
                         && (r.WorkflowStageId == null || r.WorkflowStageId == task.StageId))
                .OrderBy(r => r.EscalationLevel)
                .ToList();

            if (matching.Count == 0)
            {
                // No rules configured → single-level fallback (preserves prior behavior).
                if (!task.IsEscalated) { await FallbackEscalateAsync(db, audit, task, now, ct); escalatedCount++; }
                continue;
            }

            var overdueMinutes = (int)(now - task.DueAt).TotalMinutes;
            var firedLevels = await db.EscalationLogs
                .Where(l => l.WorkflowInstanceId == task.WorkflowInstanceId && l.WorkflowStageId == task.StageId)
                .Select(l => l.EscalationLevel).ToListAsync(ct);

            var nextRule = matching.FirstOrDefault(r => !firedLevels.Contains(r.EscalationLevel) && overdueMinutes >= r.OverdueMinutes);
            if (nextRule is null) continue;

            var target = nextRule.EscalateToPositionId ?? await ManagerOfAssigneeAsync(db, task, ct);
            if (target is { } pos)
            {
                if (nextRule.Action is EscalationAction.ReassignTask or EscalationAction.NotifyAndReassign)
                {
                    task.AssignedToPositionId = pos;
                    task.AssignedToUserId = await OccupantAsync(db, pos, ct);
                    task.WorkflowInstance.CurrentAssigneePositionId = pos;
                    task.IsEscalated = true;
                    task.EscalatedAt = now;
                }
                if (nextRule.Action is EscalationAction.NotifyOnly or EscalationAction.NotifyAndReassign)
                    await NotifyPositionAsync(db, pos, task, ct);
            }

            if (nextRule.NotifyOriginator && task.WorkflowInstance.InitiatedByUserId is { } originator)
                db.Notifications.Add(new Notification
                {
                    RecipientUserId = originator,
                    Title = $"تصعيد على معاملتك: {task.Stage.Name}",
                    Body = $"تجاوزت المهمة المدة المحددة (المستوى {nextRule.EscalationLevel}).",
                    Type = NotificationType.Warning,
                    EntityType = task.WorkflowInstance.EntityType,
                    EntityId = task.WorkflowInstance.EntityId,
                    IsEscalation = true,
                });

            db.EscalationLogs.Add(new EscalationLog
            {
                WorkflowInstanceId = task.WorkflowInstanceId,
                WorkflowStageId = task.StageId,
                EscalationRuleId = nextRule.Id,
                EscalationLevel = nextRule.EscalationLevel,
                EscalatedToPositionId = target ?? 0,
                ActionTaken = nextRule.Action,
                EscalatedAt = now,
            });

            // Fires the SLA-breach domain event (logged + available to webhooks).
            task.WorkflowInstance.AddDomainEvent(new WorkflowSLABreachedEvent(
                task.WorkflowInstanceId, task.StageId, target ?? 0, task.DueAt));

            await audit.WriteAsync("Escalated", task.WorkflowInstance.EntityType, task.WorkflowInstance.EntityId,
                task.Stage.Name, newValues: $"level={nextRule.EscalationLevel}; to={target?.ToString() ?? "(unresolved)"}", ct: ct);
            escalatedCount++;
        }

        await db.SaveChangesAsync(ct);
        if (escalatedCount > 0) logger.LogInformation("Escalated {Count} overdue workflow task(s)", escalatedCount);
    }

    /// <summary>Reminds the registrar when a correspondence flagged <c>RemindOnNoReply</c> passes its
    /// response deadline without being answered. Fires once per item (guarded by <c>NoReplyNotifiedAt</c>)
    /// and marks the item Overdue.</summary>
    private async Task NotifyUnansweredCorrespondenceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var now = DateTime.UtcNow;

        var closed = new[] { CorrespondenceStatus.Replied, CorrespondenceStatus.Closed, CorrespondenceStatus.Archived };
        var flagged = 0;

        // الديوان — external register entries awaiting a reply.
        var diwan = await db.DiwanMails
            .Where(x => x.RemindOnNoReply && x.NoReplyNotifiedAt == null
                     && x.ResponseDeadline != null && x.ResponseDeadline < now
                     && !closed.Contains(x.Status))
            .ToListAsync(ct);
        foreach (var m in diwan)
        {
            m.Status = CorrespondenceStatus.Overdue;
            m.NoReplyNotifiedAt = now;
            AddReminder(db, m.CreatedBy, m.Subject, m.RegisterNumber, "DiwanMail", m.Id);
            await audit.WriteAsync("NoReplyReminder", "DiwanMail", m.Id, m.Subject,
                newValues: $"deadline={m.ResponseDeadline:o}", ct: ct);
            flagged++;
        }

        // المراسلات — internal messages awaiting a reply.
        var internals = await db.InternalMails
            .Where(x => x.RemindOnNoReply && x.NoReplyNotifiedAt == null
                     && x.ResponseDeadline != null && x.ResponseDeadline < now
                     && !closed.Contains(x.Status))
            .ToListAsync(ct);
        foreach (var m in internals)
        {
            m.Status = CorrespondenceStatus.Overdue;
            m.NoReplyNotifiedAt = now;
            AddReminder(db, m.SenderUserId, m.Subject, m.MailNumber, "InternalMail", m.Id);
            await audit.WriteAsync("NoReplyReminder", "InternalMail", m.Id, m.Subject,
                newValues: $"deadline={m.ResponseDeadline:o}", ct: ct);
            flagged++;
        }

        if (flagged == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Flagged {Count} unanswered mail item(s)", flagged);
    }

    private static void AddReminder(AppDbContext db, long? ownerUserId, string subject, string number, string entityType, long entityId)
    {
        if (ownerUserId is not { } owner) return;
        db.Notifications.Add(new Notification
        {
            RecipientUserId = owner,
            Title = $"لم يُرَد على المراسلة: {subject}",
            Body = $"تجاوزت المراسلة رقم {number} موعد الرد المحدد دون استجابة.",
            Type = NotificationType.Warning,
            Channels = NotificationChannel.InApp,
            EntityType = entityType,
            EntityId = entityId,
            IsEscalation = true,
        });
    }

    // ---- helpers ----

    private static async Task FallbackEscalateAsync(AppDbContext db, IAuditWriter audit, WorkflowTask task, DateTime now, CancellationToken ct)
    {
        var target = task.Stage.EscalateToPositionId ?? await ManagerOfAssigneeAsync(db, task, ct);
        task.Status = WorkflowTaskStatus.Escalated;
        task.IsEscalated = true;
        task.EscalatedAt = now;
        if (target is { } pos)
        {
            task.AssignedToPositionId = pos;
            task.AssignedToUserId = await OccupantAsync(db, pos, ct);
            task.WorkflowInstance.CurrentAssigneePositionId = pos;
            await NotifyPositionAsync(db, pos, task, ct);
        }
        await audit.WriteAsync("Escalated", task.WorkflowInstance.EntityType, task.WorkflowInstance.EntityId,
            task.Stage.Name, newValues: $"SLA breach; escalated to position {target?.ToString() ?? "(unresolved)"}", ct: ct);
    }

    private static async Task<long?> ManagerOfAssigneeAsync(AppDbContext db, WorkflowTask task, CancellationToken ct)
    {
        if (task.AssignedToPositionId is not { } assignedPos) return null;
        var orgUnitId = await db.Positions.Where(p => p.Id == assignedPos).Select(p => (long?)p.OrgUnitId).FirstOrDefaultAsync(ct);
        if (orgUnitId is null) return null;
        return await db.OrgUnits.Where(u => u.Id == orgUnitId).Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct);
    }

    private static Task<long?> OccupantAsync(AppDbContext db, long positionId, CancellationToken ct) =>
        db.Positions.Where(p => p.Id == positionId).Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);

    private static async Task NotifyPositionAsync(AppDbContext db, long positionId, WorkflowTask task, CancellationToken ct)
    {
        if (await OccupantAsync(db, positionId, ct) is not { } uid) return;
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
}
