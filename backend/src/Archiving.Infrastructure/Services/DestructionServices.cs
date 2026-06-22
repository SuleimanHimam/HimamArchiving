using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Destruction;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Retention-driven eligibility: a record may be destroyed only when its retention has
/// expired AND it is under no active legal hold AND no workflow is open on it.</summary>
public sealed class DestructionEligibilityService(AppDbContext db) : IDestructionEligibilityService
{
    public async Task<DestructionEligibilityDto> CheckAsync(long documentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return new DestructionEligibilityDto(documentId, false, new[] { "الوثيقة غير موجودة" });

        var reasons = new List<string>();
        if (doc.IsTombstone) reasons.Add("الوثيقة مُتلَفة مسبقًا");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (doc.ExpiryDate is null || doc.ExpiryDate > today) reasons.Add("لم تنتهِ مدة الحفظ بعد");

        if (await HasActiveHoldAsync(doc, ct)) reasons.Add("الوثيقة تحت حجز قانوني");

        if (await db.WorkflowInstances.AnyAsync(w =>
                w.EntityType == "Document" && w.EntityId == documentId && w.Status == WorkflowStatus.Running, ct))
            reasons.Add("توجد دورة عمل مفتوحة على الوثيقة");

        return new DestructionEligibilityDto(documentId, reasons.Count == 0, reasons);
    }

    private Task<bool> HasActiveHoldAsync(Document doc, CancellationToken ct) =>
        db.LegalHolds.AnyAsync(h => h.ReleasedAtUtc == null && (
            (h.Scope == LegalHoldScope.Document && h.DocumentId == doc.Id)
            || (h.Scope == LegalHoldScope.Folder && h.FolderId != null && h.FolderId == doc.FolderId)
            || (h.Scope == LegalHoldScope.OrgUnit && h.OrgUnitId == doc.OwningOrgUnitId)), ct);
}

public sealed class LegalHoldService(AppDbContext db, ICurrentUser currentUser, IAuditWriter audit) : ILegalHoldService
{
    private long Uid => currentUser.UserId ?? 0;

    public async Task<IReadOnlyList<LegalHoldDto>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.LegalHolds.AsQueryable();
        if (activeOnly) q = q.Where(h => h.ReleasedAtUtc == null);
        return await q.OrderByDescending(h => h.PlacedAtUtc)
            .Select(h => new LegalHoldDto(h.Id, h.Reason, (int)h.Scope, h.DocumentId, h.FolderId, h.OrgUnitId,
                h.PlacedByUserId, db.Users.Where(u => u.Id == h.PlacedByUserId).Select(u => u.FullName).FirstOrDefault(),
                h.PlacedAtUtc, h.ReleasedByUserId, h.ReleasedAtUtc, h.ReleasedAtUtc == null))
            .ToListAsync(ct);
    }

    public async Task<Result<LegalHoldDto>> PlaceAsync(PlaceLegalHoldRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) return Result<LegalHoldDto>.Fail("سبب الحجز مطلوب");
        var scope = (LegalHoldScope)r.Scope;

        switch (scope)
        {
            case LegalHoldScope.Document when r.DocumentId is null || !await db.Documents.AnyAsync(d => d.Id == r.DocumentId, ct):
                return Result<LegalHoldDto>.Fail("الوثيقة غير موجودة");
            case LegalHoldScope.Folder when r.FolderId is null:
                return Result<LegalHoldDto>.Fail("المجلد مطلوب");
            case LegalHoldScope.OrgUnit when r.OrgUnitId is null || !await db.OrgUnits.AnyAsync(o => o.Id == r.OrgUnitId, ct):
                return Result<LegalHoldDto>.Fail("الوحدة التنظيمية غير موجودة");
        }

        var e = new LegalHold
        {
            Reason = r.Reason.Trim(), Scope = scope,
            DocumentId = scope == LegalHoldScope.Document ? r.DocumentId : null,
            FolderId = scope == LegalHoldScope.Folder ? r.FolderId : null,
            OrgUnitId = scope == LegalHoldScope.OrgUnit ? r.OrgUnitId : null,
            QueryExpression = scope == LegalHoldScope.Query ? r.QueryExpression : null,
            PlacedByUserId = Uid,
        };
        db.LegalHolds.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Placed", "LegalHold", e.Id, e.Reason, ct: ct);
        var name = await db.Users.Where(u => u.Id == Uid).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return Result<LegalHoldDto>.Ok(new LegalHoldDto(e.Id, e.Reason, (int)e.Scope, e.DocumentId, e.FolderId, e.OrgUnitId,
            e.PlacedByUserId, name, e.PlacedAtUtc, null, null, true));
    }

    public async Task<Result<bool>> ReleaseAsync(long id, CancellationToken ct = default)
    {
        var e = await db.LegalHolds.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الحجز غير موجود");
        if (e.ReleasedAtUtc is not null) return Result<bool>.Ok(true);
        e.ReleasedByUserId = Uid;
        e.ReleasedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Released", "LegalHold", e.Id, e.Reason, ct: ct);
        return Result<bool>.Ok(true);
    }
}

