using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Diwan;
using Archiving.Application.Features.Documents;
using Archiving.Application.Features.InternalMail;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Archiving.Infrastructure.Services;

/// <summary>الديوان — external register engine. Registration (incoming/outgoing), listing, search,
/// archiving to Documents, attachments. Validation is inline via <see cref="Result{T}"/>; classification
/// is filtered server-side against the caller's clearance.</summary>
public sealed class DiwanService(
    AppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditWriter audit,
    IDocumentService documents,
    IInternalMailService internalMail,
    IAccessScope scope) : IDiwanService
{
    private const string EntityType = "DiwanMail";
    private long Uid => currentUser.UserId ?? 0;

    /// <summary>Narrows the register to the letters the caller has to do with: the ones they
    /// registered, the ones their head let them in on, and — heading a department — everything
    /// addressed to it or registered by its staff.
    ///
    /// A colleague's letter is *not* theirs, and neither is one merely addressed to the department
    /// they sit in: the head answers for the department's external correspondence and says who
    /// handles which letter. See <see cref="DiwanAccess"/>.</summary>
    private async Task<IQueryable<DiwanMail>> ScopedAsync(IQueryable<DiwanMail> q, CancellationToken ct)
    {
        var (isAdmin, _, managed) = await scope.ResolveAsync(ct);
        // Shared with the dashboard that counts this register — see ScopeFilters.
        return isAdmin ? q : q.VisibleDiwan(db, Uid, managed);
    }

    public async Task<Result<DiwanDetailDto>> CreateAsync(SaveDiwanRequest r, CancellationToken ct = default)
    {
        // No field is mandatory in the register: a clerk may log a letter with only what is known at
        // the counter. The register number is still assigned, so the entry is always traceable.
        var institutionId = await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (institutionId is null) return Fail("لا توجد مؤسسة مُعرّفة");

        // When the party is linked to the directory, take its name from there so the snapshot on the
        // register always matches the organization record. A free-typed name is kept as given.
        long? orgId = r.ExternalOrgId;
        var orgName = r.ExternalOrgName;
        if (orgId is { } oid)
        {
            var linked = await db.ExternalOrganizations.AsNoTracking()
                .Where(o => o.Id == oid).Select(o => o.Name).FirstOrDefaultAsync(ct);
            if (linked is null) orgId = null;          // stale id → fall back to the typed name
            else orgName = linked;
        }

        var m = new DiwanMail
        {
            InstitutionId = institutionId.Value,
            Direction = r.Direction,
            RegisterNumber = await NextNumberAsync(institutionId.Value, r.Direction, ct),
            ReferenceNumber = r.ReferenceNumber,
            ExternalOrgId = orgId,
            ExternalOrgName = orgName,
            AddresseeName = r.AddresseeName,
            AddresseeTitle = r.AddresseeTitle,
            Subject = (r.Subject ?? string.Empty).Trim(),
            Description = r.Description,
            Body = r.Body,
            Keywords = r.Keywords,
            MailDate = r.MailDate,
            RegistrationDate = DateTime.UtcNow,
            IssueDate = r.IssueDate,
            SentDate = r.SentDate,
            DueDate = r.DueDate,
            ResponseDeadline = r.ResponseDeadline ?? (r.ResponsePeriodDays is > 0 ? r.MailDate.AddDays(r.ResponsePeriodDays.Value) : null),
            ResponsePeriodDays = r.ResponsePeriodDays,
            RemindOnNoReply = r.RemindOnNoReply,
            ActionType = r.ActionType,
            CategoryId = r.CategoryId,
            ReferenceToId = r.ReferenceToId,
            Classification = r.Classification,
            Priority = r.Priority,
            Status = CorrespondenceStatus.Registered,
            FileNumber = r.FileNumber,
            CreatedBy = Uid,
        };
        db.DiwanMails.Add(m);
        await db.SaveChangesAsync(ct);
        await SetBureausAsync(m.Id, r.ToDiwanOrgUnitIds, ct);
        await audit.WriteAsync("Registered", EntityType, m.Id, m.Subject, ct: ct);
        return Ok(await LoadDetailAsync(m.Id, ct));
    }

    public async Task<Result<DiwanDetailDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        // Same scope as the list — otherwise anything hidden from the register is still readable by id.
        var q = await ScopedAsync(db.DiwanMails.AsNoTracking(), ct);
        var allowed = await q.AnyAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct);
        return allowed ? Ok(await LoadDetailAsync(id, ct)) : Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");
    }

    public async Task<PagedResult<DiwanListItemDto>> ListAsync(DiwanListFilter f, CancellationToken ct = default)
    {
        var q = db.DiwanMails.AsNoTracking().Where(x => x.Classification <= currentUser.Clearance);
        q = f.Folder switch
        {
            "incoming" => q.Where(x => x.Direction == DiwanDirection.Incoming),
            "outgoing" => q.Where(x => x.Direction == DiwanDirection.Outgoing),
            "drafts" => q.Where(x => x.Status == CorrespondenceStatus.Draft),
            "deleted" => q.IgnoreQueryFilters().Where(x => x.IsDeleted),
            _ => q,
        };
        if (f.Direction is { } dir) q = q.Where(x => x.Direction == dir);
        if (f.Status is { } st) q = q.Where(x => x.Status == st);
        if (f.Classification is { } cl) q = q.Where(x => x.Classification == cl);
        if (f.From is { } from) q = q.Where(x => x.MailDate >= from);
        if (f.To is { } to) q = q.Where(x => x.MailDate <= to);
        q = await ScopedAsync(q, ct);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, f.Page);
        var size = Math.Clamp(f.PageSize, 1, 100);
        var rows = await q.OrderByDescending(x => x.RegistrationDate).Skip((page - 1) * size).Take(size)
            .Select(x => new DiwanListItemDto(
                x.Id, x.RegisterNumber, x.ReferenceNumber, x.Direction.ToString(), x.Subject, x.ExternalOrgName,
                x.AddresseeName, x.Classification.ToString(), x.Priority.ToString(), x.Status.ToString(),
                x.ActionType.HasValue ? x.ActionType.ToString() : null,
                x.Category != null ? x.Category.Name : null,
                x.MailDate, x.RegistrationDate, x.DueDate, x.ResponseDeadline, x.IsArchived, x.Attachments.Any(),
                x.CategoryId))
            .ToListAsync(ct);
        return new PagedResult<DiwanListItemDto> { Items = rows, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<PagedResult<DiwanListItemDto>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0) return new PagedResult<DiwanListItemDto> { Items = [], Page = 1, PageSize = pageSize, TotalCount = 0 };
        var q = db.DiwanMails.AsNoTracking().Where(x => x.Classification <= currentUser.Clearance)
            .Where(x => EF.Functions.Like(x.Subject, $"%{term}%")
                     || (x.Body != null && EF.Functions.Like(x.Body, $"%{term}%"))
                     || (x.Keywords != null && EF.Functions.Like(x.Keywords, $"%{term}%"))
                     || (x.ExternalOrgName != null && EF.Functions.Like(x.ExternalOrgName, $"%{term}%")));
        q = await ScopedAsync(q, ct);
        var total = await q.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 100);
        var pg = Math.Max(1, page);
        var rows = await q.OrderByDescending(x => x.RegistrationDate).Skip((pg - 1) * size).Take(size)
            .Select(x => new DiwanListItemDto(
                x.Id, x.RegisterNumber, x.ReferenceNumber, x.Direction.ToString(), x.Subject, x.ExternalOrgName,
                x.AddresseeName, x.Classification.ToString(), x.Priority.ToString(), x.Status.ToString(),
                x.ActionType.HasValue ? x.ActionType.ToString() : null,
                x.Category != null ? x.Category.Name : null,
                x.MailDate, x.RegistrationDate, x.DueDate, x.ResponseDeadline, x.IsArchived, x.Attachments.Any(),
                x.CategoryId))
            .ToListAsync(ct);
        return new PagedResult<DiwanListItemDto> { Items = rows, Page = pg, PageSize = size, TotalCount = total };
    }

    // ── من يطّلع على البريد — per-letter access ────────────────────────────────

    /// <summary>Whether the caller may say who reads this letter. The administrator may; so may a
    /// head of a department the letter reached, or of the department that registered it — the same
    /// two arms that let them read it in the first place. Nobody else, including an employee who
    /// was let in: a grant lets you read the letter, not hand it on.</summary>
    private async Task<bool> CanManageAccessAsync(long mailId, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return true;
        var managed = await scope.ManagedOrgUnitIdsAsync(ct);
        if (managed.Count == 0) return false;

        return await db.DiwanBureaus.AnyAsync(bu => bu.DiwanMailId == mailId && managed.Contains(bu.OrgUnitId), ct)
            || await db.DiwanMails.AnyAsync(m => m.Id == mailId
                && db.Users.Any(u => u.Id == m.CreatedBy && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value)), ct);
    }

    public async Task<Result<DiwanAccessViewDto>> GetAccessAsync(long id, CancellationToken ct = default)
    {
        // Read through the register's own scope: someone who cannot open the letter has no business
        // learning who else can.
        var q = await ScopedAsync(db.DiwanMails.AsNoTracking(), ct);
        var mail = await q.Where(x => x.Id == id && x.Classification <= currentUser.Clearance)
            .Select(x => new { x.Id, x.CreatedBy }).FirstOrDefaultAsync(ct);
        if (mail is null) return Result<DiwanAccessViewDto>.Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");

        var canManage = await CanManageAccessAsync(id, ct);

        // The heads who see it by virtue of the seat they hold — the units it was routed to, plus
        // the registrar's own unit.
        var reachedUnits = await db.DiwanBureaus.AsNoTracking()
            .Where(bu => bu.DiwanMailId == id).Select(bu => bu.OrgUnitId).ToListAsync(ct);
        if (mail.CreatedBy is { } author)
        {
            var authorUnit = await db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == author).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
            if (authorUnit is { } au) reachedUnits.Add(au);
        }
        var headIds = await db.OrgUnits.AsNoTracking()
            .Where(o => reachedUnits.Contains(o.Id) && o.ManagerPositionId != null)
            .Join(db.Positions.AsNoTracking(), o => o.ManagerPositionId, p => (long?)p.Id, (o, p) => p.CurrentOccupantUserId)
            .Where(uid => uid != null)
            .Select(uid => uid!.Value)
            .Distinct()
            .ToListAsync(ct);

        var granted = await db.DiwanAccess.AsNoTracking()
            .Where(a => a.DiwanMailId == id)
            .Select(a => new { a.UserId, a.Note })
            .ToListAsync(ct);

        // Registrar first, then the heads, then everyone let in — the order the access was acquired.
        var ordered = new List<(long UserId, string Reason, string? Note)>();
        if (mail.CreatedBy is { } reg) ordered.Add((reg, "Registrar", null));
        foreach (var h in headIds.Where(h => ordered.All(e => e.UserId != h)))
            ordered.Add((h, "Head", null));
        foreach (var g in granted.Where(g => ordered.All(e => e.UserId != g.UserId)))
            ordered.Add((g.UserId, "Granted", g.Note));

        var names = await NamesOfAsync(ordered.Select(e => e.UserId).ToList(), ct);
        var entries = ordered
            .Where(e => names.ContainsKey(e.UserId))
            .Select(e => new DiwanAccessDto(e.UserId, names[e.UserId].Name, names[e.UserId].Unit, e.Reason, e.Note))
            .ToList();

        var candidates = canManage ? await AccessCandidatesAsync(ct) : [];
        return Result<DiwanAccessViewDto>.Ok(new DiwanAccessViewDto(canManage, entries, candidates));
    }

    public async Task<Result<bool>> SetAccessAsync(long id, SetDiwanAccessRequest r, CancellationToken ct = default)
    {
        var q = await ScopedAsync(db.DiwanMails.AsNoTracking(), ct);
        var subject = await q.Where(x => x.Id == id && x.Classification <= currentUser.Clearance)
            .Select(x => x.Subject).FirstOrDefaultAsync(ct);
        if (subject is null) return FailBool("المراسلة غير موجودة أو لا تملك صلاحية الوصول");
        if (!await CanManageAccessAsync(id, ct))
            return FailBool("تحديد من يطّلع على البريد الخارجي من صلاحية مدير القسم");

        // Only people the caller could have chosen from. Sending an id straight to the endpoint is
        // otherwise a way to admit a colleague from a department that never received the letter.
        var allowed = (await AccessCandidatesAsync(ct)).Select(c => c.UserId).ToHashSet();
        var wanted = (r.UserIds ?? []).Distinct().Where(allowed.Contains).ToList();

        var existing = await db.DiwanAccess.Where(a => a.DiwanMailId == id).ToListAsync(ct);
        // Only the caller's own candidate pool is replaced: a grant made by the head of another
        // department that also received the letter is theirs, and is left alone.
        var removable = existing.Where(a => allowed.Contains(a.UserId) && !wanted.Contains(a.UserId)).ToList();
        if (removable.Count > 0) db.DiwanAccess.RemoveRange(removable);

        var already = existing.Select(a => a.UserId).ToHashSet();
        foreach (var uid in wanted.Where(u => !already.Contains(u)))
            db.DiwanAccess.Add(new DiwanAccess
            {
                DiwanMailId = id, UserId = uid, GrantedByUserId = Uid,
                Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(), CreatedBy = Uid,
            });

        if (removable.Count == 0 && wanted.Count == already.Count && wanted.All(already.Contains))
            return Result<bool>.Ok(true);                       // nothing changed — don't journal a non-event

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("EditAccess", EntityType, id, subject,
            oldValues: string.Join(',', already), newValues: string.Join(',', wanted), ct: ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>Staff the caller may let in: everyone in the departments they head. The
    /// administrator may choose from the whole institution.</summary>
    private async Task<IReadOnlyList<DiwanAccessCandidateDto>> AccessCandidatesAsync(CancellationToken ct)
    {
        var q = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!await scope.IsSystemAdminAsync(ct))
        {
            var managed = await scope.ManagedOrgUnitIdsAsync(ct);
            if (managed.Count == 0) return [];
            q = q.Where(u => (u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value))
                || db.UserOrgUnitAccess.Any(g => g.UserId == u.Id && managed.Contains(g.OrgUnitId)));
        }
        return await q.OrderBy(u => u.FullName)
            .Select(u => new DiwanAccessCandidateDto(u.Id, u.FullName, u.OrgUnit != null ? u.OrgUnit.Name : null))
            .ToListAsync(ct);
    }

    /// <summary>Names for a set of user ids, including any since deactivated — an access list that
    /// silently dropped them would read as though nobody had been let in.</summary>
    private async Task<Dictionary<long, (string Name, string? Unit)>> NamesOfAsync(
        List<long> userIds, CancellationToken ct) =>
        userIds.Count == 0
            ? []
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, UnitName = u.OrgUnit != null ? u.OrgUnit.Name : null })
                .ToDictionaryAsync(u => u.Id, u => (u.FullName, u.UnitName), ct);

    // ── Replies & chains ──────────────────────────────────────────────────────

    public async Task<Result<DiwanDetailDto>> ReplyAsync(long id, ReplyDiwanRequest r, CancellationToken ct = default)
    {
        var src = await LoadForActionAsync(id, ct);
        if (src is null) return Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");

        // A reply always travels the other way: a reply to وارد is صادر, and vice versa.
        var replyDirection = src.Direction == DiwanDirection.Incoming
            ? DiwanDirection.Outgoing
            : DiwanDirection.Incoming;

        var created = await CreateAsync(new SaveDiwanRequest(
            replyDirection,
            r.Subject,
            // The reply cites the original's register number as its own مرجع.
            src.RegisterNumber,
            src.ExternalOrgId,
            src.ExternalOrgName,
            src.AddresseeName,
            src.AddresseeTitle,
            r.Classification,
            r.Priority,
            r.MailDate ?? DateTime.UtcNow,
            null, null,
            r.DueDate,
            r.ResponseDeadline,
            null, false,
            src.ActionType,
            src.CategoryId,
            src.Id,                                   // ReferenceToId — مرجع الى
            // Fall back to the original's routing when the reply doesn't name its own bureaus.
            r.ToDiwanOrgUnitIds is { Count: > 0 }
                ? r.ToDiwanOrgUnitIds
                : await db.DiwanBureaus.AsNoTracking()
                    .Where(x => x.DiwanMailId == src.Id).Select(x => x.OrgUnitId).ToListAsync(ct),
            null,
            r.Body,
            src.Keywords,
            src.FileNumber), ct);
        if (!created.Succeeded) return created;

        // Flatten to a single root so a reply-to-a-reply still lands in the same chain.
        var reply = await db.DiwanMails.FirstAsync(x => x.Id == created.Value!.Id, ct);
        reply.ReplyToId = src.Id;
        reply.ParentId = src.ParentId ?? src.Id;
        src.Status = CorrespondenceStatus.Replied;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Replied", EntityType, src.Id, src.Subject, ct: ct);

        return Ok(await LoadDetailAsync(reply.Id, ct));
    }

    public async Task<IReadOnlyList<DiwanThreadNodeDto>> GetThreadAsync(long id, CancellationToken ct = default)
    {
        var root = await db.DiwanMails.AsNoTracking()
            .Where(x => x.Id == id && x.Classification <= currentUser.Clearance)
            .Select(x => new { x.Id, x.ParentId }).FirstOrDefaultAsync(ct);
        if (root is null) return [];
        var rootId = root.ParentId ?? root.Id;

        // The chain is the root, everything parented to it, and anything citing it as مرجع —
        // the last arm catches entries linked by reference without a formal reply.
        var members = await db.DiwanMails.AsNoTracking()
            .Where(x => (x.Id == rootId || x.ParentId == rootId || x.ReferenceToId == rootId)
                        && x.Classification <= currentUser.Clearance)
            .Select(x => new
            {
                x.Id, x.RegisterNumber, x.ReferenceNumber, x.Subject, x.Direction, x.Status,
                x.MailDate, x.ReplyToId, x.ParentId, x.ExternalOrgName, x.Body,
                HasAttachments = db.DiwanAttachments.Any(a => a.DiwanMailId == x.Id),
            })
            .ToListAsync(ct);

        return members
            .OrderBy(x => x.MailDate).ThenBy(x => x.Id)
            .Select(x => new DiwanThreadNodeDto(
                x.Id, x.RegisterNumber, x.ReferenceNumber, x.Subject,
                x.Direction.ToString(), x.Status.ToString(),
                x.Id == rootId ? "Root" : x.ReplyToId != null ? "Reply" : "Reference",
                x.MailDate, x.ExternalOrgName, x.Body, x.HasAttachments))
            .ToList();
    }

    public async Task<Result<long>> ConvertToInternalAsync(long id, ConvertToInternalRequest r, CancellationToken ct = default)
    {
        var src = await LoadForActionAsync(id, ct);
        if (src is null) return Result<long>.Fail("المراسلة غير موجودة أو لا تملك صلاحية الوصول");
        if (r.Recipients is null || r.Recipients.Count == 0)
            return Result<long>.Fail("اختر مستلمًا واحدًا على الأقل");

        // The register entry's type only carries over when it is also offered on the internal
        // side (Scope Internal or Both). An external-only type like مطالبة مالية would otherwise
        // end up on an internal message, where it means nothing and no letterhead matches it.
        var carriedCategoryId = src.CategoryId is { } srcCat
            && await db.MailCategories.AnyAsync(
                c => c.Id == srcCat && (c.Scope == MailCategoryScope.Internal || c.Scope == MailCategoryScope.Both), ct)
            ? src.CategoryId
            : null;

        // Compose through the internal service so numbering, per-user read rows and
        // notifications all behave exactly as a normally-composed message.
        var composed = await internalMail.ComposeAsync(new ComposeInternalRequest(
            string.IsNullOrWhiteSpace(r.Subject) ? src.Subject : r.Subject!,
            src.RegisterNumber,                       // cites the register entry as its reference
            r.Classification ?? src.Classification,
            r.Priority ?? src.Priority,
            r.DueDate ?? src.DueDate,
            r.ResponseDeadline ?? src.ResponseDeadline,
            null, false,
            r.ActionType ?? src.ActionType,
            carriedCategoryId,
            string.IsNullOrWhiteSpace(r.Notes) ? src.Description : r.Notes,
            r.Body ?? src.Body,
            src.Keywords,
            src.FileNumber,
            r.Recipients), ct);
        if (!composed.Succeeded) return Result<long>.Fail(composed.Error ?? "تعذّر إنشاء المراسلة الداخلية");

        var newId = composed.Value!.Id;

        if (r.CopyAttachments)
        {
            var files = await db.DiwanAttachments.AsNoTracking()
                .Where(a => a.DiwanMailId == src.Id).ToListAsync(ct);
            foreach (var f in files)
            {
                // Copy the bytes rather than sharing the storage key, so deleting one
                // message can never destroy the other's attachment.
                var stream = await storage.OpenAsync(f.StorageKey, ct);
                if (stream is null) continue;          // key already gone — skip, don't fail the convert
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                await stream.DisposeAsync();
                await internalMail.AddAttachmentAsync(newId, f.FileName, f.ContentType, ms.ToArray(), ct);
            }
        }

        await audit.WriteAsync("ConvertedToInternal", EntityType, src.Id, src.Subject,
            newValues: $"InternalMailId={newId}", ct: ct);
        return Result<long>.Ok(newId);
    }

    public async Task<Result<bool>> ChangeStatusAsync(long id, ChangeDiwanStatusRequest r, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return FailBool("المراسلة غير موجودة");
        if (!Enum.IsDefined(r.Status)) return FailBool("حالة غير صالحة");

        var old = m.Status;
        if (old == r.Status) return Result<bool>.Ok(true);   // nothing to do

        m.Status = r.Status;
        m.UpdatedAt = DateTime.UtcNow;
        m.UpdatedBy = Uid;

        // Filing to the archive is its own action (it creates a Document); setting the status
        // alone must not fake that, so the archive flags are deliberately left untouched here.
        await db.SaveChangesAsync(ct);

        var note = string.IsNullOrWhiteSpace(r.Note) ? "" : $" — {r.Note}";
        var when = (r.EffectiveDate ?? DateTime.UtcNow).ToString("u");
        // Named "Edit…" deliberately: AuditWriter only journals create/edit/delete/download/print,
        // and a status change is a record edit that must appear in the monitoring log.
        await audit.WriteAsync("EditStatus", EntityType, id, m.Subject,
            oldValues: old.ToString(),
            newValues: $"{r.Status}{note} @ {when}", ct: ct);
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
            m.Subject, m.RegisterNumber, m.Body, m.Description, m.Keywords, m.Classification, m.MailDate, ct);
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
        var m = await db.DiwanMails.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted && x.Classification <= currentUser.Clearance, ct);
        if (m is null) return FailBool("المراسلة غير موجودة في المحذوفات");
        m.IsDeleted = false; m.DeletedAt = null; m.DeletedBy = null;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Restored", EntityType, id, m.Subject, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<DiwanAttachmentDto>> AddAttachmentAsync(long id, string fileName, string? contentType, byte[] content, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return Result<DiwanAttachmentDto>.Fail("المراسلة غير موجودة");
        if (content.Length == 0) return Result<DiwanAttachmentDto>.Fail("الملف فارغ");
        using var ms = new MemoryStream(content);
        var stored = await storage.SaveAsync($"diwan/{id}", fileName, ms, ct);
        var att = new DiwanAttachment
        {
            DiwanMailId = id, StorageKey = stored.StorageKey, Checksum = stored.Checksum,
            FileName = fileName, ContentType = contentType, SizeBytes = stored.SizeBytes,
            OcrStatus = CorrespondenceOcrStatus.Pending, CreatedBy = Uid,
        };
        db.DiwanAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("AttachmentAdded", EntityType, id, fileName, ct: ct);
        return Result<DiwanAttachmentDto>.Ok(new DiwanAttachmentDto(att.Id, att.StorageKey, att.FileName, att.ContentType, att.SizeBytes, att.Version, att.OcrStatus.ToString()));
    }

    /// <summary>Opens a register attachment's bytes. Goes through the same clearance check as the
    /// letter itself, so a download can never expose a classification the caller may not see.</summary>
    public async Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long id, long attachmentId, CancellationToken ct = default)
    {
        var m = await LoadForActionAsync(id, ct);
        if (m is null) return Result<AttachmentDownload>.Fail("المراسلة غير موجودة");

        var att = await db.DiwanAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DiwanMailId == id, ct);
        if (att is null) return Result<AttachmentDownload>.Fail("المرفق غير موجود");

        var stream = await storage.OpenAsync(att.StorageKey, ct);
        if (stream is null) return Result<AttachmentDownload>.Fail("ملف المرفق غير موجود في وحدة التخزين");

        await audit.WriteAsync("DownloadAttachment", EntityType, id, m.Subject, newValues: att.FileName, ct: ct);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(stream, att.FileName, att.ContentType ?? "application/octet-stream"));
    }

    // ── helpers ──
    private static Result<DiwanDetailDto> Fail(string m) => Result<DiwanDetailDto>.Fail(m);
    private static Result<DiwanDetailDto> Ok(DiwanDetailDto d) => Result<DiwanDetailDto>.Ok(d);
    private static Result<bool> FailBool(string m) => Result<bool>.Fail(m);

    /// <summary>Next register number for the direction and year. Derived from the highest number
    /// already issued, not from COUNT+1: any gap in the sequence (a deleted record, a number
    /// assigned out of band) made COUNT+1 repeat a number the unique index then rejected.</summary>
    private async Task<string> NextNumberAsync(long institutionId, DiwanDirection dir, CancellationToken ct)
    {
        var prefix = $"{(dir == DiwanDirection.Incoming ? "IN" : "OUT")}-{DateTime.UtcNow.Year}-";
        var last = await db.DiwanMails.IgnoreQueryFilters()
            .Where(x => x.InstitutionId == institutionId && x.Direction == dir && x.RegisterNumber.StartsWith(prefix))
            .OrderByDescending(x => x.RegisterNumber)
            .Select(x => x.RegisterNumber)
            .FirstOrDefaultAsync(ct);

        var seq = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{seq:00000}";
    }

    private Task<DiwanMail?> LoadForActionAsync(long id, CancellationToken ct) =>
        db.DiwanMails.FirstOrDefaultAsync(x => x.Id == id && x.Classification <= currentUser.Clearance, ct);

    private async Task<DiwanDetailDto> LoadDetailAsync(long id, CancellationToken ct)
    {
        var m = await db.DiwanMails.AsNoTracking()
            .Include(x => x.Attachments).Include(x => x.Category)
            .FirstAsync(x => x.Id == id, ct);

        var refNumber = m.ReferenceToId is { } rid
            ? await db.DiwanMails.AsNoTracking().Where(x => x.Id == rid).Select(x => x.RegisterNumber).FirstOrDefaultAsync(ct) : null;

        var toUnits = await db.DiwanBureaus.AsNoTracking()
            .Where(x => x.DiwanMailId == id)
            .Select(x => new OrgUnitRefDto(x.OrgUnitId, x.OrgUnit.Name))
            .ToListAsync(ct);

        // The author's department, so printing can pick that department's letterhead.
        var authorOrgUnitId = m.CreatedBy is { } authorId
            ? await db.Users.IgnoreQueryFilters().Where(u => u.Id == authorId)
                .Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct)
            : null;

        return new DiwanDetailDto(
            m.Id, m.RegisterNumber, m.ReferenceNumber, m.Direction.ToString(), m.ExternalOrgId, m.ExternalOrgName,
            m.AddresseeName, m.AddresseeTitle, m.Subject, m.Description, m.Body, m.Keywords,
            m.Classification.ToString(), m.Priority.ToString(), m.Status.ToString(),
            m.ActionType.HasValue ? m.ActionType.ToString() : null, m.ResponsePeriodDays, m.RemindOnNoReply,
            m.FileNumber, m.IsArchived, m.ArchivedDocumentId,
            m.MailDate, m.RegistrationDate, m.IssueDate, m.SentDate, m.DueDate, m.ResponseDeadline,
            m.CategoryId, m.Category?.Name, m.ReferenceToId, refNumber, toUnits,
            m.Attachments.Select(a => new DiwanAttachmentDto(a.Id, a.StorageKey, a.FileName, a.ContentType, a.SizeBytes, a.Version, a.OcrStatus.ToString())).ToList(),
            m.CreatedBy, authorOrgUnitId);
    }

    /// <summary>Replace a letter's الى ديوان routing with the given units (de-duplicated, existence-checked).</summary>
    private async Task SetBureausAsync(long mailId, IReadOnlyList<long>? unitIds, CancellationToken ct)
    {
        var existing = await db.DiwanBureaus.Where(x => x.DiwanMailId == mailId).ToListAsync(ct);
        if (existing.Count > 0) db.DiwanBureaus.RemoveRange(existing);

        var wanted = (unitIds ?? []).Distinct().ToList();
        if (wanted.Count > 0)
        {
            // Drop ids that no longer exist rather than failing the whole save.
            var valid = await db.OrgUnits.AsNoTracking()
                .Where(u => wanted.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
            foreach (var uid in valid)
                db.DiwanBureaus.Add(new DiwanBureau { DiwanMailId = mailId, OrgUnitId = uid, CreatedBy = Uid });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>تصنيف البريد lookup, seeded with a default set on first use.</summary>
    public async Task<IReadOnlyList<MailCategoryDto>> ListCategoriesAsync(
        int? scope = null, bool includeInactive = false, CancellationToken ct = default)
    {
        await SeedCategoriesAsync(ct);

        var q = db.MailCategories.AsNoTracking();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        if (scope is { } s && Enum.IsDefined(typeof(MailCategoryScope), s))
        {
            // A type marked Both belongs to either register, so it is never filtered out.
            var side = (MailCategoryScope)s;
            q = q.Where(c => c.Scope == side || c.Scope == MailCategoryScope.Both);
        }
        return await q.OrderBy(c => c.Scope).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => ToCategoryDto(c)).ToListAsync(ct);
    }

    public async Task<Result<MailCategoryDto>> CreateCategoryAsync(SaveMailCategoryRequest r, CancellationToken ct = default)
    {
        var err = ValidateCategory(r);
        if (err is not null) return Result<MailCategoryDto>.Fail(err);
        var institutionId = await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (institutionId is null) return Result<MailCategoryDto>.Fail("لا توجد مؤسسة مُعرّفة");

        var e = new MailCategory
        {
            InstitutionId = institutionId.Value,
            Name = r.Name.Trim(),
            NameEn = string.IsNullOrWhiteSpace(r.NameEn) ? null : r.NameEn.Trim(),
            Scope = (MailCategoryScope)r.Scope,
            SortOrder = r.SortOrder,
            IsActive = r.IsActive,
            CreatedBy = Uid,
        };
        db.MailCategories.Add(e);
        await db.SaveChangesAsync(ct);
        return Result<MailCategoryDto>.Ok(ToCategoryDto(e));
    }

    public async Task<Result<MailCategoryDto>> UpdateCategoryAsync(long id, SaveMailCategoryRequest r, CancellationToken ct = default)
    {
        var e = await db.MailCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<MailCategoryDto>.Fail("التصنيف غير موجود");
        var err = ValidateCategory(r);
        if (err is not null) return Result<MailCategoryDto>.Fail(err);

        e.Name = r.Name.Trim();
        e.NameEn = string.IsNullOrWhiteSpace(r.NameEn) ? null : r.NameEn.Trim();
        e.Scope = (MailCategoryScope)r.Scope;
        e.SortOrder = r.SortOrder;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<MailCategoryDto>.Ok(ToCategoryDto(e));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var e = await db.MailCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<bool>.Fail("التصنيف غير موجود");

        // Letters keep their classification for good, so a type in use is never pulled out from
        // under them. Print designs narrowed to it are widened instead — the layout is real work.
        if (await db.DiwanMails.IgnoreQueryFilters().AnyAsync(m => m.CategoryId == id, ct)
            || await db.InternalMails.IgnoreQueryFilters().AnyAsync(m => m.CategoryId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف تصنيف مستخدم في مراسلات — يمكنك تعطيله بدلاً من ذلك");

        await db.ReportTemplates.Where(t => t.MailCategoryId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MailCategoryId, (long?)null), ct);
        db.MailCategories.Remove(e);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    // ---- category helpers ----

    private static MailCategoryDto ToCategoryDto(MailCategory c) =>
        new(c.Id, c.Name, c.NameEn, c.SortOrder, c.Scope.ToString(), c.IsActive);

    private static string? ValidateCategory(SaveMailCategoryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "اسم التصنيف مطلوب";
        if (!Enum.IsDefined(typeof(MailCategoryScope), r.Scope)) return "نطاق التصنيف غير صالح";
        return null;
    }

    /// <summary>Fills the lookup on first use: the external register's classic set, plus the
    /// internal vouchers staff exchange. Runs once — a later empty table means someone deleted
    /// them deliberately, so only a completely empty table is seeded.</summary>
    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await db.MailCategories.AnyAsync(ct)) return;
        var institutionId = await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (institutionId is not { } inst) return;

        var defaults = new (string Name, MailCategoryScope Scope)[]
        {
            ("عام", MailCategoryScope.Both),
            ("إبداء رأي", MailCategoryScope.External),
            ("استفسار", MailCategoryScope.External),
            ("شكوى", MailCategoryScope.External),
            ("تدريب", MailCategoryScope.External),
            ("دعوة", MailCategoryScope.External),
            ("تعميم", MailCategoryScope.Both),
            ("طلب بيانات", MailCategoryScope.External),
            ("مطالبة مالية", MailCategoryScope.External),
            ("قسيمة راتب", MailCategoryScope.Internal),
            ("سند قبض", MailCategoryScope.Internal),
            ("سند دفع", MailCategoryScope.Internal),
        };
        for (var i = 0; i < defaults.Length; i++)
        {
            db.MailCategories.Add(new MailCategory
            {
                InstitutionId = inst, Name = defaults[i].Name, Scope = defaults[i].Scope,
                SortOrder = i, IsActive = true, CreatedBy = Uid,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
