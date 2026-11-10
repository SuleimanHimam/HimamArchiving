using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>The routing engine: admin-defined definitions run as instances whose open tasks form worklists.
/// Tasks bind to a <b>position</b> (the seat) so work follows occupancy, not the person.</summary>
public sealed class WorkflowService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IDelegationService delegations,
    IWorkflowMailRouter mailRouter) : IWorkflowService
{
    private const string EntityType = "Workflow";

    /// <summary>Entity type of a run driven by internal correspondence.</summary>
    public const string InternalMailEntityType = "InternalMail";

    /// <summary>Tasks opened during the current call that still need their step correspondence.
    ///
    /// <para>The mail cannot be written inside <see cref="OpenTaskCoreAsync"/>: a task has no id until
    /// it is saved, and saving there would dispatch the domain events queued by the caller before the
    /// run's own state change is committed. So the ids are collected and flushed once, after the save
    /// that makes the advance real — which also means a failed advance sends nothing.</para></summary>
    private readonly List<WorkflowTask> _pendingTaskObjects = [];

    /// <summary>Writes the step correspondence for every task opened since the last flush. Ids are read
    /// after the save, so each task exists before its message points at it. Failure to generate a
    /// message never fails the advance — the task is still in the worklist, and the notification the
    /// engine already queued still fired.</summary>
    private async Task FlushStepMailsAsync(long instanceId, CancellationToken ct)
    {
        if (_pendingTaskObjects.Count == 0) return;
        var ids = _pendingTaskObjects.Where(t => t.Id != 0).Select(t => t.Id).ToList();
        _pendingTaskObjects.Clear();
        foreach (var taskId in ids)
            await mailRouter.SendStepAsync(instanceId, taskId, ct);
    }

    // ---- Definitions ----

    public async Task<IReadOnlyList<WorkflowDefinitionListItem>> ListDefinitionsAsync(CancellationToken ct = default) =>
        await db.WorkflowDefinitions.OrderBy(d => d.Name)
            .Select(d => new WorkflowDefinitionListItem(
                d.Id, d.Name, d.TriggerModule, d.Version, d.IsActive, d.Stages.Count,
                d.MailCategoryId,
                db.MailCategories.Where(c => c.Id == d.MailCategoryId).Select(c => c.Name).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<Result<WorkflowDefinitionDto>> GetDefinitionAsync(long id, CancellationToken ct = default)
    {
        var def = await db.WorkflowDefinitions.Include(d => d.Stages)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        return def is null
            ? Result<WorkflowDefinitionDto>.Fail("تعريف سير العمل غير موجود")
            : Result<WorkflowDefinitionDto>.Ok(ToDefinitionDto(def));
    }

    public async Task<Result<WorkflowDefinitionDto>> CreateDefinitionAsync(CreateWorkflowDefinitionRequest r, CancellationToken ct = default)
    {
        if (r.Stages is null || r.Stages.Count == 0)
            return Result<WorkflowDefinitionDto>.Fail("يجب تعريف مرحلة واحدة على الأقل");

        // A mail-triggered route with no تصنيف can never fire: nothing about a composed message
        // would ever select it. Refused at creation rather than left to be discovered later as a
        // route that silently never runs.
        if (r.TriggerModule == InternalMailEntityType && r.MailCategoryId is null)
            return Result<WorkflowDefinitionDto>.Fail("اختر تصنيف البريد الذي يُشغّل المسار");

        var def = new WorkflowDefinition
        {
            Name = r.Name,
            Description = r.Description,
            TriggerModule = r.TriggerModule,
            DocumentTypeId = r.DocumentTypeId,
            MailCategoryId = r.MailCategoryId,
            IsActive = r.IsActive,
        };
        foreach (var s in r.Stages.OrderBy(s => s.Order))
        {
            def.Stages.Add(new WorkflowStage
            {
                Name = s.Name,
                Order = s.Order,
                AssigneeType = s.AssigneeType,
                AssigneePositionId = s.AssigneePositionId,
                AssigneeOrgUnitId = s.AssigneeOrgUnitId,
                AssigneeUserId = s.AssigneeUserId,
                ResponseHours = s.ResponseHours <= 0 ? 24 : s.ResponseHours,
                EscalateAfterHours = s.EscalateAfterHours,
                EscalateToPositionId = s.EscalateToPositionId,
                AllowedActions = s.AllowedActions == WorkflowActionType.None
                    ? WorkflowActionType.Approve | WorkflowActionType.Reject | WorkflowActionType.Forward | WorkflowActionType.Comment
                    : s.AllowedActions,
                IsFinal = s.IsFinal,
                // Phase 7 routing
                Type = s.Type,
                ConditionExpression = s.ConditionExpression,
                ParallelGroupId = s.ParallelGroupId,
                RequiresAllParallelComplete = s.RequiresAllParallelComplete,
                AssignmentType = s.AssignmentType,
                AssignmentExpression = s.AssignmentExpression,
                RequiresCorrespondence = s.RequiresCorrespondence,
                RejectBehavior = s.RejectBehavior,
                ReturnResume = s.ReturnResume,
                MinimumApprovals = s.MinimumApprovals,
                Color = NormalizeColor(s.Color),
            });
        }

        db.WorkflowDefinitions.Add(def);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "WorkflowDefinition", def.Id, def.Name, ct: ct);
        return Result<WorkflowDefinitionDto>.Ok(ToDefinitionDto(def));
    }

    // ---- Instances ----

    public async Task<Result<WorkflowInstanceDto>> StartAsync(StartWorkflowRequest r, CancellationToken ct = default)
    {
        var def = await db.WorkflowDefinitions.Include(d => d.Stages)
            .FirstOrDefaultAsync(d => d.Id == r.WorkflowDefinitionId && d.IsActive, ct);
        if (def is null) return Result<WorkflowInstanceDto>.Fail("تعريف سير العمل غير موجود أو غير مفعّل");
        if (def.Stages.Count == 0) return Result<WorkflowInstanceDto>.Fail("التعريف لا يحتوي على مراحل");

        var instance = new WorkflowInstance
        {
            WorkflowDefinitionId = def.Id,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            Status = WorkflowStatus.Running,
            StartedAt = DateTime.UtcNow,
            InitiatedByUserId = currentUser.UserId,
            // A mail-driven run keeps the correspondence that raised it: every message the engine
            // generates threads under it, so the request and its whole approval chain stay one
            // conversation in المراسلات.
            RootInternalMailId = r.EntityType == InternalMailEntityType ? r.EntityId : null,
        };
        db.WorkflowInstances.Add(instance);
        await db.SaveChangesAsync(ct);

        var firstStage = def.Stages.OrderBy(s => s.Order).First();
        await OpenTaskForStageAsync(instance, firstStage, ct);
        await db.SaveChangesAsync(ct);
        await FlushStepMailsAsync(instance.Id, ct);

        await audit.WriteAsync("WorkflowStarted", r.EntityType, r.EntityId, def.Name, ct: ct);
        return await LoadInstanceResultAsync(instance.Id, ct);
    }

    public async Task<Result<WorkflowInstanceDto>> GetInstanceAsync(long id, CancellationToken ct = default)
        => await LoadInstanceResultAsync(id, ct);

    // ---- Internal-mail integration ----

    /// <summary>The trigger: «IF internal mail is created AND تصنيف البريد = X THEN start workflow Y».
    /// Called by the mail service right after a message is composed. Silent when nothing is configured
    /// for that category — most correspondence is not a request.
    ///
    /// Two routes bound to the same category resolve by version, then by id, so an institution that
    /// configures overlapping routes still gets the same run every time.</summary>
    public async Task<WorkflowInstanceDto?> StartForInternalMailAsync(long internalMailId, long? mailCategoryId,
        CancellationToken ct = default)
    {
        if (mailCategoryId is not { } categoryId) return null;

        var defId = await db.WorkflowDefinitions
            .Where(d => d.IsActive && d.TriggerModule == InternalMailEntityType && d.MailCategoryId == categoryId)
            .OrderByDescending(d => d.Version)      // newest published version of the route
            .ThenByDescending(d => d.Id)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (defId is not { } definitionId) return null;

        // One run per message: a resubmission resumes the existing instance rather than opening a second.
        if (await db.WorkflowInstances.AnyAsync(i => i.RootInternalMailId == internalMailId, ct)) return null;

        var started = await StartAsync(
            new StartWorkflowRequest(definitionId, InternalMailEntityType, internalMailId), ct);
        if (!started.Succeeded) return null;

        // Stamp the request itself, so opening it shows the run — and so the inbox badges it without
        // having to join through the instance table on every row.
        var root = await db.InternalMails.FirstOrDefaultAsync(m => m.Id == internalMailId, ct);
        if (root is not null)
        {
            root.WorkflowInstanceId = started.Value!.Id;
            root.WorkflowRole = WorkflowMailRole.Origin;
            await db.SaveChangesAsync(ct);
        }
        return started.Value;
    }

    /// <summary>Workflow columns for one page of the inbox, in three queries rather than per row.</summary>
    public async Task<IReadOnlyDictionary<long, WorkflowMailBadge>> BadgesForMailsAsync(
        IReadOnlyCollection<long> internalMailIds, CancellationToken ct = default)
    {
        var empty = (IReadOnlyDictionary<long, WorkflowMailBadge>)new Dictionary<long, WorkflowMailBadge>();
        if (internalMailIds.Count == 0) return empty;

        var mails = await db.InternalMails.IgnoreQueryFilters()
            .Where(m => internalMailIds.Contains(m.Id) && m.WorkflowInstanceId != null)
            .Select(m => new { m.Id, InstanceId = m.WorkflowInstanceId!.Value, m.WorkflowRole })
            .ToListAsync(ct);
        if (mails.Count == 0) return empty;

        var instanceIds = mails.Select(m => m.InstanceId).Distinct().ToList();
        var instances = await db.WorkflowInstances
            .Where(i => instanceIds.Contains(i.Id))
            .Select(i => new
            {
                i.Id,
                i.WorkflowDefinitionId,
                DefinitionName = i.WorkflowDefinition.Name,
                i.Status,
                CurrentStageName = i.CurrentStage != null ? i.CurrentStage.Name : null,
                CurrentOrder = i.CurrentStage != null ? (int?)i.CurrentStage.Order : null,
                TotalSteps = i.WorkflowDefinition.Stages.Count,
            })
            .ToDictionaryAsync(i => i.Id, ct);

        // Which of these runs is waiting on the reader — one query for the page, not one per row.
        var myPositions = await MyPositionIdsAsync(ct);
        var me = currentUser.UserId;
        var open = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Overdue, WorkflowTaskStatus.Escalated };
        var myTasks = await db.WorkflowTasks
            .Where(t => instanceIds.Contains(t.WorkflowInstanceId) && open.Contains(t.Status))
            .Where(t => (t.AssignedToPositionId != null && myPositions.Contains(t.AssignedToPositionId!.Value))
                     || (t.AssignedToUserId != null && t.AssignedToUserId == me))
            .Select(t => new { t.WorkflowInstanceId, t.Id })
            .ToListAsync(ct);
        var taskByInstance = myTasks
            .GroupBy(t => t.WorkflowInstanceId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var result = new Dictionary<long, WorkflowMailBadge>();
        foreach (var m in mails)
        {
            if (!instances.TryGetValue(m.InstanceId, out var i)) continue;
            // Order is the admin's own numbering and need not start at 1, so the step shown is the
            // stage's rank among its siblings rather than the raw Order value.
            var step = i.CurrentOrder is null ? i.TotalSteps
                : await db.WorkflowStages.CountAsync(x => x.WorkflowDefinitionId == i.WorkflowDefinitionId
                    && x.Order <= i.CurrentOrder, ct);
            taskByInstance.TryGetValue(m.InstanceId, out var myTaskId);
            result[m.Id] = new WorkflowMailBadge(
                i.Id, i.DefinitionName, i.Status.ToString(), i.CurrentStageName,
                step, i.TotalSteps, myTaskId != 0, myTaskId == 0 ? null : myTaskId,
                m.WorkflowRole?.ToString());
        }
        return result;
    }

    /// <summary>Resubmission after «إعادة للتعديل»: the same instance resumes, either at the stage that
    /// asked for the changes or from the beginning, as that stage was configured.</summary>
    public async Task<Result<WorkflowInstanceDto>> ResubmitAsync(long instanceId, string? note,
        CancellationToken ct = default)
    {
        var instance = await db.WorkflowInstances
            .Include(i => i.WorkflowDefinition).ThenInclude(d => d.Stages)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance is null) return Result<WorkflowInstanceDto>.Fail("سير العمل غير موجود");
        if (instance.Status != WorkflowStatus.ChangesRequested)
            return Result<WorkflowInstanceDto>.Fail("سير العمل ليس بانتظار تعديل");
        if (instance.InitiatedByUserId != currentUser.UserId)
            return Result<WorkflowInstanceDto>.Fail("إعادة الإرسال من حق مُنشئ الطلب وحده");

        var ordered = instance.WorkflowDefinition.Stages.OrderBy(s => s.Order).ToList();
        if (ordered.Count == 0) return Result<WorkflowInstanceDto>.Fail("التعريف لا يحتوي على مراحل");

        var askedBy = ordered.FirstOrDefault(x => x.Id == instance.ChangesRequestedByStageId);
        var resume = askedBy is null || askedBy.ReturnResume == ReturnResumeMode.RestartFromStart
            ? ordered[0]
            : askedBy;

        instance.Status = WorkflowStatus.Running;
        instance.ChangesRequestedByStageId = null;
        await OpenTaskForStageAsync(instance, resume, ct);
        await db.SaveChangesAsync(ct);
        await FlushStepMailsAsync(instance.Id, ct);

        await audit.WriteAsync("Resubmitted", instance.EntityType, instance.EntityId,
            instance.WorkflowDefinition.Name, newValues: note, ct: ct);
        return await LoadInstanceResultAsync(instance.Id, ct);
    }

    /// <summary>What the correspondence view needs to render its workflow strip: the run, whether this
    /// caller owes it an action, and the timeline.</summary>
    public async Task<WorkflowMailContextDto?> ContextForInternalMailAsync(long internalMailId,
        CancellationToken ct = default)
    {
        var mail = await db.InternalMails.IgnoreQueryFilters()
            .Where(m => m.Id == internalMailId)
            .Select(m => new { m.Id, m.WorkflowInstanceId, m.WorkflowTaskId, m.WorkflowRole, m.ParentId })
            .FirstOrDefaultAsync(ct);
        if (mail is null) return null;

        // A generated message carries its instance directly; an ordinary reply inside the thread does
        // not, so the thread root is asked instead.
        var instanceId = mail.WorkflowInstanceId
            ?? await db.WorkflowInstances.Where(i => i.RootInternalMailId == (mail.ParentId ?? mail.Id))
                .Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (instanceId is not { } iid) return null;

        var instance = await db.WorkflowInstances
            .Include(i => i.WorkflowDefinition).ThenInclude(d => d.Stages)
            .Include(i => i.CurrentStage)
            .FirstOrDefaultAsync(i => i.Id == iid, ct);
        if (instance is null) return null;

        var ordered = instance.WorkflowDefinition.Stages.OrderBy(s => s.Order).ToList();
        var stepIndex = instance.CurrentStageId is { } cs ? ordered.FindIndex(x => x.Id == cs) + 1 : 0;

        // The caller's own open task on this run — position-based, so it follows the seat.
        var myPositions = await MyPositionIdsAsync(ct);
        var me = currentUser.UserId;
        var open = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Overdue, WorkflowTaskStatus.Escalated };
        var mine = await db.WorkflowTasks.Include(t => t.Stage)
            .Where(t => t.WorkflowInstanceId == iid && open.Contains(t.Status))
            .Where(t => (t.AssignedToPositionId != null && myPositions.Contains(t.AssignedToPositionId!.Value))
                     || (t.AssignedToUserId != null && t.AssignedToUserId == me))
            .OrderBy(t => t.DueAt)
            .FirstOrDefaultAsync(ct);

        return new WorkflowMailContextDto(
            instance.Id, instance.WorkflowDefinitionId, instance.WorkflowDefinition.Name,
            instance.Status.ToString(), instance.RootInternalMailId ?? instance.EntityId,
            instance.CurrentStage?.Name, stepIndex < 1 ? ordered.Count : stepIndex, ordered.Count,
            mail.WorkflowRole?.ToString(),
            mine is not null, mine?.Id, mine?.Stage.AllowedActions.ToString(), mine?.DueAt,
            instance.Status == WorkflowStatus.ChangesRequested && instance.InitiatedByUserId == me,
            await TimelineAsync(iid, ct));
    }

    /// <summary>The run narrated in order: when it was submitted, who it went to, what each of them did,
    /// and how it ended. Built from the tasks themselves, so it cannot disagree with the run's state.</summary>
    private async Task<IReadOnlyList<WorkflowTimelineEntry>> TimelineAsync(long instanceId, CancellationToken ct)
    {
        var instance = await db.WorkflowInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance is null) return [];

        var tasks = await db.WorkflowTasks.AsNoTracking().Include(t => t.Stage)
            .Where(t => t.WorkflowInstanceId == instanceId)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(ct);

        // Assignees as well as actors: a "Sent" entry names who it went to, and without them the
        // timeline reads «أُرسلت إلى —».
        var userIds = tasks.Select(t => t.CompletedByUserId)
            .Concat(tasks.Select(t => t.AssignedToUserId))
            .Concat([instance.InitiatedByUserId])
            .Where(u => u is not null).Select(u => u!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(long? id) => id is { } v && names.TryGetValue(v, out var n) ? n : null;

        var entries = new List<WorkflowTimelineEntry>
        {
            new(instance.StartedAt, "Submitted", "—", NameOf(instance.InitiatedByUserId), null,
                instance.RootInternalMailId),
        };

        foreach (var t in tasks)
        {
            entries.Add(new WorkflowTimelineEntry(t.CreatedAt, "Sent", t.Stage.Name,
                NameOf(t.AssignedToUserId), null, t.InternalMailId));

            if (t.CompletedAt is not { } done) continue;
            var kind = t.ActionTaken switch
            {
                WorkflowActionType.Approve or WorkflowActionType.Close => "Approved",
                WorkflowActionType.Reject => "Rejected",
                WorkflowActionType.RequestChanges => "ChangesRequested",
                WorkflowActionType.Return => "Returned",
                _ => t.ActionTaken.ToString(),
            };
            entries.Add(new WorkflowTimelineEntry(done, kind, t.Stage.Name,
                NameOf(t.CompletedByUserId), t.Note, t.InternalMailId));
        }

        if (instance.CompletedAt is { } end)
            entries.Add(new WorkflowTimelineEntry(end,
                instance.Status == WorkflowStatus.Rejected ? "Rejected" : "Completed",
                "—", null, null, null));

        return entries.OrderBy(e => e.At).ToList();
    }

    // ---- Worklist ----

    public async Task<IReadOnlyList<WorklistItem>> MyWorklistAsync(CancellationToken ct = default)
    {
        var myPositions = await MyPositionIdsAsync(ct);
        var me = currentUser.UserId;
        var now = DateTime.UtcNow;

        var open = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Overdue, WorkflowTaskStatus.Escalated };

        return await db.WorkflowTasks
            .Include(t => t.Stage)
            .Include(t => t.WorkflowInstance).ThenInclude(i => i.WorkflowDefinition)
            .Where(t => open.Contains(t.Status))
            .Where(t => (t.AssignedToPositionId != null && myPositions.Contains(t.AssignedToPositionId!.Value))
                     || (t.AssignedToUserId != null && t.AssignedToUserId == me))
            .OrderBy(t => t.DueAt)
            .Select(t => new WorklistItem(
                t.Id, t.WorkflowInstanceId,
                t.WorkflowInstance.EntityType, t.WorkflowInstance.EntityId,
                t.WorkflowInstance.WorkflowDefinition.Name, t.Stage.Name,
                t.Status.ToString(), t.DueAt, t.DueAt < now,
                t.Stage.AllowedActions.ToString()))
            .ToListAsync(ct);
    }

    public async Task<Result<WorkflowInstanceDto>> ActOnTaskAsync(long taskId, WorkflowActionRequest r, CancellationToken ct = default)
    {
        var task = await db.WorkflowTasks
            .Include(t => t.Stage)
            .Include(t => t.WorkflowInstance).ThenInclude(i => i.WorkflowDefinition).ThenInclude(d => d.Stages)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return Result<WorkflowInstanceDto>.Fail("المهمة غير موجودة");

        if (task.Status is WorkflowTaskStatus.Completed or WorkflowTaskStatus.Reassigned)
            return Result<WorkflowInstanceDto>.Fail("تم إغلاق هذه المهمة مسبقًا");

        if (!await CallerOwnsTaskAsync(task, ct))
            return Result<WorkflowInstanceDto>.Fail("هذه المهمة ليست ضمن قائمة أعمالك");

        if ((task.Stage.AllowedActions & r.Action) == 0)
            return Result<WorkflowInstanceDto>.Fail("هذا الإجراء غير مسموح في هذه المرحلة");

        // A rejection or a return without a reason tells the requester nothing and leaves the audit
        // trail unable to answer why the request died. Required for both, at every stage.
        if (r.Action is WorkflowActionType.Reject && string.IsNullOrWhiteSpace(r.Note))
            return Result<WorkflowInstanceDto>.Fail("سبب الرفض مطلوب");
        if (r.Action is WorkflowActionType.RequestChanges && string.IsNullOrWhiteSpace(r.Note))
            return Result<WorkflowInstanceDto>.Fail("سبب الإعادة للتعديل مطلوب");

        // A letterhead edit may not be cleared by the person who made it. The generic engine has
        // no opinion on self-approval — for most modules the assignee is a different seat by
        // construction — but an Organization Admin editing their own department's letterhead would
        // otherwise land the approval task on their own desk, making the review a formality.
        if (task.WorkflowInstance.EntityType == LetterheadService.WorkflowEntityType
            && r.Action is WorkflowActionType.Approve or WorkflowActionType.Close)
        {
            var submittedBy = await db.ReportTemplates
                .Where(t => t.Id == task.WorkflowInstance.EntityId)
                .Select(t => t.UpdatedBy ?? t.CreatedBy)
                .FirstOrDefaultAsync(ct);
            if (submittedBy is not null && submittedBy == currentUser.UserId)
                return Result<WorkflowInstanceDto>.Fail("لا يمكنك اعتماد تعديل ترويسة قمت به بنفسك");
        }

        var instance = task.WorkflowInstance;
        var def = instance.WorkflowDefinition;

        // ── Scan-to-archive integration: this Document instance carries archive side-effects ──
        var archiveDoc = instance.EntityType == "Document"
            ? await db.Documents.FirstOrDefaultAsync(d => d.Id == instance.EntityId && d.ArchiveWorkflowInstanceId == instance.Id, ct)
            : null;
        if (archiveDoc is not null)
        {
            // A box must be assigned before the final filing step can close.
            if (r.Action == WorkflowActionType.Close && archiveDoc.BoxId is null)
                return Result<WorkflowInstanceDto>.Fail("يجب تخصيص صندوق فيزيائي للوثيقة قبل إنهاء الأرشفة");

            // Return → send the document back to its uploader and cancel the run (re-sendable cleanly).
            if (r.Action == WorkflowActionType.Return)
            {
                if (string.IsNullOrWhiteSpace(r.Note)) return Result<WorkflowInstanceDto>.Fail("سبب الإرجاع مطلوب");
                task.Status = WorkflowTaskStatus.Completed; task.ActionTaken = r.Action; task.Note = r.Note;
                task.CompletedAt = DateTime.UtcNow; task.CompletedByUserId = currentUser.UserId;
                instance.Status = WorkflowStatus.Cancelled; instance.CompletedAt = DateTime.UtcNow;
                instance.CurrentStageId = null; instance.CurrentAssigneePositionId = null;
                archiveDoc.Status = DocumentStatus.PendingArchive;
                archiveDoc.ArchiveReturnReason = r.Note;
                archiveDoc.ArchiveWorkflowInstanceId = null;
                NotifyUser(archiveDoc.CreatedBy, "أُعيدت وثيقتك من قسم الأرشيف", $"السبب: {r.Note} — يُرجى التعديل وإعادة الإرسال.", archiveDoc.Id);
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync("ArchiveReturned", "Document", archiveDoc.Id, archiveDoc.Title, newValues: r.Note, ct: ct);
                return await LoadInstanceResultAsync(instance.Id, ct);
            }
        }

        // Comment / Hold do not complete the task — they annotate or pause it.
        if (r.Action == WorkflowActionType.Comment)
        {
            await audit.WriteAsync("Comment", instance.EntityType, instance.EntityId, def.Name, newValues: r.Note, ct: ct);
            return await LoadInstanceResultAsync(instance.Id, ct);
        }
        if (r.Action == WorkflowActionType.Hold)
        {
            task.Status = WorkflowTaskStatus.InProgress;
            task.Note = r.Note;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Held", instance.EntityType, instance.EntityId, def.Name, newValues: r.Note, ct: ct);
            return await LoadInstanceResultAsync(instance.Id, ct);
        }

        // Complete the current task (immutable routing history).
        task.Status = WorkflowTaskStatus.Completed;
        task.ActionTaken = r.Action;
        task.Note = r.Note;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedByUserId = currentUser.UserId;

        var orderedStages = def.Stages.OrderBy(s => s.Order).ToList();
        var currentIndex = orderedStages.FindIndex(s => s.Id == task.StageId);

        var eventDocumentId = instance.EntityType == "Document" ? instance.EntityId : (long?)null;
        instance.AddDomainEvent(new WorkflowStageCompletedEvent(
            instance.Id, task.StageId, r.Action.ToString(), task.AssignedToPositionId ?? 0)
        {
            TriggeredByUserId = currentUser.UserId?.ToString(),
        });

        switch (r.Action)
        {
            case WorkflowActionType.Reject
                when task.Stage.RejectBehavior == RejectBehavior.ReturnToPreviousStage && currentIndex > 0:
                // Bounced one desk back rather than killed: the previous approver corrects and re-approves,
                // and the run keeps its history. Configured per stage — see RejectBehavior.
                await OpenTaskForStageAsync(instance, orderedStages[currentIndex - 1], ct);
                break;

            case WorkflowActionType.Reject:
                instance.Status = WorkflowStatus.Rejected;
                instance.CompletedAt = DateTime.UtcNow;
                instance.CurrentStageId = null;
                instance.CurrentAssigneePositionId = null;
                instance.AddDomainEvent(new WorkflowRejectedEvent(instance.Id, eventDocumentId, r.Note)
                {
                    TriggeredByUserId = currentUser.UserId?.ToString(),
                });
                break;

            case WorkflowActionType.RequestChanges:
                // Parked on the requester. Nothing already approved is undone: the instance survives,
                // and ReturnResume decides where a resubmission picks up.
                instance.Status = WorkflowStatus.ChangesRequested;
                instance.ChangesRequestedByStageId = task.StageId;
                instance.CurrentStageId = null;
                instance.CurrentAssigneePositionId = null;
                break;

            case WorkflowActionType.Close:
                instance.Status = WorkflowStatus.Completed;
                instance.CompletedAt = DateTime.UtcNow;
                instance.CurrentStageId = null;
                instance.CurrentAssigneePositionId = null;
                break;

            case WorkflowActionType.Return:
                // Send back to the previous stage (if any), otherwise stay.
                if (currentIndex > 0)
                    await OpenTaskForStageAsync(instance, orderedStages[currentIndex - 1], ct);
                break;

            case WorkflowActionType.Forward when r.ForwardToPositionId is { } pos:
                // Explicit hand-off to another seat within the same stage.
                await OpenTaskForPositionAsync(instance, task.Stage, pos, ct);
                break;

            case WorkflowActionType.Approve:
            case WorkflowActionType.Forward:
                // Advance with conditional-skip + parallel-join handling (Phase 7).
                await AdvanceAfterApprovalAsync(instance, orderedStages, task, eventDocumentId, ct);
                break;
        }

        await db.SaveChangesAsync(ct);

        // Correspondence is generated only now, against committed state: the next approver's message
        // and the requester's result can never describe an advance that did not happen.
        await FlushStepMailsAsync(instance.Id, ct);
        await SendOutcomeMailAsync(instance, r.Note, ct);

        await audit.WriteAsync(r.Action.ToString(), instance.EntityType, instance.EntityId, def.Name, newValues: r.Note, ct: ct);

        // Archive-intake completed → mark the document Archived, notify the uploader, fire correspondence.
        if (archiveDoc is not null && instance.Status == WorkflowStatus.Completed)
        {
            archiveDoc.Status = DocumentStatus.Archived;
            archiveDoc.ArchiveWorkflowInstanceId = null;
            archiveDoc.ArchiveReturnReason = null;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Archived", "Document", archiveDoc.Id, archiveDoc.Title, ct: ct);
            NotifyUser(archiveDoc.CreatedBy, "تمت أرشفة وثيقتك", "اطّلع على الموقع الفيزيائي في تفاصيل الوثيقة.", archiveDoc.Id);
            await db.SaveChangesAsync(ct);
        }

        return await LoadInstanceResultAsync(instance.Id, ct);
    }

    /// <summary>Queue an in-app notification for a single user (no-op if the user is unknown).</summary>
    private void NotifyUser(long? userId, string title, string body, long documentId)
    {
        if (userId is not { } uid) return;
        db.Notifications.Add(new Notification
        {
            RecipientUserId = uid, Title = title, Body = body,
            Type = NotificationType.Info, Channels = NotificationChannel.InApp,
            EntityType = "Document", EntityId = documentId,
        });
    }

    // ---- helpers ----

    private async Task<List<long>> MyPositionIdsAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (me is null) return [];
        // Own seats plus any positions currently delegated to me (Phase 5).
        return await delegations.GetEffectivePositionIdsForUserAsync(me.Value, ct);
    }

    private async Task<bool> CallerOwnsTaskAsync(WorkflowTask task, CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (task.AssignedToUserId == me && me is not null) return true;
        if (task.AssignedToPositionId is not { } pos) return false;
        return await db.Positions.AnyAsync(p => p.Id == pos && p.CurrentOccupantUserId == me, ct);
    }

    /// <summary>Resolves the stage's responsible seat + occupant and opens a task there.</summary>
    private async Task OpenTaskForStageAsync(WorkflowInstance instance, WorkflowStage stage, CancellationToken ct)
    {
        var (positionId, userId) = stage.AssignmentType == DynamicAssignmentType.Static
            ? await ResolveAssigneeAsync(stage, ct)
            : await ResolveDynamicAssigneeAsync(instance, stage, ct);
        await OpenTaskCoreAsync(instance, stage, positionId, userId, ct);
    }

    /// <summary>Phase 7 dynamic assignment: resolves the seat from the org hierarchy relative to the
    /// originator/document owner. Falls back to the stage's static config if it can't be resolved.</summary>
    private async Task<(long? PositionId, long? UserId)> ResolveDynamicAssigneeAsync(
        WorkflowInstance instance, WorkflowStage stage, CancellationToken ct)
    {
        var orgUnitId = await OriginatorOrgUnitAsync(instance, ct);
        long? position = null;

        switch (stage.AssignmentType)
        {
            case DynamicAssignmentType.DepartmentHead:
            case DynamicAssignmentType.DocumentOwnerDepartment:
                if (orgUnitId is { } ou)
                    position = await db.OrgUnits.Where(u => u.Id == ou).Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct);
                break;

            case DynamicAssignmentType.OrgHierarchy:
                var levels = ParseLevels(stage.AssignmentExpression);
                long? cursor = orgUnitId;
                for (var i = 0; i < levels && cursor is { } c; i++)
                    cursor = await db.OrgUnits.Where(u => u.Id == c).Select(u => u.ParentId).FirstOrDefaultAsync(ct);
                if (cursor is { } cc)
                    position = await db.OrgUnits.Where(u => u.Id == cc).Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct);
                break;
        }

        if (position is null) return await ResolveAssigneeAsync(stage, ct);   // fail-safe fallback
        return (position, await OccupantOfAsync(position.Value, ct));
    }

    private async Task<long?> OriginatorOrgUnitAsync(WorkflowInstance instance, CancellationToken ct)
    {
        if (instance.EntityType == "Document")
            return await db.Documents.IgnoreQueryFilters().Where(d => d.Id == instance.EntityId)
                .Select(d => (long?)d.OwningOrgUnitId).FirstOrDefaultAsync(ct);
        if (instance.InitiatedByUserId is { } u)
            return await db.Users.Where(x => x.Id == u).Select(x => x.OrgUnitId).FirstOrDefaultAsync(ct);
        return null;
    }

    private static int ParseLevels(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return 1;
        try
        {
            using var d = System.Text.Json.JsonDocument.Parse(expression);
            return d.RootElement.TryGetProperty("levels", out var l) && l.TryGetInt32(out var n) && n > 0 ? n : 1;
        }
        catch { return 1; }
    }

    private async Task OpenTaskForPositionAsync(WorkflowInstance instance, WorkflowStage stage, long positionId, CancellationToken ct)
    {
        var occupant = await db.Positions.Where(p => p.Id == positionId)
            .Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
        await OpenTaskCoreAsync(instance, stage, positionId, occupant, ct);
    }

    private async Task OpenTaskCoreAsync(WorkflowInstance instance, WorkflowStage stage, long? positionId, long? userId, CancellationToken ct)
    {
        var due = DateTime.UtcNow.AddHours(stage.ResponseHours);
        var task = new WorkflowTask
        {
            WorkflowInstanceId = instance.Id,
            StageId = stage.Id,
            AssignedToPositionId = positionId,
            AssignedToUserId = userId,
            Status = WorkflowTaskStatus.Pending,
            DueAt = due,
        };
        db.WorkflowTasks.Add(task);
        _pendingTaskObjects.Add(task);

        instance.CurrentStageId = stage.Id;
        instance.CurrentAssigneePositionId = positionId;
        instance.DueAt = due;

        if (userId is { } uid)
            await NotifyAsync(uid, $"مهمة جديدة: {stage.Name}",
                $"لديك معاملة بانتظار إجراءك في مرحلة \"{stage.Name}\".",
                instance.EntityType, instance.EntityId, ct);
    }

    /// <summary>Advances the run after an approval/forward: applies parallel joins, skips conditional stages
    /// whose condition is false, opens the next stage(s), or completes the run. Sequential stages with no
    /// parallel group behave exactly as before.</summary>
    private async Task AdvanceAfterApprovalAsync(
        WorkflowInstance instance, List<WorkflowStage> orderedStages, WorkflowTask completedTask,
        long? eventDocumentId, CancellationToken ct)
    {
        var completedStage = completedTask.Stage;
        var currentIndex = orderedStages.FindIndex(s => s.Id == completedTask.StageId);

        // --- Parallel join ---
        if (completedStage.ParallelGroupId is { } groupId)
        {
            var groupStages = orderedStages.Where(s => s.ParallelGroupId == groupId).ToList();
            var groupStageIds = groupStages.Select(s => s.Id).ToHashSet();

            // Quorum ("2 of 3") wins over the all/any flag when it is set, and is read from whichever
            // stage in the group carries it — an admin setting it on one stage means it for the group.
            var quorum = groupStages.Select(s => s.MinimumApprovals).FirstOrDefault(v => v is > 0);

            if (quorum is { } needed && needed < groupStages.Count)
            {
                var approvals = await db.WorkflowTasks.CountAsync(t => t.WorkflowInstanceId == instance.Id
                    && t.CompletedAt != null && groupStageIds.Contains(t.StageId)
                    && (t.ActionTaken == WorkflowActionType.Approve || t.ActionTaken == WorkflowActionType.Close), ct);
                if (approvals < needed) return;
                await CloseRemainingParallelAsync(instance.Id, groupStageIds,
                    $"(تم تجاوزها — اكتمل النصاب {needed} من {groupStages.Count})", ct);
            }
            else if (completedStage.RequiresAllParallelComplete)
            {
                // AND join: wait until every parallel task in the group is completed.
                var stillOpen = await db.WorkflowTasks.AnyAsync(t => t.WorkflowInstanceId == instance.Id
                    && t.CompletedAt == null && groupStageIds.Contains(t.StageId), ct);
                if (stillOpen) return;
            }
            else
            {
                // OR join: the first approval releases; the rest are closed as superseded.
                await CloseRemainingParallelAsync(instance.Id, groupStageIds,
                    "(تم تجاوزها — اكتمل مسار متوازٍ)", ct);
            }

            currentIndex = orderedStages.FindLastIndex(s => s.ParallelGroupId == groupId);
        }

        if (completedStage.IsFinal) { CompleteInstance(instance, eventDocumentId); return; }

        var context = await BuildConditionContextAsync(instance, completedTask, ct);
        var nextIndex = NextOpenableIndex(orderedStages, currentIndex, context);
        if (nextIndex < 0) { CompleteInstance(instance, eventDocumentId); return; }

        var nextStage = orderedStages[nextIndex];
        if (nextStage.ParallelGroupId is { } ng)
            foreach (var ps in orderedStages.Where(s => s.ParallelGroupId == ng))
                await OpenTaskForStageAsync(instance, ps, ct);
        else
            await OpenTaskForStageAsync(instance, nextStage, ct);
    }

    /// <summary>Closes the still-open tasks of a parallel group once the join has released, so they
    /// leave every worklist at the same moment the run moves on.</summary>
    private async Task CloseRemainingParallelAsync(long instanceId, HashSet<long> groupStageIds, string note,
        CancellationToken ct)
    {
        var remaining = await db.WorkflowTasks.Where(t => t.WorkflowInstanceId == instanceId
            && t.CompletedAt == null && groupStageIds.Contains(t.StageId)).ToListAsync(ct);
        foreach (var rem in remaining)
        {
            rem.Status = WorkflowTaskStatus.Completed;
            rem.CompletedAt = DateTime.UtcNow;
            rem.Note = note;
        }
    }

    /// <summary>Closes the loop with the requester when a run ends: approved, rejected, or returned for
    /// changes. A run still in flight sends nothing.</summary>
    private async Task SendOutcomeMailAsync(WorkflowInstance instance, string? reason, CancellationToken ct)
    {
        var outcome = instance.Status switch
        {
            WorkflowStatus.Completed => WorkflowOutcome.Approved,
            WorkflowStatus.Rejected => WorkflowOutcome.Rejected,
            WorkflowStatus.ChangesRequested => WorkflowOutcome.ChangesRequested,
            _ => (WorkflowOutcome?)null,
        };
        if (outcome is not { } o) return;
        await mailRouter.SendResultAsync(instance.Id, o, reason, ct);
    }

    private void CompleteInstance(WorkflowInstance instance, long? eventDocumentId)
    {
        instance.Status = WorkflowStatus.Completed;
        instance.CompletedAt = DateTime.UtcNow;
        instance.CurrentStageId = null;
        instance.CurrentAssigneePositionId = null;
        instance.AddDomainEvent(new WorkflowApprovedEvent(instance.Id, eventDocumentId)
        {
            TriggeredByUserId = currentUser.UserId?.ToString(),
        });
    }

    /// <summary>First index after <paramref name="fromIndex"/> whose stage should open — skipping Conditional
    /// stages whose condition evaluates false. Returns -1 when the run is finished.</summary>
    private static int NextOpenableIndex(List<WorkflowStage> stages, int fromIndex, IReadOnlyDictionary<string, string?> context)
    {
        for (var i = fromIndex + 1; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s.Type == StageType.Conditional && !WorkflowConditionEvaluator.Evaluate(s.ConditionExpression, context))
                continue;   // condition false → skip this stage
            return i;
        }
        return -1;
    }

    /// <summary>Builds the value context a conditional stage is evaluated against: the document's metadata,
    /// its custom-field values, and the last action taken.</summary>
    private async Task<IReadOnlyDictionary<string, string?>> BuildConditionContextAsync(
        WorkflowInstance instance, WorkflowTask lastTask, CancellationToken ct)
    {
        var ctx = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["action"] = lastTask.ActionTaken.ToString(),
        };
        if (instance.EntityType == "Document")
        {
            var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == instance.EntityId, ct);
            if (doc is not null)
            {
                ctx["Title"] = doc.Title;
                ctx["Status"] = doc.Status.ToString();
                ctx["Confidentiality"] = doc.Confidentiality.ToString();
                ctx["DocumentTypeId"] = doc.DocumentTypeId.ToString();
                var cfs = await db.CustomFieldValues
                    .Where(v => v.EntityType == "Document" && v.EntityId == doc.Id)
                    .Join(db.CustomFieldDefinitions, v => v.FieldId, d => d.Id, (v, d) => new { d.FieldKey, v.Value })
                    .ToListAsync(ct);
                foreach (var cf in cfs) ctx[cf.FieldKey] = cf.Value;
            }
        }
        return ctx;
    }

    /// <summary>Maps a stage's assignee configuration to a concrete (position, occupant-user) pair.</summary>
    private async Task<(long? PositionId, long? UserId)> ResolveAssigneeAsync(WorkflowStage stage, CancellationToken ct)
    {
        switch (stage.AssigneeType)
        {
            case StageAssigneeType.Position when stage.AssigneePositionId is { } pid:
                return (pid, await OccupantOfAsync(pid, ct));

            case StageAssigneeType.User when stage.AssigneeUserId is { } uid:
                return (null, uid);

            case StageAssigneeType.OrgUnit when stage.AssigneeOrgUnitId is { } ouId:
                // Route to the unit's manager seat, else any active position in the unit.
                var mgr = await db.OrgUnits.Where(u => u.Id == ouId).Select(u => u.ManagerPositionId).FirstOrDefaultAsync(ct)
                          ?? await db.Positions.Where(p => p.OrgUnitId == ouId && p.IsActive)
                              .OrderByDescending(p => p.Rank).Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);
                return (mgr, mgr is { } m ? await OccupantOfAsync(m, ct) : null);

            case StageAssigneeType.DirectManager when stage.EscalateToPositionId is { } esc:
                return (esc, await OccupantOfAsync(esc, ct));

            default:
                // Role-based or unconfigured: fall back to the explicit position/user if present.
                return (stage.AssigneePositionId,
                        stage.AssigneeUserId ?? (stage.AssigneePositionId is { } p ? await OccupantOfAsync(p, ct) : null));
        }
    }

    private async Task<long?> OccupantOfAsync(long positionId, CancellationToken ct) =>
        await db.Positions.Where(p => p.Id == positionId).Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);

    private async Task NotifyAsync(long userId, string title, string body, string entityType, long entityId, CancellationToken ct)
    {
        db.Notifications.Add(new Notification
        {
            RecipientUserId = userId,
            Title = title,
            Body = body,
            Type = NotificationType.Task,
            Channels = NotificationChannel.InApp,
            EntityType = entityType,
            EntityId = entityId,
        });
        await Task.CompletedTask;
    }

    private async Task<Result<WorkflowInstanceDto>> LoadInstanceResultAsync(long id, CancellationToken ct)
    {
        var instance = await db.WorkflowInstances
            .Include(i => i.WorkflowDefinition)
            .Include(i => i.Tasks).ThenInclude(t => t.Stage)
            .Include(i => i.CurrentStage)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return instance is null
            ? Result<WorkflowInstanceDto>.Fail("نسخة سير العمل غير موجودة")
            : Result<WorkflowInstanceDto>.Ok(ToInstanceDto(instance));
    }

    private static WorkflowDefinitionDto ToDefinitionDto(WorkflowDefinition d) => new(
        d.Id, d.Name, d.Description, d.TriggerModule, d.DocumentTypeId, d.MailCategoryId, d.Version, d.IsActive,
        d.Stages.OrderBy(s => s.Order).Select(s => new WorkflowStageDto(
            s.Id, s.Name, s.Order, s.AssigneeType.ToString(), s.AssigneePositionId, s.AssigneeOrgUnitId,
            s.AssigneeUserId, s.ResponseHours, s.EscalateAfterHours, s.EscalateToPositionId,
            s.AllowedActions.ToString(), s.IsFinal,
            s.Type.ToString(), s.ConditionExpression, s.ParallelGroupId, s.RequiresAllParallelComplete,
            s.AssignmentType.ToString(), s.AssignmentExpression, s.RequiresCorrespondence,
            s.RejectBehavior.ToString(), s.ReturnResume.ToString(), s.MinimumApprovals, s.Color)).ToList());

    /// <summary>Keeps the column a palette key: anything longer, or with a character no key can
    /// contain, is dropped back to the default tile rather than stored and rendered as nothing.</summary>
    private static string? NormalizeColor(string? color)
    {
        var key = color?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key) || key.Length > 16) return null;
        return key.All(c => c is >= 'a' and <= 'z') ? key : null;
    }

    private static WorkflowInstanceDto ToInstanceDto(WorkflowInstance i) => new(
        i.Id, i.WorkflowDefinitionId, i.WorkflowDefinition.Name, i.EntityType, i.EntityId,
        i.CurrentStageId, i.CurrentStage?.Name, i.CurrentAssigneePositionId, i.Status.ToString(),
        i.StartedAt, i.DueAt, i.CompletedAt,
        i.Tasks.OrderBy(t => t.Id).Select(t => new WorkflowTaskDto(
            t.Id, t.WorkflowInstanceId, t.StageId, t.Stage.Name, t.AssignedToPositionId, t.AssignedToUserId,
            t.Status.ToString(), t.DueAt, t.ActionTaken.ToString(), t.Note, t.CompletedAt, t.IsEscalated)).ToList());
}
