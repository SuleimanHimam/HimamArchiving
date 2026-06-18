using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Lifecycle;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class LifecycleService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : ILifecycleService
{
    // ---- Retention policies ----

    public async Task<IReadOnlyList<RetentionPolicyDto>> ListPoliciesAsync(CancellationToken ct = default) =>
        await db.RetentionPolicies.Include(p => p.DocumentType).OrderBy(p => p.DocumentType.Name)
            .Select(p => new RetentionPolicyDto(p.Id, p.DocumentTypeId, p.DocumentType.Name,
                p.RetentionMonths, p.DisposalAction.ToString(), p.RequiresApproval, p.Description))
            .ToListAsync(ct);

    public async Task<Result<RetentionPolicyDto>> CreatePolicyAsync(CreateRetentionPolicyRequest r, CancellationToken ct = default)
    {
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == r.DocumentTypeId, ct);
        if (type is null) return Result<RetentionPolicyDto>.Fail("نوع الوثيقة غير موجود");

        var e = new RetentionPolicy
        {
            DocumentTypeId = r.DocumentTypeId,
            RetentionMonths = r.RetentionMonths,
            DisposalAction = r.DisposalAction,
            RequiresApproval = r.RequiresApproval,
            Description = r.Description,
        };
        db.RetentionPolicies.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "RetentionPolicy", e.Id, type.Name, ct: ct);
        return Result<RetentionPolicyDto>.Ok(new RetentionPolicyDto(e.Id, e.DocumentTypeId, type.Name,
            e.RetentionMonths, e.DisposalAction.ToString(), e.RequiresApproval, e.Description));
    }

    // ---- Expiring documents ----

    public async Task<IReadOnlyList<ExpiringDocumentDto>> ExpiringAsync(int withinDays, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(Math.Max(0, withinDays));

        return await db.Documents
            .Where(d => d.ExpiryDate != null && d.ExpiryDate <= horizon)
            .Where(d => (int)d.Confidentiality <= (int)currentUser.Clearance)
            .OrderBy(d => d.ExpiryDate)
            .Select(d => new ExpiringDocumentDto(
                d.Id, d.DocumentNumber, d.Title, d.ExpiryDate!.Value,
                d.ExpiryDate!.Value.DayNumber - today.DayNumber))
            .ToListAsync(ct);
    }

    // ---- Disposal flow ----

    public async Task<IReadOnlyList<DisposalRequestDto>> ListDisposalRequestsAsync(CancellationToken ct = default) =>
        // IgnoreQueryFilters so disposal records remain visible after their document is soft-deleted
        // (the row is kept permanently as the historical archive).
        await db.DisposalRequests.IgnoreQueryFilters().Include(r => r.Document).OrderByDescending(r => r.RequestedAt)
            .Select(r => ToDto(r))
            .ToListAsync(ct);

    public async Task<Result<DisposalRequestDto>> RequestDisposalAsync(CreateDisposalRequestRequest r, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == r.DocumentId, ct);
        if (doc is null) return Result<DisposalRequestDto>.Fail("الوثيقة غير موجودة");

        if (await db.DisposalRequests.AnyAsync(x => x.DocumentId == r.DocumentId
                && (x.Status == DisposalRequestStatus.Pending || x.Status == DisposalRequestStatus.Approved), ct))
            return Result<DisposalRequestDto>.Fail("يوجد طلب إتلاف قائم لهذه الوثيقة");

        var e = new DisposalRequest
        {
            DocumentId = r.DocumentId,
            Action = r.Action,
            Status = DisposalRequestStatus.Pending,
            RequestedByUserId = currentUser.UserId ?? 0,
            RequestedAt = DateTime.UtcNow,
            Justification = r.Justification,
        };
        db.DisposalRequests.Add(e);
        doc.Status = DocumentStatus.PendingDisposal;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DisposalRequested", "Document", doc.Id, doc.Title, newValues: r.Justification, ct: ct);

        await db.Entry(e).Reference(x => x.Document).LoadAsync(ct);
        return Result<DisposalRequestDto>.Ok(ToDto(e));
    }

    public async Task<Result<DisposalRequestDto>> DecideAsync(long id, DisposalDecisionRequest r, CancellationToken ct = default)
    {
        var req = await db.DisposalRequests.Include(x => x.Document).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DisposalRequestDto>.Fail("طلب الإتلاف غير موجود");
        if (req.Status != DisposalRequestStatus.Pending)
            return Result<DisposalRequestDto>.Fail("تم البت في هذا الطلب مسبقًا");

        req.ApprovedByUserId = currentUser.UserId;
        req.ApprovedAt = DateTime.UtcNow;

        if (r.Approve)
        {
            req.Status = DisposalRequestStatus.Approved;
        }
        else
        {
            req.Status = DisposalRequestStatus.Rejected;
            req.Document.Status = DocumentStatus.Active; // restore the document
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(r.Approve ? "DisposalApproved" : "DisposalRejected",
            "Document", req.DocumentId, req.Document.Title, newValues: r.Note, ct: ct);
        return Result<DisposalRequestDto>.Ok(ToDto(req));
    }

    public async Task<Result<DisposalRequestDto>> ExecuteAsync(long id, CancellationToken ct = default)
    {
        var req = await db.DisposalRequests.Include(x => x.Document).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (req is null) return Result<DisposalRequestDto>.Fail("طلب الإتلاف غير موجود");
        if (req.Status != DisposalRequestStatus.Approved)
            return Result<DisposalRequestDto>.Fail("يجب اعتماد الطلب قبل تنفيذه");

        var doc = req.Document;
        switch (req.Action)
        {
            case DisposalAction.Destroy:
                // Soft-delete explicitly (not via Remove): the DisposalRequest navigation is tracked and
                // references this document with a required FK, so Remove() would try to cascade-sever it.
                // The disposal record itself is kept permanently as the historical archive.
                doc.Status = DocumentStatus.Disposed;
                doc.IsDeleted = true;
                doc.DeletedAt = DateTime.UtcNow;
                doc.DeletedBy = currentUser.UserId;
                break;
            case DisposalAction.Transfer:
                doc.Status = DocumentStatus.Archived;
                break;
            case DisposalAction.Review:
                doc.Status = DocumentStatus.PendingDisposal;
                break;
            case DisposalAction.Retain:
                doc.Status = DocumentStatus.Active;
                break;
        }

        req.Status = DisposalRequestStatus.Executed;
        req.ExecutedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DisposalExecuted", "Document", req.DocumentId, doc.Title,
            newValues: req.Action.ToString(), ct: ct);
        return Result<DisposalRequestDto>.Ok(ToDto(req));
    }

    private static DisposalRequestDto ToDto(DisposalRequest r) => new(
        r.Id, r.DocumentId, r.Document.DocumentNumber, r.Document.Title, r.Action.ToString(), r.Status.ToString(),
        r.RequestedByUserId, r.RequestedAt, r.ApprovedByUserId, r.ApprovedAt, r.ExecutedAt, r.Justification);
}
