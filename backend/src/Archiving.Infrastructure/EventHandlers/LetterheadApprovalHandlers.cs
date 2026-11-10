using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>
/// Publishes a department letterhead once its approval run completes.
///
/// The workflow events are Document-shaped (<c>WorkflowApprovedEvent.DocumentId</c> is null for
/// anything else), so the instance is resolved by id and anything that is not a letterhead run is
/// ignored. That keeps the generic engine untouched — no new event type, no changed payload.
/// </summary>
public sealed class LetterheadApprovedHandler(AppDbContext db, ILogger<LetterheadApprovedHandler> log)
    : INotificationHandler<WorkflowApprovedEvent>
{
    public async Task Handle(WorkflowApprovedEvent e, CancellationToken ct)
    {
        var template = await LetterheadWorkflow.FindTemplateAsync(db, e.InstanceId, ct);
        if (template is null) return;

        template.Status = LetterheadStatus.Active;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Letterhead {TemplateId} approved and is now live", template.Id);
    }
}

/// <summary>The mirror case: a rejected edit must not linger as "pending" forever, and the
/// previously approved slots stay on paper untouched.</summary>
public sealed class LetterheadRejectedHandler(AppDbContext db, ILogger<LetterheadRejectedHandler> log)
    : INotificationHandler<WorkflowRejectedEvent>
{
    public async Task Handle(WorkflowRejectedEvent e, CancellationToken ct)
    {
        var template = await LetterheadWorkflow.FindTemplateAsync(db, e.InstanceId, ct);
        if (template is null) return;

        template.Status = LetterheadStatus.Rejected;
        template.ReviewNote = string.IsNullOrWhiteSpace(e.Reason) ? "رُفض التعديل" : e.Reason;

        if (template.OrgUnitId is { } unitId)
        {
            db.Notifications.Add(new Domain.Entities.Notification
            {
                RecipientUserId = template.CreatedBy ?? 0,
                Title = "رُفض تعديل ترويسة القسم",
                Body = string.IsNullOrWhiteSpace(e.Reason) ? "تمت إعادة التعديل دون اعتماد." : $"السبب: {e.Reason}",
                Type = NotificationType.Warning,
                EntityType = LetterheadService.WorkflowEntityType,
                EntityId = template.Id,
            });
            log.LogInformation("Letterhead {TemplateId} for unit {UnitId} rejected", template.Id, unitId);
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Shared lookup for the two handlers and the self-approval guard.</summary>
internal static class LetterheadWorkflow
{
    public static async Task<Domain.Entities.ReportTemplate?> FindTemplateAsync(
        AppDbContext db, long instanceId, CancellationToken ct)
    {
        var instance = await db.WorkflowInstances
            .Where(i => i.Id == instanceId)
            .Select(i => new { i.EntityType, i.EntityId })
            .FirstOrDefaultAsync(ct);

        if (instance is null || instance.EntityType != LetterheadService.WorkflowEntityType) return null;
        return await db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == instance.EntityId, ct);
    }
}