public sealed class DestructionService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IDestructionEligibilityService eligibility,
    IFileStorage storage,
    ICertificateService certificates,
    IPasswordHasher hasher) : IDestructionService
{
    private long Uid => currentUser.UserId ?? 0;

    public async Task<PagedResult<DestructionRequestDto>> ListAsync(DestructionRequestQuery query, CancellationToken ct = default)
    {
        var q = db.DestructionRequests.AsQueryable();
        if (query.Status is { } st) q = q.Where(r => (int)r.Status == st);
        var total = await q.CountAsync(ct);
        var size = query.PageSize <= 0 ? 20 : query.PageSize;
        var page = query.Page <= 0 ? 1 : query.Page;
        var ids = await q.OrderByDescending(r => r.RequestedAtUtc)
            .Skip((page - 1) * size).Take(size).Select(r => r.Id).ToListAsync(ct);
        var items = new List<DestructionRequestDto>();
        foreach (var id in ids) items.Add((await BuildDtoAsync(id, ct))!);
        return new PagedResult<DestructionRequestDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<DestructionRequestDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var dto = await BuildDtoAsync(id, ct);
        return dto is null ? Result<DestructionRequestDto>.Fail("الطلب غير موجود") : Result<DestructionRequestDto>.Ok(dto);
    }

    public async Task<Result<DestructionRequestDto>> CreateAsync(CreateDestructionRequest r, CancellationToken ct = default)
    {
        if (r.DocumentIds.Count == 0) return Result<DestructionRequestDto>.Fail("اختر وثيقة واحدة على الأقل");
        if (string.IsNullOrWhiteSpace(r.Reason)) return Result<DestructionRequestDto>.Fail("سبب الإتلاف مطلوب");
        if (r.Method == (int)DestructionMethod.Other && string.IsNullOrWhiteSpace(r.CustomMethod))
            return Result<DestructionRequestDto>.Fail("حدّد طريقة الإتلاف عند اختيار «أخرى»");

        var distinct = r.DocumentIds.Distinct().ToList();
        foreach (var docId in distinct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == docId, ct);
            if (doc is null) return Result<DestructionRequestDto>.Fail($"الوثيقة #{docId} غير موجودة");
            if (doc.IsTombstone) return Result<DestructionRequestDto>.Fail($"الوثيقة #{docId} مُتلَفة مسبقًا");
            // Hard block: a held record can never enter a destruction request.
            var elig = await eligibility.CheckAsync(docId, ct);
            if (elig.Reasons.Contains("الوثيقة تحت حجز قانوني"))
                return Result<DestructionRequestDto>.Fail($"الوثيقة #{docId} تحت حجز قانوني ولا يمكن إدراجها");
        }

        var req = new DestructionRequest
        {
            Status = DestructionStatus.Draft,
            Reason = r.Reason.Trim(),
            RetentionBasisId = r.RetentionBasisId,
            RequestedByUserId = Uid,
            ScheduledForUtc = r.ScheduledForUtc,
            Items = distinct.Select(docId => new DestructionItem
            {
                DocumentId = docId,
                Method = (DestructionMethod)r.Method,
                CustomMethod = r.CustomMethod?.Trim(),   // chosen method label (catalog or free text)
            }).ToList(),
        };
        db.DestructionRequests.Add(req);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DestructionRequest", req.Id, r.Reason, ct: ct);
        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    public async Task<Result<DestructionRequestDto>> SubmitAsync(long id, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return Result<DestructionRequestDto>.Fail("الطلب غير موجود");
        if (req.Status != DestructionStatus.Draft) return Result<DestructionRequestDto>.Fail("لا يمكن تقديم الطلب في حالته الحالية");
        req.Status = DestructionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        await NotifyApproversAsync(req, ct);
        await audit.WriteAsync("Submitted", "DestructionRequest", req.Id, ct: ct);
        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    public async Task<Result<DestructionRequestDto>> ApproveAsync(long id, DestructionDecisionRequest r, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DestructionRequestDto>.Fail("الطلب غير موجود");
        if (req.Status is not (DestructionStatus.PendingApproval or DestructionStatus.PendingReview))
            return Result<DestructionRequestDto>.Fail("لا يمكن اعتماد الطلب في حالته الحالية");
        // Two-person rule: the requester cannot approve their own request.
        if (req.RequestedByUserId == Uid)
            return Result<DestructionRequestDto>.Fail("لا يمكن للطالب اعتماد طلبه (قاعدة المستخدمَين)");

        // Re-check eligibility at approval time; block if any item became ineligible.
        foreach (var it in req.Items)
        {
            var elig = await eligibility.CheckAsync(it.DocumentId, ct);
            if (!elig.Eligible)
                return Result<DestructionRequestDto>.Fail($"الوثيقة #{it.DocumentId} لم تعد مؤهلة: {string.Join("، ", elig.Reasons)}");
        }

        req.Status = DestructionStatus.Approved;
        req.ApprovedByUserId = Uid;
        req.ApprovedAtUtc = DateTime.UtcNow;
        req.DecisionNote = r.Note;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Approved", "DestructionRequest", req.Id, r.Note, ct: ct);
        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    public async Task<Result<DestructionRequestDto>> RejectAsync(long id, DestructionDecisionRequest r, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DestructionRequestDto>.Fail("الطلب غير موجود");
        if (req.Status is not (DestructionStatus.PendingApproval or DestructionStatus.PendingReview))
            return Result<DestructionRequestDto>.Fail("لا يمكن رفض الطلب في حالته الحالية");
        req.Status = DestructionStatus.Rejected;
        req.ApprovedByUserId = Uid;
        req.ApprovedAtUtc = DateTime.UtcNow;
        req.DecisionNote = r.Note;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rejected", "DestructionRequest", req.Id, r.Note, ct: ct);
        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    public async Task<Result<DestructionRequestDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DestructionRequestDto>.Fail("الطلب غير موجود");
        if (req.Status is DestructionStatus.Completed or DestructionStatus.Executing)
            return Result<DestructionRequestDto>.Fail("لا يمكن إلغاء طلب قيد التنفيذ أو مكتمل");
        req.Status = DestructionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "DestructionRequest", req.Id, ct: ct);
        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    public async Task<Result<DestructionRequestDto>> ExecuteAsync(long id, ExecuteDestructionRequest r, bool canOverride = false, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DestructionRequestDto>.Fail("الطلب غير موجود");

        // A manager/admin (canOverride) may destroy directly: auto-approve a not-yet-approved request and
        // bypass the two-person rule. Everyone else needs a separately-approved request + segregation of duties.
        if (canOverride)
        {
            if (req.Status is DestructionStatus.Draft or DestructionStatus.PendingReview or DestructionStatus.PendingApproval)
            {
                req.Status = DestructionStatus.Approved;
                req.ApprovedByUserId = Uid;
                req.ApprovedAtUtc = DateTime.UtcNow;
            }
            if (req.Status != DestructionStatus.Approved)
                return Result<DestructionRequestDto>.Fail("لا يمكن تنفيذ الطلب في حالته الحالية");
        }
        else
        {
            if (req.Status != DestructionStatus.Approved)
                return Result<DestructionRequestDto>.Fail("يجب اعتماد الطلب قبل التنفيذ");
            // Segregation of duties: the executor must differ from the requester and the approver.
            if (req.RequestedByUserId == Uid) return Result<DestructionRequestDto>.Fail("لا يمكن للطالب تنفيذ الإتلاف (فصل المهام)");
            if (req.ApprovedByUserId == Uid) return Result<DestructionRequestDto>.Fail("لا يمكن للمعتمِد تنفيذ الإتلاف (فصل المهام)");
        }

        // MFA step-up: re-authenticate the executor before the irreversible action.
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == Uid, ct);
        if (user is null) return Result<DestructionRequestDto>.Fail("المستخدم غير موجود");
        if (string.IsNullOrEmpty(r.StepUpPassword) || !hasher.Verify(r.StepUpPassword, user.PasswordHash))
            return Result<DestructionRequestDto>.Fail("فشل التحقق الأمني — يلزم إعادة المصادقة لتنفيذ الإتلاف");

        // Final eligibility re-check (e.g. a legal hold may have been placed since approval).
        foreach (var it in req.Items)
        {
            var elig = await eligibility.CheckAsync(it.DocumentId, ct);
            if (!elig.Eligible)
                return Result<DestructionRequestDto>.Fail($"الوثيقة #{it.DocumentId} لم تعد مؤهلة: {string.Join("، ", elig.Reasons)}");
        }

        req.Status = DestructionStatus.Executing;
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        try
        {
            foreach (var it in req.Items)
            {
                var doc = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == it.DocumentId, ct);
                // Destroy every representation: original (SIP), preservation master (AIP), DIP/cache.
                var atts = await db.DocumentAttachments
                    .Where(a => a.DocumentId == doc.Id && !a.ContentDestroyed).ToListAsync(ct);
                it.ChecksumBefore = atts.FirstOrDefault()?.Checksum;

                var destroyedCount = 0;
                foreach (var att in atts)
                {
                    var applied = await storage.CryptoShredAsync(att.StorageKey, ct);
                    att.ContentDestroyed = true;
                    att.ContentDestroyedAt = now;
                    // Fixity void: the original SHA-256 is retained in the certificate; the bytes are gone.
                    db.FixityChecks.Add(new FixityCheck
                    {
                        DocumentAttachmentId = att.Id, Algorithm = att.ChecksumAlgorithm,
                        ExpectedHash = att.Checksum, ActualHash = null, Result = FixityResult.Missing,
                        CheckedByUserId = Uid, Note = $"المحتوى مُتلَف ({applied})",
                    });
                    destroyedCount++;
                }

                var methodLabel = !string.IsNullOrEmpty(it.CustomMethod) ? it.CustomMethod : it.Method.ToString();
                var physical = it.Method is DestructionMethod.Shredding or DestructionMethod.Incineration
                    or DestructionMethod.Pulping or DestructionMethod.Degaussing
                    ? $" · مادي: {it.Method} · المسؤول: {r.PhysicalOfficer ?? "—"} · الشاهد: {r.PhysicalWitness ?? "—"}" : "";
                it.Outcome = $"{methodLabel}: {destroyedCount} ملف{physical}";

                // Tombstone: keep the metadata row, drop the content.
                doc.IsTombstone = true;
                doc.DestroyedAtUtc = now;
                doc.Status = DocumentStatus.Disposed;
            }

            req.ExecutedByUserId = Uid;
            req.ExecutedAtUtc = now;
            req.Status = DestructionStatus.Completed;
            await db.SaveChangesAsync(ct);

            await certificates.IssueAsync(req.Id, ct);   // sets CertificateId
            await audit.WriteAsync("Executed", "DestructionRequest", req.Id,
                $"items={req.Items.Count}", ct: ct);
        }
        catch
        {
            req.Status = DestructionStatus.Failed;
            await db.SaveChangesAsync(ct);
            throw;
        }

        return Result<DestructionRequestDto>.Ok((await BuildDtoAsync(req.Id, ct))!);
    }

    // Notify destruction approvers (managers / admins) that a request awaits approval — like the
    // incoming/outgoing approval flow. The requester is not notified about their own request.
    private async Task NotifyApproversAsync(DestructionRequest req, CancellationToken ct)
    {
        var approverIds = await db.Users
            .Where(u => u.IsActive && u.Id != req.RequestedByUserId && db.UserRoles.Any(ur => ur.UserId == u.Id && (
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator") ||
                db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId &&
                    db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Destruction.Approve")))))
            .Select(u => u.Id).ToListAsync(ct);

        foreach (var aid in approverIds)
            db.Notifications.Add(new Notification
            {
                RecipientUserId = aid, Title = "طلب إتلاف بانتظار الاعتماد", Body = req.Reason,
                Type = NotificationType.Task, Channels = NotificationChannel.InApp,
                EntityType = "Destruction", EntityId = req.Id,
            });
        if (approverIds.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task<DestructionRequestDto?> BuildDtoAsync(long id, CancellationToken ct)
    {
        var req = await db.DestructionRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return null;
        var reqName = await db.Users.Where(u => u.Id == req.RequestedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        string? appName = req.ApprovedByUserId is { } aid
            ? await db.Users.Where(u => u.Id == aid).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;

        var docIds = req.Items.Select(i => i.DocumentId).ToList();
        var docs = await db.Documents.Where(d => docIds.Contains(d.Id))
            .Select(d => new { d.Id, d.DocumentNumber, d.Title }).ToListAsync(ct);
        var items = req.Items.Select(i =>
        {
            var d = docs.FirstOrDefault(x => x.Id == i.DocumentId);
            return new DestructionItemDto(i.Id, i.DocumentId, d?.DocumentNumber ?? $"#{i.DocumentId}",
                d?.Title ?? "—", (int)i.Method, i.CustomMethod, i.ChecksumBefore, i.Outcome);
        }).ToList();

        return new DestructionRequestDto(req.Id, req.Status.ToString(), req.Reason, req.RetentionBasisId,
            req.RequestedByUserId, reqName, req.RequestedAtUtc,
            req.ApprovedByUserId, appName, req.ApprovedAtUtc,
            req.ExecutedByUserId, req.ExecutedAtUtc,
            req.DecisionNote, req.ScheduledForUtc, req.CertificateId, items);
    }
}
