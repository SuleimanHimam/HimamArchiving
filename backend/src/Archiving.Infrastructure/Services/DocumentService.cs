using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Documents;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Events;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class DocumentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditWriter audit,
    IWorkflowService workflow,
    IAccessScope scope) : IDocumentService
{
    private const string EntityType = "Document";

    // Formats accepted by the system (spec §3): PDF / DOCX / XLSX / JPG / PNG / ZIP.
    // ---- Document types ----

    /// <summary>Types offered to a department: its own plus the shared (عام) ones.
    /// Without <paramref name="orgUnitId"/> the whole catalogue is returned.</summary>
    public async Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(long? orgUnitId, CancellationToken ct = default)
    {
        var q = db.DocumentTypes.AsQueryable();
        if (orgUnitId is { } uid) q = q.Where(t => t.OrgUnitId == null || t.OrgUnitId == uid);
        return await q.OrderBy(t => t.OrgUnitId == null ? 0 : 1).ThenBy(t => t.Number).ThenBy(t => t.Name)
            .Select(t => new DocumentTypeDto(t.Id, t.Name, t.NameEn, t.Code, t.CategoryId,
                t.DefaultConfidentiality.ToString(), t.RetentionMonths, t.RequiresApproval,
                t.AllowedUploadSources.ToString(), t.IsActive,
                t.RetentionValue, t.RetentionUnit.ToString(),
                t.OrgUnitId, t.OrgUnit != null ? t.OrgUnit.Name : null, t.Number, t.Note))
            .ToListAsync(ct);
    }

    public async Task<Result<DocumentTypeDto>> CreateTypeAsync(CreateDocumentTypeRequest r, CancellationToken ct = default)
    {
        // The same two checks UpdateTypeAsync makes. Creation was missing them, which let a blank
        // name through — and a nameless type shows as an empty row in every type dropdown in the app.
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<DocumentTypeDto>.Fail("اسم النوع مطلوب");
        if (r.CategoryId is { } cid && !await db.DocumentCategories.AnyAsync(c => c.Id == cid, ct))
            return Result<DocumentTypeDto>.Fail("التصنيف غير موجود");

        var e = new DocumentType
        {
            Name = r.Name, NameEn = r.NameEn, CategoryId = r.CategoryId,
            Code = string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim(),   // optional identification code
            DefaultConfidentiality = r.DefaultConfidentiality,
            RetentionMonths = r.RetentionMonths <= 0 ? 120 : r.RetentionMonths,
            RequiresApproval = r.RequiresApproval,
            AllowedUploadSources = r.AllowedUploadSources == UploadSource.None ? UploadSource.All : r.AllowedUploadSources,
            IsActive = r.IsActive,
            OrgUnitId = r.OrgUnitId,          // null = عام, offered to every department
            Number = r.Number,
            Note = r.Note,
            RetentionValue = r.RetentionValue,
            RetentionUnit = r.RetentionUnit,
        };
        // RetentionMonths is derived, never trusted from the client.
        e.RetentionMonths = MonthsFrom(e.RetentionValue, e.RetentionUnit);
        db.DocumentTypes.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<DocumentTypeDto>.Ok(ToTypeDto(e));
    }

    public async Task<Result<DocumentTypeDto>> UpdateTypeAsync(long id, UpdateDocumentTypeRequest r, CancellationToken ct = default)
    {
        var e = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<DocumentTypeDto>.Fail("نوع الوثيقة غير موجود");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<DocumentTypeDto>.Fail("اسم النوع مطلوب");
        if (r.CategoryId is { } cid && !await db.DocumentCategories.AnyAsync(c => c.Id == cid, ct))
            return Result<DocumentTypeDto>.Fail("التصنيف غير موجود");

        e.Name = r.Name; e.NameEn = r.NameEn; e.CategoryId = r.CategoryId;
        e.Code = string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim();   // optional identification code
        e.DefaultConfidentiality = r.DefaultConfidentiality;
        e.RetentionMonths = r.RetentionMonths <= 0 ? 120 : r.RetentionMonths;
        e.RequiresApproval = r.RequiresApproval;
        e.AllowedUploadSources = r.AllowedUploadSources == UploadSource.None ? UploadSource.All : r.AllowedUploadSources;
        e.IsActive = r.IsActive;
        e.OrgUnitId = r.OrgUnitId;
        e.Number = r.Number;
        e.Note = r.Note;
        e.RetentionValue = r.RetentionValue;
        e.RetentionUnit = r.RetentionUnit;
        e.RetentionMonths = MonthsFrom(e.RetentionValue, e.RetentionUnit);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<DocumentTypeDto>.Ok(ToTypeDto(e));
    }

    /// <summary>Hands a document to the Archive department for physical filing: parks it in
    /// PendingArchive, where it appears in the archive's intake queue
    /// (<see cref="ListArchiveIntakeAsync"/>) until an archivist files or returns it.
    ///
    /// A workflow instance is opened alongside it when the archive-intake definition is configured,
    /// purely so the hand-over shows up in the document's timeline. It is not what moves the
    /// document any more — the queue is — so a system with no such definition still archives
    /// normally rather than refusing the submission.</summary>
    public async Task<Result<bool>> SendToArchiveAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");
        if (!doc.DocumentType.RequiresPhysicalArchiving)
            return Result<bool>.Fail("نوع هذه الوثيقة لا يتطلب أرشفة فيزيائية");

        // The document must belong to the caller's department (or the caller uploaded it).
        var myDept = await db.Users.Where(u => u.Id == currentUser.UserId).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        if (doc.OwningOrgUnitId != myDept && doc.CreatedBy != currentUser.UserId)
            return Result<bool>.Fail("لا يمكنك إرسال وثيقة لا تتبع قسمك");

        if (doc.Status == DocumentStatus.Archived) return Result<bool>.Fail("الوثيقة مؤرشفة بالفعل");
        if (doc.ArchiveWorkflowInstanceId is { } wf &&
            await db.WorkflowInstances.AnyAsync(w => w.Id == wf && w.Status == WorkflowStatus.Running, ct))
            return Result<bool>.Fail("الوثيقة قيد الأرشفة حاليًا");

        var defId = await db.WorkflowDefinitions
            .Where(d => d.Name == DbSeeder.ArchiveIntakeWorkflowName && d.IsActive)
            .Select(d => d.Id).FirstOrDefaultAsync(ct);
        var started = defId == 0
            ? null
            : (await workflow.StartAsync(new StartWorkflowRequest(defId, "Document", doc.Id), ct)).Value;

        var previousStatus = doc.Status;
        doc.Status = DocumentStatus.PendingArchive;
        doc.ArchiveWorkflowInstanceId = started?.Id;
        doc.SentToArchiveAtUtc = DateTime.UtcNow;
        /* The previous return reason deliberately survives the re-send: on a second attempt the
           archivist's first question is "was the thing I objected to actually fixed", and clearing
           it here left them re-deriving that from the audit log. It is cleared when the document is
           finally filed, and the submitter's copy of the objection is hidden while it waits. */
        doc.AddDomainEvent(new DocumentStatusChangedEvent(doc.Id, previousStatus.ToString(), doc.Status.ToString()));

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("SentToArchive", EntityType, doc.Id, doc.Title, ct: ct);

        // The archive works from a queue, not from a notification — but a department that has just
        // handed over a box of papers expects someone to know it arrived.
        await NotifyArchiveStaffAsync(doc, ct);
        return Result<bool>.Ok(true);
    }

    // ── The Archive department's side of the hand-over ──────────────────────────────────────
    //
    // Sending parks a document in PendingArchive; these three are what get it out again. Filing is
    // the only path to Archived for a document that came from another department, which is what
    // makes "the archive decides where paper lives" true rather than a convention.

    public async Task<IReadOnlyList<ArchiveIntakeItem>> ListArchiveIntakeAsync(CancellationToken ct = default)
    {
        // Clearance still applies to the archive: custody is not a licence to read secret files.
        var maxClearance = (int)currentUser.Clearance;

        return await db.Documents
            .Where(d => d.Status == DocumentStatus.PendingArchive
                     && !d.IsTombstone
                     && (int)d.Confidentiality <= maxClearance)
            // Oldest first: the queue is a waiting line, and the thing waiting longest is the one
            // someone is chasing.
            .OrderBy(d => d.SentToArchiveAtUtc ?? d.UpdatedAt ?? d.CreatedAt)
            .Select(d => new ArchiveIntakeItem(
                d.Id, d.DocumentNumber, d.Title,
                d.DocumentType.Name,
                d.OwningOrgUnitId, d.OwningOrgUnit.Name,
                d.Confidentiality.ToString(),
                d.DocumentDate,
                // Same fallback the ordering uses: documents that were already waiting before this
                // column existed would otherwise show no waiting time at all.
                d.SentToArchiveAtUtc ?? d.UpdatedAt ?? d.CreatedAt,
                db.Users.Where(u => u.Id == d.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                d.Attachments.Count,
                d.ArchiveReturnReason))
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> FileToArchiveAsync(long id, FileToArchiveRequest r, CancellationToken ct = default)
    {
        var doc = await LoadForIntakeAsync(id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (doc.Status != DocumentStatus.PendingArchive)
            return Result<bool>.Fail("هذه الوثيقة ليست ضمن وارد الأرشفة");
        if (!await CanFilePhysicallyAsync(ct)) return Result<bool>.Fail(NoFilingRightsError);

        var box = await db.Boxes.FirstOrDefaultAsync(b => b.Id == r.BoxId, ct);
        if (box is null) return Result<bool>.Fail("الصندوق غير موجود");
        if (!box.IsActive) return Result<bool>.Fail("الصندوق غير مفعّل");
        // Capacity is the shelf's answer, not a suggestion: a box recorded as holding more than it
        // does makes the whole register wrong about where a paper is.
        if (box.Capacity is { } cap && box.CurrentCount >= cap)
            return Result<bool>.Fail("الصندوق ممتلئ — اختر صندوقًا آخر");

        var oldBox = doc.BoxId;
        doc.BoxId = box.Id;
        doc.TransitionTo(DocumentStatus.Archived, currentUser.UserId?.ToString());
        doc.ArchiveReturnReason = null;
        await db.SaveChangesAsync(ct);
        await AdjustBoxCountAsync(oldBox, box.Id, ct);

        await CloseIntakeWorkflowAsync(doc, WorkflowStatus.Completed, ct);
        await audit.WriteAsync("Archived", EntityType, doc.Id, doc.Title,
            newValues: $"box={box.BoxCode}" + (string.IsNullOrWhiteSpace(r.Note) ? "" : $"; {r.Note.Trim()}"), ct: ct);

        Notify(doc.CreatedBy, "تمت أرشفة وثيقتك",
            $"حُفظت «{doc.Title}» في الصندوق {box.BoxCode}." +
            (string.IsNullOrWhiteSpace(r.Note) ? "" : $" ملاحظة قسم الأرشيف: {r.Note.Trim()}"), doc.Id);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ReturnFromArchiveAsync(long id, ReturnFromArchiveRequest r, CancellationToken ct = default)
    {
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) return Result<bool>.Fail("سبب الإرجاع مطلوب");

        var doc = await LoadForIntakeAsync(id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (doc.Status != DocumentStatus.PendingArchive)
            return Result<bool>.Fail("هذه الوثيقة ليست ضمن وارد الأرشفة");
        if (!await CanFilePhysicallyAsync(ct)) return Result<bool>.Fail(NoFilingRightsError);

        // Back to Active, not to some "rejected" limbo: the submitting department has to be able to
        // fix the thing and send it again, and the reason is what tells them what to fix.
        doc.TransitionTo(DocumentStatus.Active, currentUser.UserId?.ToString());
        doc.ArchiveReturnReason = reason;
        doc.SentToArchiveAtUtc = null;
        await db.SaveChangesAsync(ct);

        await CloseIntakeWorkflowAsync(doc, WorkflowStatus.Cancelled, ct);
        await audit.WriteAsync("ArchiveReturned", EntityType, doc.Id, doc.Title, newValues: reason, ct: ct);

        Notify(doc.CreatedBy, "أُعيدت وثيقتك من قسم الأرشيف",
            $"«{doc.Title}» — السبب: {reason}. يُرجى التعديل وإعادة الإرسال.", doc.Id);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>A queue document, loaded without the ownership check the rest of the service applies:
    /// intake is precisely the case where a document from another department is the archivist's to
    /// act on. Clearance still holds — that gate is in the queue query and re-applied here.</summary>
    private async Task<Document?> LoadForIntakeAsync(long id, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsTombstone, ct);
        return doc is not null && WithinClearance(doc) ? doc : null;
    }

    /// <summary>Close out the workflow instance opened when the document was sent, if there is one.
    /// The instance is a record of the hand-over for the timeline; leaving it Running would show an
    /// archived document as still in transit.</summary>
    private async Task CloseIntakeWorkflowAsync(Document doc, WorkflowStatus status, CancellationToken ct)
    {
        if (doc.ArchiveWorkflowInstanceId is not { } instanceId) return;

        var instance = await db.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == instanceId, ct);
        if (instance is not null && instance.Status == WorkflowStatus.Running)
        {
            instance.Status = status;
            instance.CompletedAt = DateTime.UtcNow;
            instance.CurrentStageId = null;
            instance.CurrentAssigneePositionId = null;

            foreach (var task in await db.WorkflowTasks
                .Where(t => t.WorkflowInstanceId == instanceId && t.Status != WorkflowTaskStatus.Completed)
                .ToListAsync(ct))
            {
                task.Status = WorkflowTaskStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;
                task.CompletedByUserId = currentUser.UserId;
            }
        }

        doc.ArchiveWorkflowInstanceId = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Tell the archive department a document is waiting. Best-effort: a submission is not
    /// worth failing because a notification row could not be written.</summary>
    private async Task NotifyArchiveStaffAsync(Document doc, CancellationToken ct)
    {
        try
        {
            var recipients = await db.Users
                .Where(u => u.IsActive && u.Id != currentUser.UserId
                    && db.UserRoles.Any(ur => ur.UserId == u.Id
                        && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                            && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Archive.Archive"))))
                .Select(u => u.Id).ToListAsync(ct);

            foreach (var uid in recipients)
                Notify(uid, "وثيقة بانتظار الأرشفة",
                    $"وصلت «{doc.Title}» ({doc.DocumentNumber}) إلى وارد الأرشفة.", doc.Id);
            if (recipients.Count > 0) await db.SaveChangesAsync(ct);
        }
        catch { /* best-effort */ }
    }

    /// <summary>Queue an in-app notification; the caller saves. No-op for an unknown user.</summary>
    private void Notify(long? userId, string title, string body, long documentId)
    {
        if (userId is not { } uid) return;
        db.Notifications.Add(new Notification
        {
            RecipientUserId = uid,
            Title = title,
            Body = body,
            Type = NotificationType.Info,
            Channels = NotificationChannel.InApp,
            EntityType = EntityType,
            EntityId = documentId,
            CreatedBy = currentUser.UserId,
        });
    }

    public async Task<Result<bool>> DeleteTypeAsync(long id, CancellationToken ct = default)
    {
        var e = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<bool>.Fail("نوع الوثيقة غير موجود");
        if (await db.Documents.AnyAsync(d => d.DocumentTypeId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف نوع مستخدم في وثائق — يمكنك تعطيله بدلاً من ذلك");

        db.DocumentTypes.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    private static DocumentTypeDto ToTypeDto(DocumentType e) => new(
        e.Id, e.Name, e.NameEn, e.Code, e.CategoryId,
        e.DefaultConfidentiality.ToString(), e.RetentionMonths, e.RequiresApproval,
        e.AllowedUploadSources.ToString(), e.IsActive,
        e.RetentionValue, e.RetentionUnit.ToString(),
        e.OrgUnitId, e.OrgUnit?.Name, e.Number, e.Note);

    /// <summary>مدة الحفظ as entered (value + unit) reduced to the months the lifecycle works in.</summary>
    private static int MonthsFrom(int value, RetentionUnit unit) => unit switch
    {
        RetentionUnit.Days => Math.Max(1, (int)Math.Ceiling(value / 30.0)),
        RetentionUnit.Years => value * 12,
        _ => value,
    };

    // ---- Categories ----

    public async Task<IReadOnlyList<DocumentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await db.DocumentCategories.OrderBy(c => c.Name)
            .Select(c => new DocumentCategoryDto(c.Id, c.ParentId, c.Name, c.Code, c.IsActive))
            .ToListAsync(ct);

    public async Task<Result<DocumentCategoryDto>> CreateCategoryAsync(CreateDocumentCategoryRequest r, CancellationToken ct = default)
    {
        if (r.ParentId is { } pid && !await db.DocumentCategories.AnyAsync(c => c.Id == pid, ct))
            return Result<DocumentCategoryDto>.Fail("التصنيف الأب غير موجود");

        var e = new DocumentCategory { ParentId = r.ParentId, Name = r.Name, Code = r.Code };
        db.DocumentCategories.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<DocumentCategoryDto>.Ok(new DocumentCategoryDto(e.Id, e.ParentId, e.Name, e.Code, e.IsActive));
    }

    public async Task<Result<DocumentCategoryDto>> UpdateCategoryAsync(long id, UpdateDocumentCategoryRequest r, CancellationToken ct = default)
    {
        var e = await db.DocumentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<DocumentCategoryDto>.Fail("التصنيف غير موجود");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<DocumentCategoryDto>.Fail("اسم التصنيف مطلوب");
        if (r.ParentId == id) return Result<DocumentCategoryDto>.Fail("لا يمكن جعل التصنيف أبًا لنفسه");
        if (r.ParentId is { } pid && !await db.DocumentCategories.AnyAsync(c => c.Id == pid, ct))
            return Result<DocumentCategoryDto>.Fail("التصنيف الأب غير موجود");

        e.Name = r.Name.Trim(); e.Code = r.Code; e.ParentId = r.ParentId; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<DocumentCategoryDto>.Ok(new DocumentCategoryDto(e.Id, e.ParentId, e.Name, e.Code, e.IsActive));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var e = await db.DocumentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<bool>.Fail("التصنيف غير موجود");
        if (await db.DocumentCategories.AnyAsync(c => c.ParentId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف تصنيف يحتوي على تصنيفات فرعية");
        if (await db.Documents.AnyAsync(d => d.CategoryId == id, ct) || await db.DocumentTypes.AnyAsync(tp => tp.CategoryId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف تصنيف مستخدم — يمكنك تعطيله بدلاً من ذلك");

        db.DocumentCategories.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- Documents ----

    public async Task<PagedResult<DocumentListItem>> ListAsync(DocumentQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Documents
            .Where(d => d.IsLatestVersion)
            .Where(d => (int)d.Confidentiality <= (int)currentUser.Clearance); // clearance gate

        // Ownership gate: a document belongs to the person who registered it, to the department
        // that owns it, or to whoever it was explicitly shared with. Archive custodians and the
        // administrator are exempt — see IAccessScope.
        if (!await scope.SeesAllDocumentsAsync(ct))
        {
            var me = currentUser.UserId;
            var depts = await scope.OrgUnitIdsAsync(ct);
            q = q.Where(d => d.CreatedBy == me
                || depts.Contains(d.OwningOrgUnitId)
                || db.DocumentShares.Any(sh => sh.DocumentId == d.Id && sh.SharedWithUserId == me));
        }

        if (query.Status is { } status) q = q.Where(d => d.Status == status);
        if (query.DocumentTypeId is { } typeId) q = q.Where(d => d.DocumentTypeId == typeId);
        if (query.OwningOrgUnitId is { } unitId) q = q.Where(d => d.OwningOrgUnitId == unitId);
        if (query.DateFrom is { } df) q = q.Where(d => d.DocumentDate >= df);
        if (query.DateTo is { } dt) q = q.Where(d => d.DocumentDate <= dt);
        if (query.FolderId is { } folderId) q = q.Where(d => d.FolderId == folderId);
        else if (query.UnfiledOnly) q = q.Where(d => d.FolderId == null);
        if (query.BoxId is { } boxId) q = q.Where(d => d.BoxId == boxId);

        // A place at any level of the hierarchy. Resolved to the boxes underneath it first: the
        // document only records the box it sits in, so "everything in this building" has to be
        // asked as "everything in these boxes". An unknown or empty place matches nothing rather
        // than falling through to the whole archive — a filter that silently does nothing is
        // worse than one that shows an empty shelf.
        if (query.LocationKind is { Length: > 0 } kind && query.LocationId is { } placeId)
        {
            var boxIds = await BoxIdsUnderAsync(kind, placeId, ct);
            q = q.Where(d => d.BoxId != null && boxIds.Contains(d.BoxId.Value));
        }
        if (query.FavoritesOnly)
            q = q.Where(d => db.DocumentFavorites.Any(f => f.DocumentId == d.Id && f.UserId == currentUser.UserId));
        if (query.SharedWithMe)
            q = q.Where(d => db.DocumentShares.Any(sh => sh.DocumentId == d.Id && sh.SharedWithUserId == currentUser.UserId));

        // Filter by an admin-defined custom field value.
        if (query.CustomFieldId is { } cfId)
        {
            var cfVal = query.CustomFieldValue?.Trim();
            q = q.Where(d => db.CustomFieldValues.Any(v =>
                v.FieldId == cfId && v.EntityType == "Document" && v.EntityId == d.Id
                && (string.IsNullOrEmpty(cfVal) || v.Value.Contains(cfVal!))));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            // Words *inside* files, plus metadata and attachment file names.
            var contentDocIds = await SearchAttachmentTextAsync(s, ct);
            q = q.Where(d => d.DocumentNumber.Contains(s) || d.Title.Contains(s)
                || (d.Keywords != null && d.Keywords.Contains(s))
                || d.Attachments.Any(a => a.FileName.Contains(s))
                || contentDocIds.Contains(d.Id));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(d => new DocumentListItem(
                d.Id, d.DocumentNumber, d.Title, d.DocumentType.Name,
                d.Confidentiality.ToString(), d.Status.ToString(), d.Version,
                d.DocumentDate, d.ExpiryDate, d.CreatedAt,
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.PhysicalLocation.Name).FirstOrDefault(),
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.BoxNumber).FirstOrDefault(),
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.FileNumber).FirstOrDefault(),
                db.Boxes.Where(b => b.Id == d.BoxId).Select(b => b.BoxCode).FirstOrDefault(),
                d.Attachments.OrderBy(a => a.Id).Select(a => a.FileExtension).FirstOrDefault(),
                d.Attachments.Count(),
                d.Attachments.Sum(a => (long?)a.SizeBytes) ?? 0L,
                d.UpdatedAt,
                d.Attachments.OrderBy(a => a.Id).Select(a => (long?)a.Id).FirstOrDefault()))
            .ToListAsync(ct);

        // Attach admin-defined custom field values for the documents on this page.
        var ids = items.Select(i => i.Id).ToList();
        if (ids.Count > 0)
        {
            var values = await db.CustomFieldValues
                .Where(v => v.EntityType == "Document" && ids.Contains(v.EntityId))
                .Select(v => new { v.EntityId, v.FieldId, v.Value })
                .ToListAsync(ct);
            if (values.Count > 0)
            {
                var byDoc = values.GroupBy(v => v.EntityId)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.FieldId, x => x.Value));
                items = items.Select(it => byDoc.TryGetValue(it.Id, out var cv) ? it with { CustomValues = cv } : it).ToList();
            }
        }

        return new PagedResult<DocumentListItem> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<DocumentDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentDetail>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentDetail>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");
        var isFav = await db.DocumentFavorites.AnyAsync(f => f.DocumentId == id && f.UserId == currentUser.UserId, ct);
        var boxCode = doc.BoxId is { } bid ? await db.Boxes.Where(b => b.Id == bid).Select(b => b.BoxCode).FirstOrDefaultAsync(ct) : null;
        var detail = WithPhysical(ToDetail(doc), await GetPhysicalItemAsync(doc.Id, ct)) with { FolderId = doc.FolderId, IsFavorite = isFav, BoxCode = boxCode };
        return Result<DocumentDetail>.Ok(detail);
    }

    /// <summary>
    /// The code behind the printed QR.
    ///
    /// Issued lazily as well as at registration: every document filed before this feature existed
    /// still has to be printable with a QR, and the first person to open one is a better moment to
    /// mint it than a migration that rewrites the whole table. Guarded by the same access check as
    /// reading the document — the code is public once printed, but who may print it is not.
    /// </summary>
    public async Task<Result<DocumentVerificationDto>> GetVerificationAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentVerificationDto>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentVerificationDto>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        if (string.IsNullOrEmpty(doc.VerificationCode))
        {
            // The unique index is the real guarantee; the retry is for the vanishingly rare
            // collision, and giving up after a few tries beats looping on a broken RNG.
            for (var attempt = 0; attempt < 5 && string.IsNullOrEmpty(doc.VerificationCode); attempt++)
            {
                var candidate = DocumentVerification.NewCode();
                if (await db.Documents.AnyAsync(d => d.VerificationCode == candidate, ct)) continue;
                doc.VerificationCode = candidate;
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { doc.VerificationCode = null; }   // someone took it first
            }
            if (string.IsNullOrEmpty(doc.VerificationCode))
                return Result<DocumentVerificationDto>.Fail("تعذّر إصدار رمز التحقق");
        }

        var code = doc.VerificationCode!;
        return Result<DocumentVerificationDto>.Ok(new DocumentVerificationDto(
            doc.Id, doc.DocumentNumber, code, $"/verify/{code}", $"/api/public/verify/{code}/qr.png"));
    }

    public async Task<Result<DocumentProperties>> GetPropertiesAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentProperties>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentProperties>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        // Every user id the card needs, resolved in one round-trip. A document with ten
        // attachments uploaded by the same person must not cost ten name lookups.
        var userIds = doc.Attachments.Select(a => a.CreatedBy)
            .Append(doc.CreatedBy).Append(doc.UpdatedBy)
            .Where(u => u is not null).Select(u => u!.Value).Distinct().ToList();
        var names = userIds.Count == 0
            ? new Dictionary<long, string>()
            : await db.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(long? uid) => uid is { } v && names.TryGetValue(v, out var n) ? n : null;

        // Name, tint and symbol together — the card draws the same folder the browser does.
        var folder = doc.FolderId is { } fid
            ? await db.Folders.Where(f => f.Id == fid)
                .Select(f => new { f.Name, f.Color, f.Icon }).FirstOrDefaultAsync(ct)
            : null;
        var physical = await GetPhysicalItemAsync(doc.Id, ct);

        var files = doc.Attachments.OrderBy(a => a.Id).Select(a => new DocumentAttachmentInfo(
            a.Id, a.FileName, a.FileExtension, a.SizeBytes, a.IsScanned, a.CreatedAt,
            NameOf(a.CreatedBy))).ToList();

        return Result<DocumentProperties>.Ok(new DocumentProperties(
            doc.Id, doc.DocumentNumber, doc.Title, doc.DocumentType.Name,
            doc.Confidentiality.ToString(), doc.Status.ToString(), doc.Version,
            doc.DocumentDate,
            doc.CreatedAt, NameOf(doc.CreatedBy),
            doc.UpdatedAt, NameOf(doc.UpdatedBy),
            folder?.Name, folder?.Color, folder?.Icon, physical?.PhysicalLocation.Name,
            files.Count, files.Sum(f => f.SizeBytes), files));
    }

    public async Task<Result<DocumentDetail>> CreateAsync(CreateDocumentRequest r, CancellationToken ct = default)
    {
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == r.DocumentTypeId, ct);
        if (type is null) return Result<DocumentDetail>.Fail("نوع الوثيقة غير موجود");
        if (!await db.OrgUnits.AnyAsync(u => u.Id == r.OwningOrgUnitId, ct))
            return Result<DocumentDetail>.Fail("الوحدة المالكة غير موجودة");
        if (r.PhysicalLocationId is { } locId && !await db.PhysicalLocations.AnyAsync(l => l.Id == locId, ct))
            return Result<DocumentDetail>.Fail("مكان الحفظ الفيزيائي غير موجود");
        // Refused rather than quietly dropped: a registrar who believes they filed a paper original
        // on a shelf, and did not, is worse off than one who is told they cannot.
        if ((r.PhysicalLocationId is not null || r.BoxId is not null
             || !string.IsNullOrWhiteSpace(r.BoxNumber) || !string.IsNullOrWhiteSpace(r.FileNumber))
            && !await CanFilePhysicallyAsync(ct))
            return Result<DocumentDetail>.Fail(NoFilingRightsError);

        var doc = new Document
        {
            DocumentNumber = await NextDocumentNumberAsync(ct),
            VerificationCode = DocumentVerification.NewCode(),
            Title = r.Title,
            Description = r.Description,
            DocumentTypeId = r.DocumentTypeId,
            CategoryId = r.CategoryId,
            OwningOrgUnitId = r.OwningOrgUnitId,
            OwnerPositionId = r.OwnerPositionId,
            Confidentiality = r.Confidentiality,
            Status = DocumentStatus.Active,
            Keywords = r.Keywords,
            RetentionMonths = type.RetentionMonths,
            DocumentDate = r.DocumentDate,
            ExpiryDate = ComputeExpiry(r.DocumentDate, type.RetentionMonths),
            BoxId = r.BoxId,
            ArchiveMode = r.ArchiveMode,
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        await AdjustBoxCountAsync(null, r.BoxId, ct);   // filing into a box bumps its CurrentCount
        await audit.WriteAsync("Created", EntityType, doc.Id, doc.Title, ct: ct);

        // Link the physical storage location at entry time, if provided.
        if (r.PhysicalLocationId is { } location)
        {
            db.PhysicalArchiveItems.Add(new PhysicalArchiveItem
            {
                DocumentId = doc.Id,
                PhysicalLocationId = location,
                BoxNumber = r.BoxNumber,
                FileNumber = r.FileNumber,
                ArchivedByUserId = currentUser.UserId,
            });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Archived", "Document", doc.Id, doc.Title, newValues: $"location={location}", ct: ct);
        }

        await db.Entry(doc).Reference(d => d.DocumentType).LoadAsync(ct);
        return Result<DocumentDetail>.Ok(WithPhysical(ToDetail(doc), await GetPhysicalItemAsync(doc.Id, ct)));
    }

    public async Task<Result<DocumentDetail>> UpdateAsync(long id, UpdateDocumentRequest r, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentDetail>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentDetail>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        doc.Title = r.Title;
        doc.Description = r.Description;
        doc.CategoryId = r.CategoryId;
        doc.OwnerPositionId = r.OwnerPositionId;
        doc.Confidentiality = r.Confidentiality;
        doc.Keywords = r.Keywords;
        doc.DocumentDate = r.DocumentDate;
        // Admin-set expiry wins; otherwise derive it from the document date + retention.
        doc.ExpiryDate = r.ExpiryDate ?? ComputeExpiry(r.DocumentDate, doc.RetentionMonths);

        var oldBox = doc.BoxId;
        if (oldBox != r.BoxId)
        {
            // Same rule on edit as on registration — otherwise the restriction lasts one screen.
            if (!await CanFilePhysicallyAsync(ct)) return Result<DocumentDetail>.Fail(NoFilingRightsError);
            doc.BoxId = r.BoxId;
            await AdjustBoxCountAsync(oldBox, r.BoxId, ct);   // move between boxes updates both counts
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<DocumentDetail>.Ok(ToDetail(doc));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        if (doc.BoxId is not null) await AdjustBoxCountAsync(doc.BoxId, null, ct);   // free a slot in the box

        /* Anything still in flight about this document has to stop with it. A soft delete left
           running workflow instances behind, and the escalation sweep — which looks at open tasks,
           not at whether their subject still exists — kept chasing people about a document nobody
           could open. The notification arrived, the link led to a 404, and the reader was told they
           might lack permission. Closing the instance here is what makes that impossible. */
        await CancelWorkflowsForDeletedDocumentAsync(doc.Id, ct);

        // The AuditableEntityInterceptor converts this into a soft delete (stamps IsDeleted/DeletedAt/By).
        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- Trash ----------------------------------------------------------------------------------

    /// <summary>The caller's deleted documents, newest first.
    ///
    /// <para>Tombstoned rows are excluded: their content is already destroyed, so offering them for
    /// restore would hand back an empty shell. They live on as proof-of-prior-existence, not as
    /// something to recover.</para></summary>
    public async Task<IReadOnlyList<TrashItem>> ListTrashAsync(CancellationToken ct = default)
    {
        var q = db.Documents.IgnoreQueryFilters()
            .Where(d => d.IsDeleted && !d.IsTombstone)
            .Where(d => (int)d.Confidentiality <= (int)currentUser.Clearance);

        // Same ownership gate the register uses — deleting something does not widen who may see it.
        if (!await scope.SeesAllDocumentsAsync(ct))
        {
            var me = currentUser.UserId;
            var depts = await scope.OrgUnitIdsAsync(ct);
            q = q.Where(d => d.CreatedBy == me || depts.Contains(d.OwningOrgUnitId));
        }

        return await q
            .OrderByDescending(d => d.DeletedAt)
            .Select(d => new TrashItem(
                d.Id, d.DocumentNumber, d.Title, d.DocumentType.Name,
                d.Attachments.OrderBy(a => a.Id).Select(a => a.FileExtension).FirstOrDefault(),
                d.Attachments.Count(),
                d.Attachments.Sum(a => (long?)a.SizeBytes) ?? 0L,
                d.DeletedAt,
                db.Users.Where(u => u.Id == d.DeletedBy).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> RestoreAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!doc.IsDeleted) return Result<bool>.Fail("الوثيقة غير محذوفة");
        if (doc.IsTombstone) return Result<bool>.Fail("لا يمكن استرجاع وثيقة أُتلف محتواها");
        if (!await CanAccessAsync(doc, ct)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        doc.IsDeleted = false;
        doc.DeletedAt = null;
        doc.DeletedBy = null;
        // The box slot was freed on delete; taking it back is what keeps the shelf honest.
        if (doc.BoxId is not null) await AdjustBoxCountAsync(null, doc.BoxId, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Restored", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> PurgeAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!doc.IsDeleted) return Result<bool>.Fail("الوثيقة غير محذوفة");
        if (!await CanAccessAsync(doc, ct)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        await ShredContentAsync(doc, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Purged", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<int>> EmptyTrashAsync(CancellationToken ct = default)
    {
        var ids = (await ListTrashAsync(ct)).Select(t => t.Id).ToList();
        if (ids.Count == 0) return Result<int>.Ok(0);

        var docs = await db.Documents.IgnoreQueryFilters().Where(d => ids.Contains(d.Id)).ToListAsync(ct);
        foreach (var doc in docs) await ShredContentAsync(doc, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("TrashEmptied", EntityType, 0, $"{docs.Count}", ct: ct);
        return Result<int>.Ok(docs.Count);
    }

    /// <summary>Destroy a document's bytes and tombstone the row.
    ///
    /// <para>Crypto-shredding — dropping the wrapped data key — is what actually makes the ciphertext
    /// unreadable; the file itself is left where it is because overwriting adds nothing once the key
    /// is gone. The metadata row survives on purpose: an archive has to be able to say that a record
    /// existed and was destroyed, which a vanished row cannot do.</para></summary>
    private async Task ShredContentAsync(Document doc, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var atts = await db.DocumentAttachments
            .Where(a => a.DocumentId == doc.Id && !a.ContentDestroyed).ToListAsync(ct);

        foreach (var att in atts)
        {
            try { await storage.CryptoShredAsync(att.StorageKey, ct); }
            catch { /* an object already gone stays gone; the row is still marked below */ }
            att.ContentDestroyed = true;
            att.ContentDestroyedAt = now;
        }

        doc.IsTombstone = true;
        doc.DestroyedAtUtc = now;
    }

    /// <summary>Cancel every running workflow instance for a document being deleted, and close its
    /// open tasks. Cancelled rather than completed: nothing was decided, the subject simply went
    /// away, and the routing history stays readable as exactly that.</summary>
    private async Task CancelWorkflowsForDeletedDocumentAsync(long documentId, CancellationToken ct)
    {
        var instances = await db.WorkflowInstances
            .Where(w => w.EntityType == EntityType && w.EntityId == documentId && w.Status == WorkflowStatus.Running)
            .ToListAsync(ct);
        if (instances.Count == 0) return;

        var ids = instances.Select(w => w.Id).ToList();
        foreach (var instance in instances)
        {
            instance.Status = WorkflowStatus.Cancelled;
            instance.CompletedAt = DateTime.UtcNow;
            instance.CurrentStageId = null;
            instance.CurrentAssigneePositionId = null;
        }

        foreach (var task in await db.WorkflowTasks
            .Where(t => ids.Contains(t.WorkflowInstanceId) && t.Status != WorkflowTaskStatus.Completed)
            .ToListAsync(ct))
        {
            task.Status = WorkflowTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.CompletedByUserId = currentUser.UserId;
        }
    }

    // ---- Attachments ----

    public async Task<Result<DocumentAttachmentDto>> AddAttachmentAsync(
        long documentId, string fileName, string contentType, Stream content,
        bool isScanned = false, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<DocumentAttachmentDto>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentAttachmentDto>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        // Any file type is accepted: the archive holds whatever a department actually
        // works with, and a global whitelist only pushed people into renaming files or
        // zipping them — which loses the type instead of recording it. What a given
        // document type will take is still its own policy, enforced just below.
        var ext = Path.GetExtension(fileName);

        // Enforce the document type's permitted upload sources (e.g. scanner-only types).
        var sourceCheck = CheckUploadSource(doc.DocumentType.AllowedUploadSources, ext, isScanned);
        if (sourceCheck is not null) return Result<DocumentAttachmentDto>.Fail(sourceCheck);

        var stored = await storage.SaveAsync($"documents/{doc.Id}", fileName, content, ct);

        var att = new DocumentAttachment
        {
            DocumentId = doc.Id,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileExtension = ext.TrimStart('.').ToLowerInvariant(),
            SizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            Checksum = stored.Checksum,
            IsScanned = isScanned,
        };
        db.DocumentAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(isScanned ? "ScannedAttachmentAdded" : "AttachmentAdded",
            EntityType, doc.Id, doc.Title, newValues: fileName, ct: ct);

        return Result<DocumentAttachmentDto>.Ok(new DocumentAttachmentDto(
            att.Id, att.FileName, att.ContentType, att.FileExtension, att.SizeBytes, att.IsScanned, att.CreatedAt,
            att.Kind.ToString(), att.SourceAttachmentId, att.PdfAConformance, att.PreservationValidated));
    }

    /// <summary>Validates an upload against the type's allowed sources. Returns null when allowed,
    /// otherwise an Arabic error message. Scanned uploads need the Scanner flag; file uploads are
    /// classified by extension (PDF / image / any-file).</summary>
    private static string? CheckUploadSource(UploadSource allowed, string ext, bool isScanned)
    {
        if (allowed == UploadSource.None || allowed.HasFlag(UploadSource.AnyFile)) return null;

        if (isScanned)
            return allowed.HasFlag(UploadSource.Scanner) ? null : "هذا النوع من الوثائق لا يسمح بالمسح الضوئي";

        var e = ext.ToLowerInvariant();
        var needed = e == ".pdf" ? UploadSource.Pdf
            : e is ".jpg" or ".jpeg" or ".png" ? UploadSource.Image
            : UploadSource.AnyFile;

        if (allowed.HasFlag(needed)) return null;

        // Most restrictive case worth a clear message: scanner-only types.
        return allowed == UploadSource.Scanner
            ? "هذا النوع من الوثائق يقبل المسح الضوئي فقط — استخدم زر \"مسح ضوئي\""
            : "نوع الملف غير مسموح لهذا النوع من الوثائق";
    }

    public async Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<AttachmentDownload>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<AttachmentDownload>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId, ct);
        if (att is null) return Result<AttachmentDownload>.Fail("المرفق غير موجود");

        var stream = await storage.OpenAsync(att.StorageKey, ct);
        if (stream is null) return Result<AttachmentDownload>.Fail("ملف المرفق غير موجود في وحدة التخزين");

        await audit.WriteAsync("AttachmentDownloaded", EntityType, doc.Id, doc.Title, newValues: att.FileName, ct: ct);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(stream, att.FileName, att.ContentType));
    }

    public async Task<Result<bool>> RemoveAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId, ct);
        if (att is null) return Result<bool>.Fail("المرفق غير موجود");

        await storage.DeleteAsync(att.StorageKey, ct);
        db.DocumentAttachments.Remove(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("AttachmentRemoved", EntityType, doc.Id, doc.Title, oldValues: att.FileName, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- helpers ----

    /// <summary>Whether the caller may put a document in a physical place — a box, a shelf, a location.
    ///
    /// Filing is the archive department's job, not the originating department's: a document registered
    /// by another department goes to the archive (<see cref="SendToArchiveAsync"/>), and the archive
    /// assigns the shelf as the second stage of the intake workflow. Gated on the same code as the
    /// "تحديد مكان الحفظ" dialog, so the API and the button agree on who may file.</summary>
    private async Task<bool> CanFilePhysicallyAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (me is null) return false;
        return await db.UserRoles.AnyAsync(ur => ur.UserId == me && (
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator")
            || db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Archive.Archive"))), ct);
    }

    private const string NoFilingRightsError =
        "لا تملك صلاحية تحديد مكان الحفظ الفعلي — سجّل الوثيقة وأرسلها إلى قسم الأرشيف ليتولى حفظها";

    private bool WithinClearance(Document d) => (int)d.Confidentiality <= (int)currentUser.Clearance;

    /// <summary>The two gates a single document must clear: the caller's clearance, and the
    /// ownership scope that the list query applies (own / department / archive custody). An
    /// explicit share overrides both — sharing a document is a deliberate grant to one person.
    /// Without the ownership half, anything hidden from the list stayed reachable by id.</summary>
    private async Task<bool> CanAccessAsync(Document d, CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (await db.DocumentShares.AnyAsync(s => s.DocumentId == d.Id && s.SharedWithUserId == me, ct)) return true;
        if (!WithinClearance(d)) return false;
        if (await scope.SeesAllDocumentsAsync(ct)) return true;
        return d.CreatedBy == me || (await scope.OrgUnitIdsAsync(ct)).Contains(d.OwningOrgUnitId);
    }

    private static DateOnly? ComputeExpiry(DateOnly? documentDate, int retentionMonths) =>
        documentDate is { } date && retentionMonths > 0 ? date.AddMonths(retentionMonths) : null;

    private async Task<string> NextDocumentNumberAsync(CancellationToken ct)
    {
        var prefix = $"DOC-{DateTime.UtcNow.Year}-";
        /* Highest existing number + 1, not COUNT + 1: any gap in the sequence — a hard-deleted row,
           a number issued in a transaction that rolled back — makes COUNT reissue a number the
           unique index then rejects, and every registration from that point on fails with
           "Duplicate entry". Same fix as the mail registers. Zero-padding keeps the string order
           the same as the numeric order, so ordering by the number picks the true maximum. */
        var last = await db.Documents.IgnoreQueryFilters()
            .Where(d => d.DocumentNumber.StartsWith(prefix))
            .OrderByDescending(d => d.DocumentNumber)
            .Select(d => d.DocumentNumber)
            .FirstOrDefaultAsync(ct);

        var seq = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{seq:00000}";
    }

    private async Task<PhysicalArchiveItem?> GetPhysicalItemAsync(long docId, CancellationToken ct) =>
        await db.PhysicalArchiveItems.Include(i => i.PhysicalLocation)
            .Where(i => i.DocumentId == docId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);

    private static DocumentDetail WithPhysical(DocumentDetail d, PhysicalArchiveItem? item) =>
        item is null ? d : d with
        {
            PhysicalLocationId = item.PhysicalLocationId,
            PhysicalLocationName = item.PhysicalLocation.Name,
            BoxNumber = item.BoxNumber,
            FileNumber = item.FileNumber,
        };

    /// <summary>
    /// Documents whose attachment text contains the search words.
    ///
    /// The two engines are asked in their own language. MySQL has the FULLTEXT index the
    /// FullTextSearch migration created, so it gets MATCH … AGAINST. SQL Server is given a plain
    /// LIKE scan: its equivalent (CONTAINS) needs a full-text catalogue, which needs the Full-Text
    /// Search feature installed and a catalogue built per database — a deployment prerequisite this
    /// system does not impose. Sending MySQL's syntax to SQL Server is what it used to do, and it
    /// made every search on a SQL Server install fail with a syntax error.
    /// </summary>
    private async Task<List<long>> SearchAttachmentTextAsync(string search, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
            return await db.Database
                .SqlQuery<long>($"SELECT DISTINCT DocumentId AS Value FROM DocumentAttachments WHERE MATCH(ExtractedText) AGAINST({search} IN NATURAL LANGUAGE MODE)")
                .ToListAsync(ct);

        return await db.DocumentAttachments
            .Where(a => a.ExtractedText != null && EF.Functions.Like(a.ExtractedText, $"%{search}%"))
            .Select(a => a.DocumentId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Every box under a place, however deep it sits: a shelf's boxes, a cabinet's
    /// shelves' boxes, and so on up to a whole building.
    ///
    /// A box hangs off a shelf, or sits straight in a room when the site is run in 3-level mode,
    /// so both routes are followed at every level above the shelf — walking only the
    /// Building→Room→Cabinet→Shelf→Box chain would quietly lose the boxes kept in the room.</summary>
    private async Task<List<long>> BoxIdsUnderAsync(string kind, long id, CancellationToken ct)
    {
        var boxes = db.Boxes.AsQueryable();
        boxes = kind switch
        {
            "Box"      => boxes.Where(b => b.Id == id),
            "Shelf"    => boxes.Where(b => b.ShelfId == id),
            "Cabinet"  => boxes.Where(b => b.Shelf != null && b.Shelf.CabinetId == id),
            "Room"     => boxes.Where(b => b.RoomId == id
                                        || (b.Shelf != null && b.Shelf.Cabinet.RoomId == id)),
            "Building" => boxes.Where(b => (b.Room != null && b.Room.BuildingId == id)
                                        || (b.Shelf != null && b.Shelf.Cabinet.Room.BuildingId == id)),
            _          => boxes.Where(_ => false),
        };
        return await boxes.Select(b => b.Id).ToListAsync(ct);
    }

    // Keep Box.CurrentCount in sync as documents are filed / moved / unfiled.
    private async Task AdjustBoxCountAsync(long? oldBoxId, long? newBoxId, CancellationToken ct)
    {
        if (oldBoxId == newBoxId) return;
        if (oldBoxId is { } ob)
        {
            var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == ob, ct);
            if (b is { CurrentCount: > 0 }) { b.CurrentCount--; await db.SaveChangesAsync(ct); }
        }
        if (newBoxId is { } nb)
        {
            var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == nb, ct);
            if (b is not null) { b.CurrentCount++; await db.SaveChangesAsync(ct); }
        }
    }

    private static DocumentDetail ToDetail(Document d) => new(
        d.Id, d.DocumentNumber, d.Title, d.Description, d.DocumentTypeId, d.DocumentType.Name,
        d.CategoryId, d.OwningOrgUnitId, d.OwnerPositionId, d.Confidentiality.ToString(), d.Status.ToString(),
        d.Keywords, d.RetentionMonths, d.DocumentDate, d.ExpiryDate, d.Version, d.ParentDocumentId, d.IsLatestVersion,
        d.CreatedAt,
        d.Attachments.OrderBy(a => a.Id).Select(a => new DocumentAttachmentDto(
            a.Id, a.FileName, a.ContentType, a.FileExtension, a.SizeBytes, a.IsScanned, a.CreatedAt,
            a.Kind.ToString(), a.SourceAttachmentId, a.PdfAConformance, a.PreservationValidated)).ToList())
    {
        IsTombstone = d.IsTombstone,
        DestroyedAtUtc = d.DestroyedAtUtc,
        BoxId = d.BoxId,
        ArchiveReturnReason = d.ArchiveReturnReason,
        SentToArchiveAtUtc = d.SentToArchiveAtUtc,
    };
}
