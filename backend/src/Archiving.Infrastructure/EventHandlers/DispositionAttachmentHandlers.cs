using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>2.7 — when a disposition is approved, notify everyone the document was shared with.
/// (The physical-slot release is already performed inside the disposition service.)</summary>
public sealed class DispositionCompletionHandler(
    AppDbContext db, ILogger<DispositionCompletionHandler> log)
    : INotificationHandler<DispositionApprovedEvent>
{
    public async Task Handle(DispositionApprovedEvent e, CancellationToken ct)
    {
        try
        {
            var sharedWith = await db.DocumentShares
                .Where(s => s.DocumentId == e.DocumentId)
                .Select(s => s.SharedWithUserId).Distinct().ToListAsync(ct);
            if (sharedWith.Count == 0) return;

            var number = await db.Documents.IgnoreQueryFilters()
                .Where(d => d.Id == e.DocumentId).Select(d => d.DocumentNumber).FirstOrDefaultAsync(ct);

            foreach (var userId in sharedWith)
                db.Notifications.Add(new Notification
                {
                    RecipientUserId = userId,
                    Title = "تمت الموافقة على التصرّف بوثيقة مشتركة معك",
                    Body = $"الوثيقة {number} خضعت لإجراء تصرّف (إتلاف/تجديد).",
                    Type = NotificationType.Warning,
                    EntityType = "Document",
                    EntityId = e.DocumentId,
                });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Disposition completion notify failed for document {DocumentId}", e.DocumentId); }
    }
}

/// <summary>2.8 — when an attachment is uploaded, expedite the extraction pipeline (OCR/PDF text → fixity →
/// PDF/A) instead of waiting for the periodic background sweep. Best-effort; the background worker remains
/// the safety net.</summary>
public sealed class AttachmentProcessingPipelineHandler(
    ITextIndexingService indexing, ILogger<AttachmentProcessingPipelineHandler> log)
    : INotificationHandler<AttachmentUploadedEvent>
{
    public async Task Handle(AttachmentUploadedEvent e, CancellationToken ct)
    {
        try { await indexing.SweepPendingAsync(batch: 5, ct); }
        catch (Exception ex) { log.LogWarning(ex, "Expedited indexing failed for attachment {AttachmentId}", e.AttachmentId); }
    }
}
