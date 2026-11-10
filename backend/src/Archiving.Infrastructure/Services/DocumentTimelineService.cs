using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Timeline;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Builds a document's unified timeline by merging audit, workflow, correspondence, sharing,
/// fixity and escalation records into one chronological list.</summary>
public sealed class DocumentTimelineService(AppDbContext db, ICurrentUser currentUser) : IDocumentTimelineService
{
    public async Task<Result<IReadOnlyList<TimelineEntryDto>>> GetTimelineAsync(
        long documentId, TimelineFilter? filter, CancellationToken ct = default)
    {
        var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<IReadOnlyList<TimelineEntryDto>>.Fail("الوثيقة غير موجودة");
        // Clearance gate (mirrors document access).
        if ((int)doc.Confidentiality > (int)currentUser.Clearance)
            return Result<IReadOnlyList<TimelineEntryDto>>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var entries = new List<(TimelineEntryDto Dto, long? UserId)>();

        // 1) Audit log (covers status changes, edits, prints, archive, …).
        var audits = await db.AuditLogs
            .Where(a => a.EntityType == "Document" && a.EntityId == documentId)
            .Select(a => new { a.Id, a.Action, a.EntityTitle, a.CreatedAt, a.UserId, a.NewValues })
            .ToListAsync(ct);
        foreach (var a in audits)
            entries.Add((new TimelineEntryDto
            {
                Id = a.Id, Timestamp = a.CreatedAt,
                EntryType = a.Action is "Archived" or "SentToArchive" or "Destroyed" ? "StatusChange" : "Audit",
                Title = a.Action, Description = a.NewValues,
            }, a.UserId));

        // 2) Workflow actions (completed tasks on this document's instances).
        var wf = await db.WorkflowTasks
            .Where(t => t.CompletedAt != null &&
                db.WorkflowInstances.Any(w => w.Id == t.WorkflowInstanceId && w.EntityType == "Document" && w.EntityId == documentId))
            .Select(t => new { t.Id, t.CompletedAt, t.CompletedByUserId, Stage = t.Stage.Name, t.ActionTaken, t.Note })
            .ToListAsync(ct);
        foreach (var t in wf)
            entries.Add((new TimelineEntryDto
            {
                Id = t.Id, Timestamp = t.CompletedAt!.Value, EntryType = "WorkflowAction",
                Title = $"{t.ActionTaken} — {t.Stage}", Description = t.Note,
            }, t.CompletedByUserId));

        // 3) Shares.
        var shares = await db.DocumentShares
            .Where(s => s.DocumentId == documentId)
            .Select(s => new { s.Id, s.CreatedAt, s.SharedWithUserId, s.SharedByUserId, s.CanEdit })
            .ToListAsync(ct);
        foreach (var s in shares)
            entries.Add((new TimelineEntryDto
            {
                Id = s.Id, Timestamp = s.CreatedAt, EntryType = "Share",
                Title = "تمت مشاركة الوثيقة", Description = s.CanEdit ? "صلاحية: عرض وتعديل" : "صلاحية: عرض",
                Metadata = new() { ["sharedWithUserId"] = s.SharedWithUserId },
            }, s.SharedByUserId));

        // 5) Fixity checks on the document's attachments.
        var fixity = await db.FixityChecks
            .Where(f => db.DocumentAttachments.Any(a => a.Id == f.DocumentAttachmentId && a.DocumentId == documentId))
            .Select(f => new { f.Id, f.CheckedAt, f.Result, f.Algorithm, f.CheckedByUserId })
            .ToListAsync(ct);
        foreach (var f in fixity)
            entries.Add((new TimelineEntryDto
            {
                Id = f.Id, Timestamp = f.CheckedAt, EntryType = "FixityCheck",
                Title = $"تحقق سلامة ({f.Algorithm})", Description = f.Result.ToString(),
            }, f.CheckedByUserId));

        // 6) Escalations on this document's workflow instances.
        var esc = await db.EscalationLogs
            .Where(e => db.WorkflowInstances.Any(w => w.Id == e.WorkflowInstanceId && w.EntityType == "Document" && w.EntityId == documentId))
            .Select(e => new { e.Id, e.EscalatedAt, e.EscalationLevel, e.ActionTaken })
            .ToListAsync(ct);
        foreach (var e in esc)
            entries.Add((new TimelineEntryDto
            {
                Id = e.Id, Timestamp = e.EscalatedAt, EntryType = "Escalation",
                Title = $"تصعيد (مستوى {e.EscalationLevel})", Description = e.ActionTaken.ToString(),
            }, null));

        // Resolve actor names in one query.
        var userIds = entries.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        foreach (var (dto, userId) in entries)
            if (userId is { } uid && names.TryGetValue(uid, out var name)) dto.ActorName = name;

        // Apply filters + sort newest-first.
        IEnumerable<TimelineEntryDto> result = entries.Select(e => e.Dto);
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Types))
            {
                var set = filter.Types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                result = result.Where(e => set.Contains(e.EntryType));
            }
            if (filter.From is { } from) result = result.Where(e => e.Timestamp >= from);
            if (filter.To is { } to) result = result.Where(e => e.Timestamp <= to);
        }

        return Result<IReadOnlyList<TimelineEntryDto>>.Ok(result.OrderByDescending(e => e.Timestamp).ToList());
    }
}
