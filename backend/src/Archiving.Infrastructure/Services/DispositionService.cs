using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Disposition;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// The disposition (إتلاف / تجديد حفظ) chain, routed to seats rather than to permission holders:
///
///   employee of any department  →  رئيس قسمه  →  مدير قسم الأرشيف
///   موظف الأرشيف                →                مدير قسم الأرشيف
///
/// Destruction is ratified for the whole institution by one seat, مدير قسم الأرشيف, whichever
/// department asked. A permission alone is never enough to act: the caller must occupy the seat
/// the request is waiting on, so "a manager" cannot clear another department's step.
///
/// Segregation of duties still holds on top of that — the person who raised a request may perform
/// neither step, and the first approver may not also ratify — so no document is destroyed on one
/// person's say-so. Documents under an active legal hold are excluded.
/// </summary>
public sealed class DispositionService(
    AppDbContext db, ICurrentUser currentUser, IAuditWriter audit, IAccessScope scope)
    : IDispositionService
{
    private long Uid => currentUser.UserId ?? 0;

    /// <summary>Who sees which requests — the same shape as the chain that decides them.
    ///
    /// مدير قسم الأرشيف ratifies every destruction in the institution, so they see every request,
    /// as do the archive custodians who carry them out. A department head sees what their own
    /// department raised, because that is what they are asked to approve. Everyone else sees the
    /// requests they raised themselves and nothing more.
    ///
    /// Disposition.View is held by every employee — asking for a record to be destroyed is an
    /// ordinary part of holding it — so without this the screen showed each of them the
    /// institution's entire disposal history, including which colleague asked for what.</summary>
    private async Task<IQueryable<DispositionRequest>> VisibleAsync(IQueryable<DispositionRequest> q, CancellationToken ct)
        // Shared with the dashboard tile that counts these — see ScopeFilters.
        => await q.VisibleDispositionsAsync(db, scope, Uid, ct);

    /// <summary>The name the archive department is seeded under (<see cref="DbSeeder"/>). Renaming
    /// the unit in the org chart breaks nothing else, so the lookup falls back to the department of
    /// whoever holds the archive custody right.</summary>
    private const string ArchiveUnitName = "قسم الأرشيف";

    /// <summary>The archive department, or null if the institution has none yet.</summary>
    private async Task<long?> ArchiveUnitIdAsync(CancellationToken ct)
    {
        var byName = await db.OrgUnits.Where(o => o.Name == ArchiveUnitName)
            .Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);
        if (byName is not null) return byName;
        // Fallback: the department of an active holder of Archive.Archive — the custody right.
        return await db.Users
            .Where(u => u.IsActive && u.OrgUnitId != null && db.UserRoles.Any(ur => ur.UserId == u.Id
                && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                    && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Archive.Archive"))))
            .Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
    }

    /// <summary>Whoever occupies a department's manager seat (OrgUnit.ManagerPositionId).</summary>
    private async Task<long?> ManagerOfUnitAsync(long? orgUnitId, CancellationToken ct)
    {
        if (orgUnitId is not { } uid) return null;
        var seat = await db.OrgUnits.Where(o => o.Id == uid).Select(o => o.ManagerPositionId).FirstOrDefaultAsync(ct);
        if (seat is not { } pid) return null;
        return await db.Positions.Where(p => p.Id == pid).Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
    }

    /// <summary>مدير قسم الأرشيف — the seat that ratifies every destruction, whichever department
    /// asked for it.</summary>
    private async Task<long?> ArchiveManagerAsync(CancellationToken ct) =>
        await ManagerOfUnitAsync(await ArchiveUnitIdAsync(ct), ct);

    /// <summary>The manager who owes the first approval: the head of the requester's own department.
    /// Null when the requester is archive staff (their step is skipped) or their department has no
    /// manager seat filled.</summary>
    private async Task<long?> DepartmentApproverAsync(long requesterId, CancellationToken ct)
    {
        var dept = await db.Users.Where(u => u.Id == requesterId).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        if (dept is null) return null;
        if (dept == await ArchiveUnitIdAsync(ct)) return null;   // archive staff go straight to their manager
        if (await ManagerOfUnitAsync(dept, ct) is { } seated) return seated;

        // Most org charts fill departments long before they fill the manager *seat*. Rather than
        // skip the department step whenever ManagerPositionId is unset, fall back to an active
        // member of that department who holds Disposition.Approve — a department head by role.
        return await db.Users
            .Where(u => u.IsActive && u.OrgUnitId == dept && u.Id != requesterId
                && db.UserRoles.Any(ur => ur.UserId == u.Id
                    && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                        && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Disposition.Approve"))))
            .Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
    }

    // ── queries ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<DispositionRequestDto>> ListAsync(string? stage, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var q = await VisibleAsync(db.DispositionRequests, ct);
        q = stage?.ToLowerInvariant() switch
        {
            "verification"  => q.Where(r => r.Status == DispositionStatus.PendingVerification),
            "finalapproval" => q.Where(r => r.Status == DispositionStatus.PendingFinalApproval),
            _               => q,
        };

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.RequestedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new Row(r,
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.DocumentNumber).FirstOrDefault(),
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.Title).FirstOrDefault(),
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.ExpiryDate).FirstOrDefault(),
                db.Boxes.Where(bx => bx.Id == db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.BoxId).FirstOrDefault()).Select(bx => bx.BoxCode).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.RequestedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.VerifiedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.FinalApprovedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.RejectedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.DispositionCertificates.Where(c => c.Id == r.CertificateId).Select(c => c.CertificateNumber).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<DispositionRequestDto>
        {
            Items = rows.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<Result<DispositionRequestDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var dto = await BuildDtoAsync(id, ct);
        return dto is null ? Result<DispositionRequestDto>.Fail("الطلب غير موجود") : Result<DispositionRequestDto>.Ok(dto);
    }

    // ── commands ─────────────────────────────────────────────────────────────

    public async Task<Result<DispositionRequestDto>> CreateAsync(CreateDispositionRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) return Fail("السبب مطلوب");
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == r.DocumentId, ct);
        if (doc is null) return Fail("الوثيقة غير موجودة");
        if (doc.IsTombstone) return Fail("الوثيقة مُتلَفة مسبقًا");
        if (await HasActiveHoldAsync(doc, ct)) return Fail("الوثيقة تحت حجز قانوني ولا يمكن التصرّف بها");
        if (await db.DispositionRequests.AnyAsync(x => x.DocumentId == r.DocumentId &&
                (x.Status == DispositionStatus.PendingVerification || x.Status == DispositionStatus.PendingFinalApproval), ct))
            return Fail("يوجد طلب تصرّف قائم على هذه الوثيقة");

        var req = new DispositionRequest
        {
            DocumentId = r.DocumentId,
            RequestedAction = (DispositionAction)r.RequestedAction,
            Reason = r.Reason.Trim(),
            RequestedByUserId = Uid,
            Method = (DestructionMethod)r.Method,
            CustomMethod = string.IsNullOrWhiteSpace(r.CustomMethod) ? null : r.CustomMethod!.Trim(),
        };

        /* The route depends on who is asking. An employee of any department is approved first by
           their own department head and only then by مدير قسم الأرشيف, who ratifies every
           destruction. Archive staff have no separate department step — their head *is* the archive
           manager — so their request starts at the final one. A department with no manager seat
           filled would otherwise leave the request stranded, so it takes the same shortcut and the
           archive manager decides alone. */
        var deptApprover = await DepartmentApproverAsync(Uid, ct);
        req.Status = deptApprover is null
            ? DispositionStatus.PendingFinalApproval
            : DispositionStatus.PendingVerification;

        db.DispositionRequests.Add(req);
        await db.SaveChangesAsync(ct);

        await SetRetentionStatusAsync(r.DocumentId, DocumentRetentionStatus.PendingReview, ct);
        await audit.WriteAsync("DispositionRequested", "DispositionRequest", req.Id, doc.DocumentNumber, ct: ct);
        if (deptApprover is { } mgr)
            await NotifyUserAsync(mgr, "طلب إتلاف بانتظار موافقتك كمدير للقسم", doc.DocumentNumber, req.Id, ct);
        else
            await NotifyUserAsync(await ArchiveManagerAsync(ct), "طلب إتلاف بانتظار مصادقة مدير الأرشيف", doc.DocumentNumber, req.Id, ct);
        return await OkAsync(req.Id, ct);
    }

    public async Task<Result<DispositionRequestDto>> VerifyAsync(long id, VerifyDispositionRequest r, CancellationToken ct = default)
    {
        var req = await db.DispositionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Fail("الطلب غير موجود");
        if (req.Status != DispositionStatus.PendingVerification) return Fail("الطلب ليس بانتظار التحقق");
        if (string.IsNullOrWhiteSpace(r.Notes)) return Fail("ملاحظات التحقق مطلوبة");
        // Nobody clears their own request: destruction is only ever approved by someone else, whatever
        // permissions the requester happens to hold. An employee cannot reach this step at all (they
        // have Disposition.View + Create and nothing more) — this closes it for the archive staff and
        // administrators who can.
        if (req.RequestedByUserId == Uid) return Fail("لا يجوز التحقق من طلب تصرّف قدّمته بنفسك");
        // This step belongs to one seat: the head of the department the request came from.
        if (!await IsCurrentApproverAsync(req, ct))
            return Fail("هذه الخطوة من صلاحية مدير القسم الذي قدّم الطلب");

        var now = DateTime.UtcNow;
        if (string.Equals(r.Decision, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            req.Status = DispositionStatus.Rejected;
            req.RejectedByUserId = Uid; req.RejectedAtUtc = now; req.RejectionReason = r.Notes.Trim();
            await db.SaveChangesAsync(ct);
            await SetRetentionStatusAsync(req.DocumentId, DocumentRetentionStatus.Active, ct);
            await audit.WriteAsync("DispositionRejected", "DispositionRequest", req.Id, "رُفض عند التحقق", ct: ct);
            await NotifyRequesterAsync(req, "رُفض طلب التصرّف عند التحقق", req.RejectionReason, ct);
            return await OkAsync(req.Id, ct);
        }

        req.VerifiedByUserId = Uid; req.VerifiedAtUtc = now; req.VerificationNotes = r.Notes.Trim();

        // Low-risk renewals may skip the second (legal) step when the category's policy allows it.
        // Destruction ALWAYS requires both steps.
        if (req.RequestedAction == DispositionAction.Renew)
        {
            var policy = await PolicyForAsync(req.DocumentId, ct);
            if (policy is { RequiresLegalApprovalForRenewal: false })
            {
                var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == req.DocumentId, ct);
                if (doc is not null)
                {
                    var months = policy.RetentionMonths > 0 ? policy.RetentionMonths : 12;
                    var newExpiry = DateOnly.FromDateTime(now).AddMonths(months);
                    await CompleteRenewAsync(req, doc, newExpiry, ct);
                    await db.SaveChangesAsync(ct);
                    await audit.WriteAsync("Renewed", "Document", doc.Id, doc.DocumentNumber,
                        newValues: $"newExpiry={newExpiry:yyyy-MM-dd} (legal step skipped per policy)", ct: ct);
                    return await OkAsync(req.Id, ct);
                }
            }
        }

        req.Status = DispositionStatus.PendingFinalApproval;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DispositionVerified", "DispositionRequest", req.Id, ct: ct);
        var num = await db.Documents.Where(d => d.Id == req.DocumentId).Select(d => d.DocumentNumber).FirstOrDefaultAsync(ct);
        await NotifyUserAsync(await ArchiveManagerAsync(ct), "طلب إتلاف بانتظار مصادقة مدير الأرشيف", num, req.Id, ct);
        return await OkAsync(req.Id, ct);
    }

    public async Task<Result<DispositionRequestDto>> FinalApproveAsync(long id, FinalApproveDispositionRequest r, CancellationToken ct = default)
    {
        var req = await db.DispositionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Fail("الطلب غير موجود");
        if (req.Status != DispositionStatus.PendingFinalApproval) return Fail("الطلب ليس بانتظار الموافقة النهائية");
        if (string.IsNullOrWhiteSpace(r.Notes)) return Fail("ملاحظات الموافقة مطلوبة");
        // Segregation of duties: neither the verifier nor the requester may grant final approval.
        if (req.VerifiedByUserId == Uid) return Fail("لا يجوز أن يقوم المستخدم نفسه بالتحقق والموافقة النهائية على الطلب");
        if (req.RequestedByUserId == Uid) return Fail("لا يجوز الموافقة على طلب تصرّف قدّمته بنفسك");
        // Destruction is ratified by one seat for the whole institution: مدير قسم الأرشيف.
        if (!await IsCurrentApproverAsync(req, ct))
            return Fail("المصادقة النهائية على الإتلاف من صلاحية مدير قسم الأرشيف");

        var now = DateTime.UtcNow;
        if (string.Equals(r.Decision, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            req.Status = DispositionStatus.Rejected;
            req.RejectedByUserId = Uid; req.RejectedAtUtc = now; req.RejectionReason = r.Notes.Trim();
            await db.SaveChangesAsync(ct);
            await SetRetentionStatusAsync(req.DocumentId, DocumentRetentionStatus.Active, ct);
            await audit.WriteAsync("DispositionRejected", "DispositionRequest", req.Id, "رُفض عند الموافقة النهائية", ct: ct);
            await NotifyRequesterAsync(req, "رُفض طلب التصرّف في الموافقة النهائية", req.RejectionReason, ct);
            return await OkAsync(req.Id, ct);
        }

        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == req.DocumentId, ct);
        if (doc is null) return Fail("الوثيقة غير موجودة");

        req.FinalApprovedByUserId = Uid; req.FinalApprovedAtUtc = now; req.FinalApprovalNotes = r.Notes.Trim();
        req.Status = DispositionStatus.Approved;
        // Capture the box before ReleaseBoxAsync clears it; dispatched on the next SaveChanges.
        req.AddDomainEvent(new DispositionApprovedEvent(req.Id, doc.Id, doc.BoxId)
        {
            TriggeredByUserId = currentUser.UserId?.ToString(),
        });

        if (req.RequestedAction == DispositionAction.Destroy)
        {
            if (await HasActiveHoldAsync(doc, ct)) return Fail("الوثيقة تحت حجز قانوني ولا يمكن إتلافها");

            // Soft destruction: tombstone + hide; physical slot released.
            doc.IsTombstone = true;
            doc.DestroyedAtUtc = now;
            doc.Status = DocumentStatus.Disposed;
            await ReleaseBoxAsync(doc, ct);

            var cert = new DispositionCertificate
            {
                DispositionRequestId = req.Id,
                CertificateNumber = $"COD-{now:yyyy}-{req.Id:D5}",
                DocumentIds = doc.Id.ToString(),
                DestructionMethod = req.CustomMethod ?? MethodLabel(req.Method),
                VerifiedByUserId = req.VerifiedByUserId,
                FinalApprovedByUserId = Uid,
            };
            db.DispositionCertificates.Add(cert);
            await db.SaveChangesAsync(ct);

            req.CertificateId = cert.Id;
            req.Status = DispositionStatus.Completed;
            await db.SaveChangesAsync(ct);
            await SetRetentionStatusAsync(req.DocumentId, DocumentRetentionStatus.Destroyed, ct);
            await audit.WriteAsync("Destroyed", "Document", doc.Id, doc.DocumentNumber,
                newValues: $"cert={cert.CertificateNumber}", ct: ct);
        }
        else // Renew
        {
            var newExpiry = r.NewExpiryDate ?? DateOnly.FromDateTime(now).AddYears(1);
            await CompleteRenewAsync(req, doc, newExpiry, ct);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Renewed", "Document", doc.Id, doc.DocumentNumber,
                newValues: $"newExpiry={newExpiry:yyyy-MM-dd}", ct: ct);
        }

        await NotifyRequesterAsync(req,
            req.RequestedAction == DispositionAction.Destroy
                ? "اعتُمد طلب الإتلاف ونُفِّذ"
                : "اعتُمد طلب تجديد الحفظ",
            doc.DocumentNumber, ct);
        return await OkAsync(req.Id, ct);
    }

    public async Task<Result<DispositionRequestDto>> RejectAsync(long id, RejectDispositionRequest r, CancellationToken ct = default)
    {
        var req = await db.DispositionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Fail("الطلب غير موجود");
        if (req.Status is not (DispositionStatus.PendingVerification or DispositionStatus.PendingFinalApproval))
            return Fail("لا يمكن رفض الطلب في حالته الحالية");
        if (string.IsNullOrWhiteSpace(r.Reason)) return Fail("سبب الرفض مطلوب");
        // Refusing is part of deciding, so it belongs to whoever the request is waiting on.
        if (!await IsCurrentApproverAsync(req, ct))
            return Fail("الطلب بانتظار جهة أخرى — لا يمكنك البتّ فيه");

        req.Status = DispositionStatus.Rejected;
        req.RejectedByUserId = Uid; req.RejectedAtUtc = DateTime.UtcNow; req.RejectionReason = r.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await SetRetentionStatusAsync(req.DocumentId, DocumentRetentionStatus.Active, ct);
        await audit.WriteAsync("DispositionRejected", "DispositionRequest", req.Id, ct: ct);
        await NotifyRequesterAsync(req, "رُفض طلب التصرّف", req.RejectionReason, ct);
        return await OkAsync(req.Id, ct);
    }

    public async Task<Result<DispositionCertificateDto>> GetCertificateAsync(long requestId, CancellationToken ct = default)
    {
        // The certificate is evidence about a request, so it follows the request's visibility.
        if (!await (await VisibleAsync(db.DispositionRequests, ct)).AnyAsync(r => r.Id == requestId, ct))
            return Result<DispositionCertificateDto>.Fail("لا توجد شهادة لهذا الطلب");
        var cert = await db.DispositionCertificates.FirstOrDefaultAsync(c => c.DispositionRequestId == requestId, ct);
        if (cert is null) return Result<DispositionCertificateDto>.Fail("لا توجد شهادة لهذا الطلب");

        var ids = cert.DocumentIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse).ToList();
        var numbers = await db.Documents.Where(d => ids.Contains(d.Id)).Select(d => d.DocumentNumber).ToListAsync(ct);
        var verifier = await db.Users.Where(u => u.Id == cert.VerifiedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var approver = await db.Users.Where(u => u.Id == cert.FinalApprovedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        return Result<DispositionCertificateDto>.Ok(new DispositionCertificateDto(
            requestId, cert.CertificateNumber, ids, numbers, cert.DestructionMethod, verifier, approver, cert.GeneratedAtUtc));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Result<DispositionRequestDto> Fail(string e) => Result<DispositionRequestDto>.Fail(e);
    private async Task<Result<DispositionRequestDto>> OkAsync(long id, CancellationToken ct) =>
        Result<DispositionRequestDto>.Ok((await BuildDtoAsync(id, ct))!);

    private Task<bool> HasActiveHoldAsync(Document doc, CancellationToken ct) =>
        db.LegalHolds.AnyAsync(h => h.ReleasedAtUtc == null && (
            (h.Scope == LegalHoldScope.Document && h.DocumentId == doc.Id)
            || (h.Scope == LegalHoldScope.Folder && h.FolderId != null && h.FolderId == doc.FolderId)
            || (h.Scope == LegalHoldScope.OrgUnit && h.OrgUnitId == doc.OwningOrgUnitId)), ct);

    private async Task ReleaseBoxAsync(Document doc, CancellationToken ct)
    {
        if (doc.BoxId is not { } boxId) return;
        var box = await db.Boxes.FirstOrDefaultAsync(b => b.Id == boxId, ct);
        if (box is not null && box.CurrentCount > 0) box.CurrentCount--;
        doc.BoxId = null;
    }

    private async Task SetRetentionStatusAsync(long documentId, DocumentRetentionStatus status, CancellationToken ct)
    {
        var ret = await db.DocumentRetentions.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
        if (ret is null) return;
        ret.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private Task<RetentionPolicy?> PolicyForAsync(long documentId, CancellationToken ct) =>
        (from d in db.Documents
         where d.Id == documentId
         join p in db.RetentionPolicies on d.DocumentTypeId equals p.DocumentTypeId
         select p).FirstOrDefaultAsync(ct);

    /// <summary>Apply an approved renewal: set the new expiry, reactivate the document, and record retention history.</summary>
    private async Task CompleteRenewAsync(DispositionRequest req, Document doc, DateOnly newExpiry, CancellationToken ct)
    {
        req.NewExpiryDate = newExpiry;
        req.Status = DispositionStatus.Completed;
        doc.ExpiryDate = newExpiry;
        doc.Status = DocumentStatus.Active;
        await RenewRetentionAsync(req.DocumentId, newExpiry, ct);
    }

    private async Task RenewRetentionAsync(long documentId, DateOnly newExpiry, CancellationToken ct)
    {
        var ret = await db.DocumentRetentions.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
        if (ret is null) return;
        ret.OriginalExpiryDate ??= ret.ExpiryDate;
        ret.ExpiryDate = newExpiry;
        ret.Status = DocumentRetentionStatus.Renewed;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Tells the person who raised the request how it ended. Without this a requester who
    /// only holds Disposition.Create never learns the outcome: the two queues are the reviewers'
    /// inboxes, and a rejected request simply disappears from them.</summary>
    private async Task NotifyRequesterAsync(DispositionRequest req, string title, string? body, CancellationToken ct)
    {
        if (req.RequestedByUserId <= 0 || req.RequestedByUserId == Uid) return;   // no self-notification
        db.Notifications.Add(new Notification
        {
            RecipientUserId = req.RequestedByUserId, Title = title, Body = body,
            Type = NotificationType.Info, Channels = NotificationChannel.InApp,
            EntityType = "Disposition", EntityId = req.Id,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Whether the caller occupies the seat this request is currently waiting on — the
    /// requester's department head before the first approval, مدير قسم الأرشيف after it. The System
    /// Administrator is treated as occupying every seat, otherwise a vacant seat strands the queue.</summary>
    private async Task<bool> IsCurrentApproverAsync(DispositionRequest req, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return true;
        return req.Status == DispositionStatus.PendingVerification
            ? await DepartmentApproverAsync(req.RequestedByUserId, ct) == Uid
            : await ArchiveManagerAsync(ct) == Uid;
    }

    /// <summary>Notifies one named seat. The chain routes to a person now, not to "whoever holds
    /// the permission", so a queue cannot quietly fan out to every manager in the institution.</summary>
    private async Task NotifyUserAsync(long? userId, string title, string? body, long entityId, CancellationToken ct)
    {
        if (userId is not { } uid || uid <= 0 || uid == Uid) return;   // no self-notification
        db.Notifications.Add(new Notification
        {
            RecipientUserId = uid, Title = title, Body = body,
            Type = NotificationType.Task, Channels = NotificationChannel.InApp,
            EntityType = "Disposition", EntityId = entityId,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task NotifyByPermissionAsync(string permCode, string title, string? body, long entityId, CancellationToken ct)
    {
        var userIds = await db.Users
            .Where(u => u.IsActive && u.Id != Uid && db.UserRoles.Any(ur => ur.UserId == u.Id && (
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator") ||
                db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId &&
                    db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == permCode)))))
            .Select(u => u.Id).ToListAsync(ct);

        foreach (var uid in userIds)
            db.Notifications.Add(new Notification
            {
                RecipientUserId = uid, Title = title, Body = body,
                Type = NotificationType.Task, Channels = NotificationChannel.InApp,
                EntityType = "Disposition", EntityId = entityId,
            });
        if (userIds.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task<DispositionRequestDto?> BuildDtoAsync(long id, CancellationToken ct)
    {
        // Same scope as the list: a request hidden from the queue does not open by id either.
        var row = await (await VisibleAsync(db.DispositionRequests, ct)).Where(r => r.Id == id)
            .Select(r => new Row(r,
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.DocumentNumber).FirstOrDefault(),
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.Title).FirstOrDefault(),
                db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.ExpiryDate).FirstOrDefault(),
                db.Boxes.Where(bx => bx.Id == db.Documents.Where(d => d.Id == r.DocumentId).Select(d => d.BoxId).FirstOrDefault()).Select(bx => bx.BoxCode).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.RequestedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.VerifiedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.FinalApprovedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.Users.Where(u => u.Id == r.RejectedByUserId).Select(u => u.FullName).FirstOrDefault(),
                db.DispositionCertificates.Where(c => c.Id == r.CertificateId).Select(c => c.CertificateNumber).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    private sealed record Row(
        DispositionRequest R, string? DocNumber, string? DocTitle, DateOnly? ExpiryDate, string? BoxCode,
        string? RequestedByName, string? VerifiedByName, string? FinalApprovedByName, string? RejectedByName,
        string? CertificateNumber);

    private static DispositionRequestDto ToDto(Row x)
    {
        var r = x.R;
        return new DispositionRequestDto(
            r.Id, r.DocumentId, x.DocNumber, x.DocTitle,
            (int)r.RequestedAction, ActionLabel(r.RequestedAction),
            r.Reason, r.RequestedByUserId, x.RequestedByName, r.RequestedAtUtc,
            (int)r.Status, StatusLabel(r.Status),
            r.VerifiedByUserId, x.VerifiedByName, r.VerifiedAtUtc, r.VerificationNotes,
            r.FinalApprovedByUserId, x.FinalApprovedByName, r.FinalApprovedAtUtc, r.FinalApprovalNotes,
            r.RejectedByUserId, x.RejectedByName, r.RejectedAtUtc, r.RejectionReason,
            r.NewExpiryDate, (int)r.Method, r.CustomMethod, r.CertificateId, x.CertificateNumber,
            x.ExpiryDate, x.BoxCode);
    }

    private static string ActionLabel(DispositionAction a) => a switch
    {
        DispositionAction.Destroy => "إتلاف",
        DispositionAction.Renew   => "تجديد",
        _ => a.ToString(),
    };

    private static string StatusLabel(DispositionStatus s) => s switch
    {
        DispositionStatus.PendingVerification  => "بانتظار التحقق",
        DispositionStatus.PendingFinalApproval => "بانتظار الموافقة النهائية",
        DispositionStatus.Approved             => "مُعتمَد",
        DispositionStatus.Rejected             => "مرفوض",
        DispositionStatus.Completed            => "مكتمل",
        _ => s.ToString(),
    };

    private static string MethodLabel(DestructionMethod m) => m switch
    {
        DestructionMethod.CryptoShred    => "محو تشفيري",
        DestructionMethod.SecureOverwrite=> "كتابة فوقية آمنة",
        DestructionMethod.DeleteFixityVoid => "حذف وإبطال البصمة",
        DestructionMethod.Shredding      => "تقطيع",
        DestructionMethod.Incineration   => "حرق",
        DestructionMethod.Pulping        => "تذويب",
        DestructionMethod.Degaussing     => "إزالة مغناطيسية",
        _ => "أخرى",
    };
}
