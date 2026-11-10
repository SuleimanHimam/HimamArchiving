using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MediatR;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>2.6 — on workflow rejection, notify the linked document's creator.</summary>
public sealed class WorkflowRejectionHandler(
    AppDbContext db, ILogger<WorkflowRejectionHandler> log)
    : INotificationHandler<WorkflowRejectedEvent>
{
    public async Task Handle(WorkflowRejectedEvent e, CancellationToken ct)
    {
        if (e.DocumentId is not { } documentId) return;
        try
        {
            var doc = await db.Documents.IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == documentId, ct);
            if (doc?.CreatedBy is not { } creator) return;

            db.Notifications.Add(new Notification
            {
                RecipientUserId = creator,
                Title = "تم رفض سير عمل على وثيقتك",
                Body = string.IsNullOrWhiteSpace(e.Reason) ? "تمت إعادة الوثيقة." : $"السبب: {e.Reason}",
                Type = NotificationType.Warning,
                EntityType = "Document",
                EntityId = documentId,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Rejection notification failed for document {DocumentId}", documentId); }
    }
}
