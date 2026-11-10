using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.InternalMail;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>المراسلات — internal correspondence engine. Composing, replying/forwarding (threaded
/// conversation), internal addressing with per-user read tracking, archiving to Documents, and search.
/// Recipients are always internal (employee / department / group) — never an external party.</summary>
public sealed class InternalMailService(
    AppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditWriter audit,
    IDocumentService documents,
    IAccessScope scope,
    IWorkflowService workflow) : IInternalMailService
{
    private const string EntityType = "InternalMail";
    private long Uid => currentUser.UserId ?? 0;

    /// <summary>Internal mail is private to its participants: the sender, and whoever it was
    /// delivered to (a read row is the delivery record, including members reached through a
    /// department or a group).
    ///
    /// Past that, one person sees it — the head of the department a participant belongs to. A
    /// manager answers for their department's correspondence, so they read what their staff send
    /// and receive; an ordinary colleague in the same department reads none of it. Only the
    /// System Administrator sees the institution's.
    ///
    /// The named folders were already per-user, but the unfiltered list, the search, the thread
    /// view and every by-id read were institution-wide within clearance — so an ordinary employee
    /// could search another department's internal correspondence and open it.</summary>
    private async Task<IQueryable<InternalMail>> MineAsync(IQueryable<InternalMail> q, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var me = Uid;
        var managed = await scope.ManagedOrgUnitIdsAsync(ct);
        if (managed.Count == 0) return q.Where(x => x.SenderUserId == me || x.Reads.Any(rd => rd.UserId == me));

        /* Department membership is read from the user record rather than the message's
           `SenderOrgUnitId` snapshot: the snapshot records where the sender sat when they wrote,
           and a manager needs their department's mail as it stands today, including what someone
           sent before transferring in. */
        return q.Where(x => x.SenderUserId == me || x.Reads.Any(rd => rd.UserId == me)
            || db.Users.Any(u => u.Id == x.SenderUserId && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value))
            || x.Reads.Any(rd => db.Users.Any(u => u.Id == rd.UserId
                && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value))));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<Result<InternalDetailDto>> ComposeAsync(ComposeInternalRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Subject)) return Fail("الموضوع مطلوب");
        if (r.Recipients.Count == 0) return Fail("يجب تحديد مُرسَل إليه واحد على الأقل");
        if (r.Recipients.Any(x => x.TargetKind == RecipientKind.External))
            return Fail("المراسلات الداخلية تُرسَل إلى موظف أو إدارة أو مجموعة فقط");

        if (await NotAMemberOfAsync(r.Recipients, ct) is { } blocked) return Fail(blocked);

        var institutionId = await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (institutionId is null) return Fail("لا توجد مؤسسة مُعرّفة");

        var me = await db.Users.Where(u => u.Id == Uid).Select(u => new { u.FullName, u.OrgUnitId }).FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;

        var m = new InternalMail
        {
            InstitutionId = institutionId.Value,
            MailNumber = await NextNumberAsync(institutionId.Value, ct),
            ReferenceNumber = r.ReferenceNumber,
            SenderUserId = Uid,
            SenderName = me?.FullName,
            SenderOrgUnitId = me?.OrgUnitId,
            Subject = r.Subject.Trim(),
            Description = r.Description,
            Body = r.Body,
            Keywords = r.Keywords,
            MailDate = now,
            RegistrationDate = now,
            DueDate = r.DueDate,
            ResponseDeadline = r.ResponseDeadline ?? (r.ResponsePeriodDays is > 0 ? now.AddDays(r.ResponsePeriodDays.Value) : null),
            ResponsePeriodDays = r.ResponsePeriodDays,
            RemindOnNoReply = r.RemindOnNoReply,
            ActionType = r.ActionType,
            CategoryId = r.CategoryId,
            Classification = r.Classification,
            Priority = r.Priority,
            Status = CorrespondenceStatus.Sent,
            FileNumber = r.FileNumber,
            CreatedBy = Uid,
        };
        AddRecipients(m, r.Recipients);
        db.InternalMails.Add(m);
        await db.SaveChangesAsync(ct);

        await ResolveReadRowsAsync(m, ct);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Created", EntityType, m.Id, m.Subject, ct: ct);

        /* The trigger. A message whose تصنيف البريد has a workflow bound to it starts that run here,
           and the engine addresses the first approver through this same module — the sender forwards
           nothing by hand. Everything else composes exactly as before. */
        await workflow.StartForInternalMailAsync(m.Id, m.CategoryId, ct);

        return Ok(await LoadDetailAsync(m.Id, ct));
    }

    public async Task<Result<InternalDetailDto>> ReplyAsync(long id, ReplyInternalRequest r, CancellationToken ct = default)
    {
        var src = await LoadForActionAsync(id, ct);
        if (src is null) return Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");

        // Reply goes back to the original sender; reply-all also copies the original To/Cc.
        var recips = new List<RecipientInput>
        {
            new(CorrespondenceAddressing.To, RecipientKind.Employee, src.SenderUserId, src.SenderName),
        };
        if (r.ReplyAll)
        {
            var copied = src.Recipients
                .Where(x => x.Addressing != CorrespondenceAddressing.Bcc && x.TargetId != Uid)
                .Select(x => new RecipientInput(CorrespondenceAddressing.Cc, x.TargetKind, x.TargetId, x.TargetName))
                .ToList();
            /* Reply-all inherits the original addressees rather than choosing them. If one of them is
               a group this sender is not in, drop that group instead of refusing the whole reply —
               everyone else still gets it, and the sender is told which group was left out. */
            if (await NotAMemberOfAsync(copied, ct) is not null)
            {
                var myGroups = await MyGroupIdsAsync(ct);
                copied = copied.Where(x => x.TargetKind != RecipientKind.Group
                    || (x.TargetId is { } gid && myGroups.Contains(gid))).ToList();
            }
            recips.AddRange(copied);
        }
        if (r.AdditionalRecipients is { } extra) recips.AddRange(extra);

        var created = await ComposeAsync(new ComposeInternalRequest(
            r.Subject, src.ReferenceNumber, r.Classification, r.Priority, null, null, null, false,
            src.ActionType, src.CategoryId, null, r.Body, src.Keywords, src.FileNumber, recips), ct);
        if (!created.Succeeded) return created;

        var reply = await db.InternalMails.FirstAsync(x => x.Id == created.Value!.Id, ct);
        reply.ReplyToId = src.Id;
        reply.ParentId = src.ParentId ?? src.Id;
        src.Status = CorrespondenceStatus.Replied;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Replied", EntityType, src.Id, src.Subject, ct: ct);
        return Ok(await LoadDetailAsync(reply.Id, ct));
    }

    public async Task<Result<InternalDetailDto>> ForwardAsync(long id, ForwardInternalRequest r, CancellationToken ct = default)
    {
        var src = await LoadForActionAsync(id, ct);
        if (src is null) return Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");
        if (r.Recipients.Count == 0) return Fail("يجب تحديد جهة تحويل واحدة على الأقل");

        var created = await ComposeAsync(new ComposeInternalRequest(
            $"تحويل: {src.Subject}", src.ReferenceNumber, src.Classification, src.Priority, null, null, null, false,
            src.ActionType, src.CategoryId, null, r.Body ?? src.Body, src.Keywords, src.FileNumber, r.Recipients), ct);
        if (!created.Succeeded) return created;

        var fwd = await db.InternalMails.FirstAsync(x => x.Id == created.Value!.Id, ct);
        fwd.ParentId = src.ParentId ?? src.Id;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Forwarded", EntityType, src.Id, src.Subject, ct: ct);
        return Ok(await LoadDetailAsync(fwd.Id, ct));
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<Result<InternalDetailDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var allowed = await (await MineAsync(db.InternalMails.AsNoTracking(), ct))
            .AnyAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct);
        return allowed ? Ok(await LoadDetailAsync(id, ct)) : Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");
    }

    public async Task<PagedResult<InternalListItemDto>> ListAsync(InternalListFilter f, CancellationToken ct = default)
    {
        var q = db.InternalMails.AsNoTracking().Where(x => x.Classification <= currentUser.Clearance);
        q = f.Folder switch
        {
            /* وارد — messages addressed to me that somebody else wrote.
             *
             * A read row is the delivery record, and the sender can hold one too: they may be on
             * the address list of their own circular, and opening their own letter used to mint
             * one. Either way the letter is صادر to them — the folder had their own outgoing mail
             * in it, under a "from" column naming the people they had written to. */
            "inbox" => q.Where(x => x.SenderUserId != Uid && x.Reads.Any(rd => rd.UserId == Uid)),
            // صادر — messages I sent.
            "sent" => q.Where(x => x.SenderUserId == Uid),
            "drafts" => q.Where(x => x.Status == CorrespondenceStatus.Draft && x.SenderUserId == Uid),
            "deleted" => q.IgnoreQueryFilters().Where(x => x.IsDeleted),
            _ => q,
        };
        if (f.Status is { } st) q = q.Where(x => x.Status == st);
        if (f.Classification is { } cl) q = q.Where(x => x.Classification == cl);
        if (f.From is { } from) q = q.Where(x => x.MailDate >= from);
        if (f.To is { } to) q = q.Where(x => x.MailDate <= to);
        q = await MineAsync(q, ct);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, f.Page);
        var size = Math.Clamp(f.PageSize, 1, 100);
        var rows = await q.OrderByDescending(x => x.RegistrationDate).Skip((page - 1) * size).Take(size)
            .Select(x => new InternalListItemDto(
                x.Id, x.MailNumber, x.ReferenceNumber, x.Subject, x.SenderName,
                x.Classification.ToString(), x.Priority.ToString(), x.Status.ToString(),
                x.ActionType.HasValue ? x.ActionType.ToString() : null,
                x.CategoryId, x.Category != null ? x.Category.Name : null,
                x.MailDate, x.RegistrationDate, x.DueDate, x.ResponseDeadline,
                x.Reads.Any(rd => rd.UserId == Uid && rd.ReadStatus == CorrespondenceReadStatus.Read),
                x.Attachments.Any(),
                x.SenderUserId == Uid))
            .ToListAsync(ct);
        return new PagedResult<InternalListItemDto>
        {
            Items = await WithWorkflowAsync(await WithRecipientsAsync(rows, ct), ct),
            Page = page, PageSize = size, TotalCount = total,
        };
    }

    /// <summary>Attaches the workflow columns to a page of rows: the route, the step, and whether this
    /// reader is the one it is waiting on. Rows outside a workflow are returned untouched, which is
    /// most of them — the cost of this is one lookup per page, not per message.</summary>
    private async Task<List<InternalListItemDto>> WithWorkflowAsync(
        List<InternalListItemDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var badges = await workflow.BadgesForMailsAsync(rows.Select(r => r.Id).ToList(), ct);
        if (badges.Count == 0) return rows;
        return rows
            .Select(r => badges.TryGetValue(r.Id, out var b) ? r with { Workflow = b } : r)
            .ToList();
    }

    /// <summary>
    /// Attaches each message's To line, so the sent folder can be listed by who it went to.
    ///
    /// Fetched for the page in one round-trip rather than projected inside the list query: a
    /// collection inside the Select is translated by pulling a second query per row on this
    /// provider, and the list is the busiest screen in the module.
    /// </summary>
    private async Task<List<InternalListItemDto>> WithRecipientsAsync(
        List<InternalListItemDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.Id).ToList();
        var recipients = await db.InternalRecipients.AsNoTracking()
            .Where(r => ids.Contains(r.InternalMailId) && r.TargetName != null)
            .Select(r => new { r.InternalMailId, r.TargetName, r.Addressing })
            .ToListAsync(ct);

        /* The To line, or the whole address list when there is no To line. A message copied to
           people without being addressed to anyone still went somewhere, and showing "—" for it
           would read as a message with no recipient at all. */
        var byMail = recipients
            .GroupBy(r => r.InternalMailId)
            .ToDictionary(g => g.Key, g =>
            {
                var to = g.Where(r => r.Addressing == CorrespondenceAddressing.To).ToList();
                return (IReadOnlyList<string>)(to.Count > 0 ? to : [.. g])
                    .Select(r => r.TargetName!).Distinct().ToList();
            });

        return rows
            .Select(r => r with { RecipientNames = byMail.GetValueOrDefault(r.Id, []) })
            .ToList();
    }

    public async Task<PagedResult<InternalListItemDto>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0) return new PagedResult<InternalListItemDto> { Items = [], Page = 1, PageSize = pageSize, TotalCount = 0 };
        var q = db.InternalMails.AsNoTracking().Where(x => x.Classification <= currentUser.Clearance)
            .Where(x => EF.Functions.Like(x.Subject, $"%{term}%")
                     || (x.Body != null && EF.Functions.Like(x.Body, $"%{term}%"))
                     || (x.Keywords != null && EF.Functions.Like(x.Keywords, $"%{term}%"))
                     || x.Attachments.Any(a => a.OcrText != null && EF.Functions.Like(a.OcrText, $"%{term}%")));
        q = await MineAsync(q, ct);
        var total = await q.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 100);
        var pg = Math.Max(1, page);
        var rows = await q.OrderByDescending(x => x.RegistrationDate).Skip((pg - 1) * size).Take(size)
            .Select(x => new InternalListItemDto(
                x.Id, x.MailNumber, x.ReferenceNumber, x.Subject, x.SenderName,
                x.Classification.ToString(), x.Priority.ToString(), x.Status.ToString(),
                x.ActionType.HasValue ? x.ActionType.ToString() : null,
                x.CategoryId, x.Category != null ? x.Category.Name : null,
                x.MailDate, x.RegistrationDate, x.DueDate, x.ResponseDeadline, false, x.Attachments.Any(),
                x.SenderUserId == Uid))
            .ToListAsync(ct);
        return new PagedResult<InternalListItemDto>
        {
            Items = await WithWorkflowAsync(await WithRecipientsAsync(rows, ct), ct),
            Page = pg, PageSize = size, TotalCount = total,
        };
    }

    public async Task<IReadOnlyList<InternalThreadNodeDto>> GetThreadAsync(long id, CancellationToken ct = default)
    {
        var root = await (await MineAsync(db.InternalMails.AsNoTracking(), ct))
            .Where(x => x.Id == id && x.Classification <= currentUser.Clearance)
            .Select(x => new { x.Id, x.ParentId }).FirstOrDefaultAsync(ct);
        if (root is null) return [];
        var rootId = root.ParentId ?? root.Id;

        var members = await (await MineAsync(db.InternalMails.AsNoTracking(), ct))
            .Where(x => (x.Id == rootId || x.ParentId == rootId) && x.Classification <= currentUser.Clearance)
            .Select(x => new { x.Id, x.MailNumber, x.Subject, x.Status, x.MailDate, x.ReplyToId, x.SenderName, x.Body })
            .ToListAsync(ct);

        return members.OrderBy(x => x.MailDate)
            .Select(x => new InternalThreadNodeDto(
                x.Id, x.MailNumber, x.Subject, x.Status.ToString(),
                x.Id == rootId ? "Root" : x.ReplyToId != null ? "Reply" : "Related",
                x.MailDate, x.SenderName, x.Body))
            .ToList();
    }

    public async Task<IReadOnlyList<InternalReadDto>> GetReadStatusAsync(long id, CancellationToken ct = default)
    {
        var allowed = await (await MineAsync(db.InternalMails.AsNoTracking(), ct))
            .AnyAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct);
        if (!allowed) return [];
        var reads = await db.InternalReads.AsNoTracking().Where(r => r.InternalMailId == id).ToListAsync(ct);
        var userIds = reads.Select(r => r.UserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName }).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return reads.Select(r => new InternalReadDto(
            r.UserId, names.TryGetValue(r.UserId, out var n) ? n : null,
            r.ReadStatus.ToString(), r.DeliveredAt, r.FirstReadAt, r.LastReadAt)).ToList();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public async Task<Result<bool>> MarkReadAsync(long id, CancellationToken ct = default)
    {
        var row = await db.InternalReads.FirstOrDefaultAsync(r => r.InternalMailId == id && r.UserId == Uid, ct);
        var now = DateTime.UtcNow;
        if (row is null)
        {
            if (!await (await MineAsync(db.InternalMails, ct)).AnyAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct))
                return FailBool("المراسلة غير موجودة");
            db.InternalReads.Add(new InternalRead
            {
                InternalMailId = id, UserId = Uid, ReadStatus = CorrespondenceReadStatus.Read,
                DeliveredAt = now, FirstReadAt = now, LastReadAt = now, CreatedBy = Uid,
            });
        }
        else
        {
            row.LastReadAt = now; row.FirstReadAt ??= now; row.DeliveredAt ??= now;
            row.ReadStatus = CorrespondenceReadStatus.Read;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (row is null)
        {
            // Two concurrent opens of the same message both saw "no read row" and both inserted.
            // The unique index rejects the loser; marking read is idempotent, so treat it as done.
            db.ChangeTracker.Clear();
            var existing = await db.InternalReads
                .FirstOrDefaultAsync(r => r.InternalMailId == id && r.UserId == Uid, ct);
            if (existing is null) throw;
            existing.LastReadAt = now; existing.FirstReadAt ??= now; existing.DeliveredAt ??= now;
            existing.ReadStatus = CorrespondenceReadStatus.Read;
            await db.SaveChangesAsync(ct);
        }
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ArchiveAsync(long id, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return FailBool("المراسلة غير موجودة");
        if (m.IsArchived) return Result<bool>.Ok(true);
        m.IsArchived = true; m.ArchivedAt = DateTime.UtcNow; m.Status = CorrespondenceStatus.Archived;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Archived", EntityType, id, m.Subject, ct: ct);

        var docId = await MailArchive.FileAndNotifyAsync(db, documents, audit, Uid,
            m.Subject, m.MailNumber, m.Body, m.Description, m.Keywords, m.Classification, m.MailDate, ct);
        if (docId is { }) { m.ArchivedDocumentId = docId; await db.SaveChangesAsync(ct); }
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SoftDeleteAsync(long id, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return FailBool("المراسلة غير موجودة");
        m.IsDeleted = true; m.DeletedAt = DateTime.UtcNow; m.DeletedBy = Uid;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", EntityType, id, m.Subject, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> RestoreAsync(long id, CancellationToken ct = default)
    {
        var m = await db.InternalMails.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted && x.Classification <= currentUser.Clearance, ct);
        if (m is null) return FailBool("المراسلة غير موجودة في المحذوفات");
        m.IsDeleted = false; m.DeletedAt = null; m.DeletedBy = null;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Restored", EntityType, id, m.Subject, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<InternalAttachmentDto>> AddAttachmentAsync(long id, string fileName, string? contentType, byte[] content, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return Result<InternalAttachmentDto>.Fail("المراسلة غير موجودة");
        if (content.Length == 0) return Result<InternalAttachmentDto>.Fail("الملف فارغ");
        using var ms = new MemoryStream(content);
        var stored = await storage.SaveAsync($"internal-mail/{id}", fileName, ms, ct);
        var att = new InternalAttachment
        {
            InternalMailId = id, StorageKey = stored.StorageKey, Checksum = stored.Checksum,
            FileName = fileName, ContentType = contentType, SizeBytes = stored.SizeBytes,
            OcrStatus = CorrespondenceOcrStatus.Pending, CreatedBy = Uid,
        };
        db.InternalAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("AttachmentAdded", EntityType, id, fileName, ct: ct);
        return Result<InternalAttachmentDto>.Ok(new InternalAttachmentDto(
            att.Id, att.StorageKey, att.FileName, att.ContentType, att.SizeBytes, att.Version, att.OcrStatus.ToString()));
    }

    /// <summary>Opens an attachment's bytes. Goes through the same clearance/visibility check as the
    /// message itself, so a download can never expose a message the caller may not read.</summary>
    public async Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long id, long attachmentId, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return Result<AttachmentDownload>.Fail("المراسلة غير موجودة");

        var att = await db.InternalAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.InternalMailId == id, ct);
        if (att is null) return Result<AttachmentDownload>.Fail("المرفق غير موجود");

        var stream = await storage.OpenAsync(att.StorageKey, ct);
        if (stream is null) return Result<AttachmentDownload>.Fail("ملف المرفق غير موجود في وحدة التخزين");

        await audit.WriteAsync("DownloadAttachment", EntityType, id, m.Subject, newValues: att.FileName, ct: ct);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(stream, att.FileName, att.ContentType ?? "application/octet-stream"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Result<InternalDetailDto> Fail(string m) => Result<InternalDetailDto>.Fail(m);
    private static Result<InternalDetailDto> Ok(InternalDetailDto d) => Result<InternalDetailDto>.Ok(d);
    private static Result<bool> FailBool(string m) => Result<bool>.Fail(m);

    private static void AddRecipients(InternalMail m, IReadOnlyList<RecipientInput> recipients)
    {
        foreach (var r in recipients)
            m.Recipients.Add(new InternalRecipient
            {
                Addressing = r.Addressing, TargetKind = r.TargetKind, TargetId = r.TargetId, TargetName = r.TargetName,
            });
    }

    /// <summary>A group may only be addressed by someone inside it — directly, or through the
    /// department the group names. Returns the error to fail with, or null when every group in the
    /// list is allowed. The System Administrator is exempt: they maintain the groups and must be
    /// able to reach any of them. Checked here rather than only in the picker, because the picker
    /// is a convenience and the API is the rule.</summary>
    /// <summary>Ids of the groups the caller belongs to, directly or through their department.</summary>
    private async Task<HashSet<long>> MyGroupIdsAsync(CancellationToken ct)
    {
        var ouId = await db.Users.Where(u => u.Id == Uid).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        var ids = await db.CorrespondenceGroupMembers
            .Where(m => (m.MemberType == GroupMemberType.User && m.UserId == Uid)
                     || (m.MemberType == GroupMemberType.OrgUnit && ouId != null && m.OrgUnitId == ouId))
            .Select(m => m.GroupId).Distinct().ToListAsync(ct);
        return ids.ToHashSet();
    }

    private async Task<string?> NotAMemberOfAsync(IReadOnlyCollection<RecipientInput> recipients, CancellationToken ct)
    {
        var groupIds = recipients
            .Where(x => x.TargetKind == RecipientKind.Group && x.TargetId is not null)
            .Select(x => x.TargetId!.Value).Distinct().ToList();
        if (groupIds.Count == 0) return null;

        if (await db.UserRoles.AnyAsync(ur => ur.UserId == Uid
                && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator"), ct))
            return null;

        var ouId = await db.Users.Where(u => u.Id == Uid).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        var allowed = await db.CorrespondenceGroupMembers
            .Where(m => groupIds.Contains(m.GroupId) && (
                (m.MemberType == GroupMemberType.User && m.UserId == Uid)
                || (m.MemberType == GroupMemberType.OrgUnit && ouId != null && m.OrgUnitId == ouId)))
            .Select(m => m.GroupId).Distinct().ToListAsync(ct);

        var denied = groupIds.Except(allowed).ToList();
        if (denied.Count == 0) return null;

        var names = await db.CorrespondenceGroups.Where(g => denied.Contains(g.Id))
            .Select(g => g.Name).ToListAsync(ct);
        return $"لا يمكن الإرسال إلى مجموعة لست عضوًا فيها: {string.Join("، ", names)}";
    }

    /// <summary>Next number in the year's sequence, taken from the highest number already issued.
    /// It used to be COUNT+1, which repeats a number as soon as the sequence has any gap — delete
    /// one message and the next one collides with the unique index and the send fails with a 500.
    /// Zero-padding makes the string order the numeric order, so MAX can be done in SQL.</summary>
    // Shared with WorkflowMailRouter, which registers generated approval messages on the same
    // sequence — two numbering implementations would eventually hand out the same INT number.
    private Task<string> NextNumberAsync(long institutionId, CancellationToken ct) =>
        InternalMailNumbering.NextAsync(db, institutionId, ct);

    /// <summary>Expands recipients into per-user read rows: Employee → that user; Department → its active
    /// users; Group → member users + the users of member org-units.</summary>
    private async Task ResolveReadRowsAsync(InternalMail m, CancellationToken ct)
    {
        var userIds = new HashSet<long>();
        foreach (var r in m.Recipients)
        {
            switch (r.TargetKind)
            {
                case RecipientKind.Employee when r.TargetId is { } uid:
                    userIds.Add(uid);
                    break;
                case RecipientKind.Department when r.TargetId is { } ouId:
                    foreach (var u in await db.Users.Where(u => u.OrgUnitId == ouId && u.IsActive).Select(u => u.Id).ToListAsync(ct))
                        userIds.Add(u);
                    break;
                case RecipientKind.Group when r.TargetId is { } gid:
                    var members = await db.CorrespondenceGroupMembers.Where(g => g.GroupId == gid).ToListAsync(ct);
                    foreach (var mem in members)
                    {
                        if (mem.MemberType == GroupMemberType.User && mem.UserId is { } mu) userIds.Add(mu);
                        else if (mem.MemberType == GroupMemberType.OrgUnit && mem.OrgUnitId is { } mo)
                            foreach (var u in await db.Users.Where(u => u.OrgUnitId == mo && u.IsActive).Select(u => u.Id).ToListAsync(ct))
                                userIds.Add(u);
                    }
                    break;
            }
        }

        var existing = m.Reads.Select(x => x.UserId).ToHashSet();
        foreach (var uid in userIds.Where(u => !existing.Contains(u)))
            m.Reads.Add(new InternalRead
            {
                InternalMailId = m.Id, UserId = uid, ReadStatus = CorrespondenceReadStatus.Delivered,
                DeliveredAt = DateTime.UtcNow, CreatedBy = Uid,
            });
    }

    private async Task<InternalMail?> LoadForActionAsync(long id, CancellationToken ct) =>
        await (await MineAsync(db.InternalMails.Include(x => x.Recipients), ct))
            .FirstOrDefaultAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct);

    private async Task<InternalDetailDto> LoadDetailAsync(long id, CancellationToken ct)
    {
        var m = await db.InternalMails.AsNoTracking()
            .Include(x => x.Recipients).Include(x => x.Attachments).Include(x => x.Category)
            .FirstAsync(x => x.Id == id, ct);
        return new InternalDetailDto(
            m.Id, m.MailNumber, m.ReferenceNumber, m.SenderUserId, m.SenderName,
            m.Subject, m.Description, m.Body, m.Keywords,
            m.Classification.ToString(), m.Priority.ToString(), m.Status.ToString(),
            m.ActionType.HasValue ? m.ActionType.ToString() : null,
            m.CategoryId, m.Category?.Name, m.ResponsePeriodDays, m.RemindOnNoReply,
            m.FileNumber, m.IsArchived, m.ArchivedDocumentId,
            m.MailDate, m.RegistrationDate, m.DueDate, m.ResponseDeadline, m.ReplyToId, m.ParentId,
            m.Recipients.Select(r => new InternalRecipientDto(r.Id, r.Addressing.ToString(), r.TargetKind.ToString(), r.TargetId, r.TargetName)).ToList(),
            m.Attachments.Select(a => new InternalAttachmentDto(a.Id, a.StorageKey, a.FileName, a.ContentType, a.SizeBytes, a.Version, a.OcrStatus.ToString())).ToList());
    }
}
