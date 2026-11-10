using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MediatR;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>2.2 — when a document is created, auto-start the workflow configured as its type's default
/// (<see cref="Archiving.Domain.Entities.DocumentType.DefaultWorkflowDefinitionId"/>). Inert until an admin
/// configures a default workflow on the type, so it never disturbs existing flows.</summary>
public sealed class DocumentCreatedWorkflowHandler(
    AppDbContext db, IWorkflowService workflow, ILogger<DocumentCreatedWorkflowHandler> log)
    : INotificationHandler<DocumentCreatedEvent>
{
    public async Task Handle(DocumentCreatedEvent e, CancellationToken ct)
    {
        try
        {
            if (e.DocumentTypeId is not { } typeId) return;
            var defId = await db.DocumentTypes.Where(t => t.Id == typeId)
                .Select(t => t.DefaultWorkflowDefinitionId).FirstOrDefaultAsync(ct);
            if (defId is null or 0) return;

            var alreadyRunning = await db.WorkflowInstances.AnyAsync(
                w => w.EntityType == "Document" && w.EntityId == e.DocumentId && w.Status == WorkflowStatus.Running, ct);
            if (alreadyRunning) return;

            await workflow.StartAsync(new StartWorkflowRequest(defId.Value, "Document", e.DocumentId), ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Auto-start default workflow failed for document {DocumentId}", e.DocumentId); }
    }
}

/// <summary>2.3 — reserve a physical box for documents whose type requires physical archiving.
/// Feature-gated (<c>EventWorkflow:AutoReservePhysicalLocation</c>, default off) because the system also
/// supports explicit filing via the location picker; enabling both would double-file.</summary>
public sealed class DocumentPhysicalLocationHandler(
    AppDbContext db, IConfiguration config, ILogger<DocumentPhysicalLocationHandler> log)
    : INotificationHandler<DocumentCreatedEvent>
{
    public async Task Handle(DocumentCreatedEvent e, CancellationToken ct)
    {
        if (!config.GetValue<bool>("EventWorkflow:AutoReservePhysicalLocation")) return;
        try
        {
            var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == e.DocumentId, ct);
            if (doc is null || doc.BoxId is not null) return;

            var requires = await db.DocumentTypes.Where(t => t.Id == doc.DocumentTypeId)
                .Select(t => t.RequiresPhysicalArchiving).FirstOrDefaultAsync(ct);
            if (!requires) return;

            var box = await db.Boxes
                .Where(b => b.IsActive && (b.Capacity == null || b.CurrentCount < b.Capacity))
                .OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
            if (box is null) return;

            doc.BoxId = box.Id;
            box.CurrentCount += 1;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Auto-reserve physical location failed for document {DocumentId}", e.DocumentId); }
    }
}
