using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.OutgoingMail;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class OutgoingMailService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : IOutgoingMailService
{
    private const string EntityType = "OutgoingMail";

    public async Task<PagedResult<OutgoingMailListItem>> ListAsync(OutgoingMailQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = db.OutgoingMails
            .Where(m => (int)m.Confidentiality <= (int)currentUser.Clearance); // clearance gate

        if (query.Status is { } status) q = q.Where(m => m.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(m => m.LetterNumber.Contains(s) || m.Subject.Contains(s) || m.RecipientEntity.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(m => new OutgoingMailListItem(
                m.Id, m.LetterNumber, m.RecipientEntity, m.Subject,
                m.Confidentiality.ToString(), m.Priority.ToString(), m.Status.ToString(),
                m.SentDate, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<OutgoingMailListItem> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<OutgoingMailDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        var mail = await db.OutgoingMails.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mail is null) return Result<OutgoingMailDetail>.Fail("الكتاب غير موجود");
        if (!CanAccess(mail)) return Result<OutgoingMailDetail>.Fail("لا تملك صلاحية الوصول لهذا الكتاب");
        return Result<OutgoingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    public async Task<Result<OutgoingMailDetail>> CreateAsync(CreateOutgoingMailRequest r, CancellationToken ct = default)
    {
        var mail = new OutgoingMail
        {
            LetterNumber = await NextLetterNumberAsync(ct),
            RecipientEntity = r.RecipientEntity,
            RecipientName = r.RecipientName,
            Subject = r.Subject,
            Body = r.Body,
            LetterTemplateId = r.LetterTemplateId,
            SignatoryPositionId = r.SignatoryPositionId,
            Confidentiality = r.Confidentiality,
            Priority = r.Priority,
            InReplyToIncomingMailId = r.InReplyToIncomingMailId,
            Status = OutgoingMailStatus.Draft,
        };
        db.OutgoingMails.Add(mail);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", EntityType, mail.Id, mail.Subject, ct: ct);
        return Result<OutgoingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    public async Task<Result<OutgoingMailDetail>> UpdateAsync(long id, UpdateOutgoingMailRequest r, CancellationToken ct = default)
    {
        var mail = await db.OutgoingMails.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mail is null) return Result<OutgoingMailDetail>.Fail("الكتاب غير موجود");
        if (!CanAccess(mail)) return Result<OutgoingMailDetail>.Fail("لا تملك صلاحية الوصول لهذا الكتاب");
        if (mail.Status != OutgoingMailStatus.Draft)
            return Result<OutgoingMailDetail>.Fail("لا يمكن تعديل الكتاب بعد إرساله للاعتماد");

        mail.RecipientEntity = r.RecipientEntity;
        mail.RecipientName = r.RecipientName;
        mail.Subject = r.Subject;
        mail.Body = r.Body;
        mail.LetterTemplateId = r.LetterTemplateId;
        mail.SignatoryPositionId = r.SignatoryPositionId;
        mail.Confidentiality = r.Confidentiality;
        mail.Priority = r.Priority;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", EntityType, mail.Id, mail.Subject, ct: ct);
        return Result<OutgoingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    public async Task<Result<OutgoingMailDetail>> ActAsync(long id, OutgoingMailActionRequest r, CancellationToken ct = default)
    {
        var mail = await db.OutgoingMails.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mail is null) return Result<OutgoingMailDetail>.Fail("الكتاب غير موجود");
        if (!CanAccess(mail)) return Result<OutgoingMailDetail>.Fail("لا تملك صلاحية الوصول لهذا الكتاب");

        string action;
        switch (r.Action)
        {
            case OutgoingMailActionType.SubmitForApproval:
                if (mail.Status != OutgoingMailStatus.Draft)
                    return Result<OutgoingMailDetail>.Fail("الكتاب ليس في حالة مسودة");
                mail.Status = OutgoingMailStatus.PendingApproval;
                action = "SubmittedForApproval";
                break;
            case OutgoingMailActionType.Approve:
                if (mail.Status != OutgoingMailStatus.PendingApproval)
                    return Result<OutgoingMailDetail>.Fail("الكتاب ليس بانتظار الاعتماد");
                mail.Status = OutgoingMailStatus.Approved;
                mail.ApprovedBy = currentUser.UserId;
                mail.ApprovedAt = DateTime.UtcNow;
                action = "Approved";
                break;
            case OutgoingMailActionType.Send:
                if (mail.Status != OutgoingMailStatus.Approved)
                    return Result<OutgoingMailDetail>.Fail("يجب اعتماد الكتاب قبل إرساله");
                mail.Status = OutgoingMailStatus.Sent;
                mail.SentDate = DateTime.UtcNow;
                action = "Sent";
                break;
            case OutgoingMailActionType.Archive:
                mail.Status = OutgoingMailStatus.Archived;
                action = "Archived";
                break;
            default:
                return Result<OutgoingMailDetail>.Fail("إجراء غير معروف");
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(action, EntityType, mail.Id, mail.Subject, newValues: r.Note, ct: ct);
        return Result<OutgoingMailDetail>.Ok(await ToDetailAsync(mail, ct));
    }

    // ---- helpers ----

    private bool CanAccess(OutgoingMail m) => (int)m.Confidentiality <= (int)currentUser.Clearance;

    private async Task<string> NextLetterNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"OUT-{year}-";
        var count = await db.OutgoingMails.IgnoreQueryFilters()
            .CountAsync(m => m.LetterNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:0000}";
    }

    private async Task<OutgoingMailDetail> ToDetailAsync(OutgoingMail m, CancellationToken ct)
    {
        var timeline = await db.AuditLogs
            .Where(a => a.EntityType == EntityType && a.EntityId == m.Id)
            .OrderBy(a => a.Id)
            .Select(a => new OutgoingMailTimelineEntry(a.Id, a.Action, a.UserId, a.CreatedAt, a.NewValues))
            .ToListAsync(ct);

        return new OutgoingMailDetail(
            m.Id, m.LetterNumber, m.RecipientEntity, m.RecipientName, m.Subject, m.Body,
            m.LetterTemplateId, m.SignatoryPositionId, m.Confidentiality.ToString(), m.Priority.ToString(),
            m.Status.ToString(), m.SentDate, m.InReplyToIncomingMailId, m.ApprovedBy, m.ApprovedAt,
            m.CreatedAt, timeline);
    }
}
