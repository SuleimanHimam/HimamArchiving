using System.Text;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// Generates the internal correspondence a workflow run needs: one message per step to the assignee,
/// and one closing message to the requester.
///
/// <para>Every message it writes hangs off the requester's original mail (<c>ParentId</c>), so opening
/// the request shows the whole approval chain as one conversation in the existing thread view. Nothing
/// here duplicates a document: the step message carries no attachments of its own and points back at
/// the root, whose attachments and permissions continue to apply.</para>
/// </summary>
public sealed class WorkflowMailRouter(
    AppDbContext db,
    ILogger<WorkflowMailRouter> log) : IWorkflowMailRouter
{
    public async Task<long?> SendStepAsync(long instanceId, long taskId, CancellationToken ct = default)
    {
        var task = await db.WorkflowTasks.Include(t => t.Stage)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return null;

        var instance = await db.WorkflowInstances.Include(i => i.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance?.RootInternalMailId is not { } rootId) return null;   // not a mail-driven run

        var root = await db.InternalMails.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == rootId, ct);
        if (root is null) return null;

        var recipientId = await RecipientOfAsync(task, ct);
        if (recipientId is not { } to)
        {
            // A seat with no occupant: the task stays open and escalation deals with it. Inventing a
            // recipient here would deliver an approval request to the wrong desk.
            log.LogWarning("Workflow task {TaskId} has no resolvable recipient; no step mail sent", taskId);
            return null;
        }

        var (stepNo, totalSteps) = await StepPositionAsync(instance, task, ct);
        var fromUserId = await LastActorAsync(instance, ct) ?? root.SenderUserId;

        var body = new StringBuilder()
            .Append("<p>وصلتك معاملة ضمن سير عمل <strong>").Append(Escape(instance.WorkflowDefinition.Name)).Append("</strong>.</p>")
            .Append("<p>المرحلة الحالية: <strong>").Append(Escape(task.Stage.Name)).Append("</strong>")
            .Append(totalSteps > 0 ? $" (خطوة {stepNo} من {totalSteps})" : string.Empty)
            .Append("</p>")
            .Append("<p>الموعد الأقصى للرد: ").Append(task.DueAt.ToString("yyyy-MM-dd HH:mm")).Append(" (UTC)</p>")
            .Append("<p>يرجى اتخاذ الإجراء المطلوب من داخل المراسلة.</p>")
            .ToString();

        var mail = await NewMailAsync(root, fromUserId, to, root.Subject, body, instance.Id, task.Id,
            WorkflowMailRole.Step, ct);

        task.InternalMailId = mail.Id;
        await db.SaveChangesAsync(ct);
        return mail.Id;
    }

    public async Task<long?> SendResultAsync(long instanceId, WorkflowOutcome outcome, string? reason,
        CancellationToken ct = default)
    {
        var instance = await db.WorkflowInstances.Include(i => i.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance?.RootInternalMailId is not { } rootId) return null;

        var root = await db.InternalMails.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == rootId, ct);
        if (root is null) return null;

        // Back to whoever raised the request — the sender of the original correspondence.
        var to = instance.InitiatedByUserId ?? root.SenderUserId;
        var fromUserId = await LastActorAsync(instance, ct) ?? to;

        var heading = outcome switch
        {
            WorkflowOutcome.Approved => "تمت الموافقة على طلبك",
            WorkflowOutcome.Rejected => "رُفض طلبك",
            _ => "أُعيد طلبك للتعديل",
        };

        var body = new StringBuilder()
            .Append("<p><strong>").Append(heading).Append("</strong></p>")
            .Append("<p>سير العمل: ").Append(Escape(instance.WorkflowDefinition.Name)).Append("</p>");

        if (!string.IsNullOrWhiteSpace(reason))
            body.Append("<p>السبب: ").Append(Escape(reason)).Append("</p>");

        body.Append(await ChainHtmlAsync(instance.Id, ct));

        if (outcome == WorkflowOutcome.ChangesRequested)
            body.Append("<p>بعد التعديل، أعد الإرسال من المراسلة الأصلية وسيتابع سير العمل من حيث توقّف.</p>");

        var mail = await NewMailAsync(root, fromUserId, to, root.Subject, body.ToString(), instance.Id, null,
            WorkflowMailRole.Result, ct);
        await db.SaveChangesAsync(ct);
        return mail.Id;
    }

    // ── building blocks ──────────────────────────────────────────────────────────────────────────

    /// <summary>Writes one generated message: addressed to a single employee, threaded under the
    /// request, carrying the run's identifiers so the inbox can badge it.</summary>
    private async Task<InternalMail> NewMailAsync(
        InternalMail root, long fromUserId, long toUserId, string subject, string body,
        long instanceId, long? taskId, WorkflowMailRole role, CancellationToken ct)
    {
        var sender = await db.Users.Where(u => u.Id == fromUserId)
            .Select(u => new { u.FullName, u.OrgUnitId }).FirstOrDefaultAsync(ct);
        var toName = await db.Users.Where(u => u.Id == toUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;

        var mail = new InternalMail
        {
            InstitutionId = root.InstitutionId,
            MailNumber = await InternalMailNumbering.NextAsync(db, root.InstitutionId, ct),
            ReferenceNumber = root.ReferenceNumber,
            SenderUserId = fromUserId,
            SenderName = sender?.FullName,
            SenderOrgUnitId = sender?.OrgUnitId,
            Subject = subject,
            Body = body,
            Keywords = root.Keywords,
            MailDate = now,
            RegistrationDate = now,
            // The category is inherited so the message prints on the same letterhead as the request.
            // It cannot re-trigger the workflow: generated mail never goes through ComposeAsync.
            CategoryId = root.CategoryId,
            Classification = root.Classification,
            Priority = root.Priority,
            Status = CorrespondenceStatus.Sent,
            FileNumber = root.FileNumber,
            ReplyToId = root.Id,
            ParentId = root.ParentId ?? root.Id,
            WorkflowInstanceId = instanceId,
            WorkflowTaskId = taskId,
            WorkflowRole = role,
            CreatedBy = fromUserId,
        };

        mail.Recipients.Add(new InternalRecipient
        {
            Addressing = CorrespondenceAddressing.To,
            TargetKind = RecipientKind.Employee,
            TargetId = toUserId,
            TargetName = toName,
            CreatedBy = fromUserId,
        });
        mail.Reads.Add(new InternalRead
        {
            UserId = toUserId,
            ReadStatus = CorrespondenceReadStatus.Delivered,
            DeliveredAt = now,
            CreatedBy = fromUserId,
        });

        db.InternalMails.Add(mail);
        await db.SaveChangesAsync(ct);
        return mail;
    }

    /// <summary>The user a task should be delivered to: the resolved occupant, or whoever holds the
    /// seat now if the occupant changed since the task was opened.</summary>
    private async Task<long?> RecipientOfAsync(WorkflowTask task, CancellationToken ct)
    {
        if (task.AssignedToUserId is { } u) return u;
        if (task.AssignedToPositionId is not { } pos) return null;
        return await db.Positions.Where(p => p.Id == pos)
            .Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
    }

    /// <summary>Who moved the run to this point — the sender of the generated message, so the chain
    /// reads as one person handing the file to the next rather than as mail from nobody.</summary>
    private async Task<long?> LastActorAsync(WorkflowInstance instance, CancellationToken ct) =>
        await db.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instance.Id && t.CompletedByUserId != null)
            .OrderByDescending(t => t.CompletedAt)
            .Select(t => t.CompletedByUserId)
            .FirstOrDefaultAsync(ct)
        ?? instance.InitiatedByUserId;

    /// <summary>«خطوة 2 من 5» — counted over the definition's stages, which is what the recipient
    /// means by a step, rather than over tasks (a parallel group is several tasks in one step).</summary>
    private async Task<(int Step, int Total)> StepPositionAsync(WorkflowInstance instance, WorkflowTask task,
        CancellationToken ct)
    {
        var orders = await db.WorkflowStages
            .Where(s => s.WorkflowDefinitionId == instance.WorkflowDefinitionId)
            .OrderBy(s => s.Order)
            .Select(s => new { s.Id, s.Order })
            .ToListAsync(ct);
        var index = orders.FindIndex(s => s.Id == task.StageId);
        return (index < 0 ? 0 : index + 1, orders.Count);
    }

    /// <summary>The decisions taken so far, as a list the requester can read without opening the run.</summary>
    private async Task<string> ChainHtmlAsync(long instanceId, CancellationToken ct)
    {
        var rows = await db.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instanceId && t.CompletedAt != null)
            .OrderBy(t => t.CompletedAt)
            .Select(t => new { StageName = t.Stage.Name, t.ActionTaken, t.CompletedByUserId })
            .ToListAsync(ct);
        if (rows.Count == 0) return string.Empty;

        var names = await db.Users
            .Where(u => rows.Select(r => r.CompletedByUserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var sb = new StringBuilder("<ul>");
        foreach (var r in rows)
        {
            var mark = r.ActionTaken switch
            {
                WorkflowActionType.Approve or WorkflowActionType.Close => "✓",
                WorkflowActionType.Reject => "✗",
                _ => "•",
            };
            var who = r.CompletedByUserId is { } id && names.TryGetValue(id, out var n) ? n : null;
            sb.Append("<li>").Append(mark).Append(' ').Append(Escape(r.StageName));
            if (who is not null) sb.Append(" — ").Append(Escape(who));
            sb.Append("</li>");
        }
        return sb.Append("</ul>").ToString();
    }

    private static string Escape(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}

/// <summary>Shared internal-mail numbering. Lives here so the router and the mail service cannot
/// drift into two different sequences for the same register.</summary>
internal static class InternalMailNumbering
{
    public static async Task<string> NextAsync(AppDbContext db, long institutionId, CancellationToken ct)
    {
        var prefix = $"INT-{DateTime.UtcNow.Year}-";
        var last = await db.InternalMails.IgnoreQueryFilters()
            .Where(x => x.InstitutionId == institutionId && x.MailNumber.StartsWith(prefix))
            .OrderByDescending(x => x.MailNumber)
            .Select(x => x.MailNumber)
            .FirstOrDefaultAsync(ct);

        var seq = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{seq:00000}";
    }
}
