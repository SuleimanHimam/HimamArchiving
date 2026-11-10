using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Documents;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Archiving.Infrastructure.Services;

/// <summary>Shared filing step used when a Diwan letter or an internal message is archived: writes its
/// official copy into the Documents table (owned by قسم الأرشيف, typed "مراسلة مؤرشفة") and notifies the
/// archive department's staff plus every Archive Officer. Best-effort — archiving never fails because the
/// document copy or notification could not be written.</summary>
internal static class MailArchive
{
    private const string TypeName = "مراسلة مؤرشفة";
    private const string ArchiveUnitName = "قسم الأرشيف";

    public static async Task<long?> FileAndNotifyAsync(
        AppDbContext db, IDocumentService documents, IAuditWriter audit,
        long actorUserId, string subject, string number, string? body, string? description,
        string? keywords, ConfidentialityLevel classification, DateTime mailDate, CancellationToken ct)
    {
        long? docId = null;
        try
        {
            /* A letter's paper original ends up on a shelf like any other record, so this type is
               marked as needing physical archiving: that flag is what lets the archived copy be sent
               on to قسم الأرشيف (DocumentService.SendToArchiveAsync refuses types without it), which
               is what makes mail → document → box one continuous path instead of stopping here.
               Reconciled on an existing type too, because the type is created once and this flag was
               false in every institution set up before it. */
            var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Name == TypeName, ct);
            if (type is null)
            {
                type = new DocumentType
                {
                    Name = TypeName, NameEn = "Archived Correspondence",
                    RetentionMonths = 120, IsActive = true, RequiresPhysicalArchiving = true,
                    DefaultConfidentiality = ConfidentialityLevel.Internal, CreatedBy = actorUserId,
                };
                db.DocumentTypes.Add(type);
                await db.SaveChangesAsync(ct);
            }
            else if (!type.RequiresPhysicalArchiving)
            {
                type.RequiresPhysicalArchiving = true;
                await db.SaveChangesAsync(ct);
            }

            var owningUnit = await db.OrgUnits.Where(u => u.Name == ArchiveUnitName).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct)
                ?? await db.Users.Where(u => u.Id == actorUserId).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct)
                ?? await db.OrgUnits.OrderBy(u => u.Id).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
            if (owningUnit is null) return null;

            var plain = string.IsNullOrWhiteSpace(body) ? description : Regex.Replace(body, "<[^>]+>", " ").Trim();
            var descr = $"مؤرشف من المراسلة {number}." + (string.IsNullOrWhiteSpace(plain) ? "" : $"\n\n{plain}");

            var created = await documents.CreateAsync(new CreateDocumentRequest(
                Title: subject,
                Description: descr,
                DocumentTypeId: type.Id,
                CategoryId: null,
                OwningOrgUnitId: owningUnit.Value,
                OwnerPositionId: null,
                Confidentiality: classification,
                Keywords: keywords,
                DocumentDate: DateOnly.FromDateTime(mailDate),
                ArchiveMode: ArchiveMode.Manual), ct);
            docId = created.Succeeded ? created.Value!.Id : null;
        }
        catch { /* filing is best-effort */ }

        try { await NotifyAsync(db, actorUserId, subject, number, docId, ct); } catch { /* ignore */ }
        return docId;
    }

    private static async Task NotifyAsync(AppDbContext db, long actorUserId, string subject, string number, long? docId, CancellationToken ct)
    {
        var archiveUnitId = await db.OrgUnits.Where(u => u.Name == ArchiveUnitName).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);

        var recipients = await db.Users
            .Where(u => u.IsActive && u.Id != actorUserId && (
                (archiveUnitId != null && u.OrgUnitId == archiveUnitId) ||
                db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Archive Officer"))))
            .Select(u => u.Id).Distinct().ToListAsync(ct);
        if (recipients.Count == 0) return;

        foreach (var uid in recipients)
            db.Notifications.Add(new Notification
            {
                RecipientUserId = uid,
                Title = $"أرشفة مراسلة: {subject}",
                Body = $"تمت أرشفة المراسلة رقم {number} وحفظها في الوثائق.",
                Type = NotificationType.Info,
                Channels = NotificationChannel.InApp,
                EntityType = docId != null ? "Document" : "Correspondence",
                EntityId = docId ?? 0,
                CreatedBy = actorUserId,
            });
        await db.SaveChangesAsync(ct);
    }
}
