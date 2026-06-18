using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.IncomingMail;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class IncomingMailService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : IIncomingMailService
{
    private const string EntityType = "IncomingMail";

    public async Task<PagedResult<IncomingMailListItem>> ListAsync(IncomingMailQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = db.IncomingMails
            .Where(m => (int)m.Confidentiality <= (int)currentUser.Clearance); // clearance gate

        if (query.Status is { } status)
            q = q.Where(m => m.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(m =>
                m.TransactionNumber.Contains(s) ||
                m.Subject.Contains(s) ||
                m.SenderEntity.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(m => new IncomingMailListItem(
                m.Id, m.TransactionNumber, m.SenderEntity, m.Subject,
                m.Confidentiality.ToString(), m.Priority.ToString(), m.Status.ToString(),
                m.ReceivedDate, m.AssignedToPositionId, m.AssignedToOrgUnitId, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<IncomingMailListItem>
        {
            Items = items, Page = page, PageSize = size, TotalCount = total,
        };
    }

    public async Task<Result<IncomingMailDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        var mail = await db.IncomingMails.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mail is null) return Result<IncomingMailDetail>.Fail("المعاملة غير موجودة");
        if (!CanAccess(mail)) return Result<IncomingMailDetail>.Fail("لا تملك صلاحية الوصول لهذه المعاملة");

        return Result<IncomingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    public async Task<Result<IncomingMailDetail>> CreateAsync(CreateIncomingMailRequest r, CancellationToken ct = default)
    {
        var mail = new IncomingMail
        {
            TransactionNumber = await NextTransactionNumberAsync(ct),
            SenderEntity = r.SenderEntity,
            SenderName = r.SenderName,
            SenderReference = r.SenderReference,
            Subject = r.Subject,
            Body = r.Body,
            IssueDate = r.IssueDate,
            ReceivedDate = r.ReceivedDate,
            Confidentiality = r.Confidentiality,
            Priority = r.Priority,
            Keywords = r.Keywords,
            DocumentTypeId = r.DocumentTypeId,
            CategoryId = r.CategoryId,
            ParentMailId = r.ParentMailId,
            Status = IncomingMailStatus.New,
        };

        db.IncomingMails.Add(mail);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", EntityType, mail.Id, mail.Subject, ct: ct);

        return Result<IncomingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    public async Task<Result<IncomingMailDetail>> ActAsync(long id, IncomingMailActionRequest r, CancellationToken ct = default)
    {
        var mail = await db.IncomingMails.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mail is null) return Result<IncomingMailDetail>.Fail("المعاملة غير موجودة");
        if (!CanAccess(mail)) return Result<IncomingMailDetail>.Fail("لا تملك صلاحية الوصول لهذه المعاملة");

        string action;
        switch (r.Action)
        {
            case IncomingMailActionType.Forward:
                mail.AssignedToPositionId = r.ToPositionId;
                mail.AssignedToOrgUnitId = r.ToOrgUnitId;
                mail.AssignedToUserId = r.ToUserId;
                mail.Status = IncomingMailStatus.Assigned;
                action = "Forwarded";
                break;
            case IncomingMailActionType.Approve:
                mail.Status = IncomingMailStatus.InProgress;
                action = "Approved";
                break;
            case IncomingMailActionType.Hold:
                mail.Status = IncomingMailStatus.OnHold;
                action = "Held";
                break;
            case IncomingMailActionType.Close:
                mail.Status = IncomingMailStatus.Closed;
                mail.ClosedAt = DateTime.UtcNow;
                mail.ClosedBy = currentUser.UserId;
                action = "Closed";
                break;
            case IncomingMailActionType.Archive:
                mail.Status = IncomingMailStatus.Archived;
                action = "Archived";
                break;
            default:
                return Result<IncomingMailDetail>.Fail("إجراء غير معروف");
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(action, EntityType, mail.Id, mail.Subject, newValues: r.Note, ct: ct);

        return Result<IncomingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    // ---- helpers ----

    private bool CanAccess(IncomingMail m) => (int)m.Confidentiality <= (int)currentUser.Clearance;

    private async Task<string> NextTransactionNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"IN-{year}-";
        var count = await db.IncomingMails.IgnoreQueryFilters()
            .CountAsync(m => m.TransactionNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:0000}";
    }

    private async Task<IncomingMailDetail> ToDetailAsync(IncomingMail m, CancellationToken ct)
    {
        var timeline = await db.AuditLogs
            .Where(a => a.EntityType == EntityType && a.EntityId == m.Id)
            .OrderBy(a => a.Id)
            .Select(a => new MailTimelineEntry(a.Id, a.Action, a.UserId, a.CreatedAt, a.NewValues))
            .ToListAsync(ct);

        return new IncomingMailDetail(
            m.Id, m.TransactionNumber, m.SenderEntity, m.SenderName, m.SenderReference,
            m.Subject, m.Body, m.IssueDate, m.ReceivedDate,
            m.Confidentiality.ToString(), m.Priority.ToString(), m.Status.ToString(), m.Keywords,
            m.AssignedToPositionId, m.AssignedToOrgUnitId, m.AssignedToUserId, m.ParentMailId,
            m.CreatedAt, timeline);
    }
}
