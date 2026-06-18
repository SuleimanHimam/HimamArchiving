using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>The routing engine: admin-defined definitions run as instances whose open tasks form worklists.
/// Tasks bind to a <b>position</b> (the seat) so work follows occupancy, not the person.</summary>
public sealed class WorkflowService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : IWorkflowService
{
    private const string EntityType = "Workflow";

    // ---- Definitions ----

    public async Task<IReadOnlyList<WorkflowDefinitionListItem>> ListDefinitionsAsync(CancellationToken ct = default) =>
        await db.WorkflowDefinitions.OrderBy(d => d.Name)
            .Select(d => new WorkflowDefinitionListItem(
                d.Id, d.Name, d.TriggerModule, d.Version, d.IsActive, d.Stages.Count))
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

        var def = new WorkflowDefinition
        {
            Name = r.Name,
            Description = r.Description,
            TriggerModule = r.TriggerModule,
            DocumentTypeId = r.DocumentTypeId,
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
        };
        db.WorkflowInstances.Add(instance);
        await db.SaveChangesAsync(ct);

        var firstStage = def.Stages.OrderBy(s => s.Order).First();
        await OpenTaskForStageAsync(instance, firstStage, ct);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("WorkflowStarted", r.EntityType, r.EntityId, def.Name, ct: ct);
        return await LoadInstanceResultAsync(instance.Id, ct);
    }

    public async Task<Result<WorkflowInstanceDto>> GetInstanceAsync(long id, CancellationToken ct = default)
        => await LoadInstanceResultAsync(id, ct);

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

        var instance = task.WorkflowInstance;
        var def = instance.WorkflowDefinition;

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

        switch (r.Action)
        {
            case WorkflowActionType.Reject:
                instance.Status = WorkflowStatus.Rejected;
                instance.CompletedAt = DateTime.UtcNow;
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
                // Advance to the next stage; if the current stage is final or last, complete the run.
                if (task.Stage.IsFinal || currentIndex == orderedStages.Count - 1)
                {
                    instance.Status = WorkflowStatus.Completed;
                    instance.CompletedAt = DateTime.UtcNow;
                    instance.CurrentStageId = null;
                    instance.CurrentAssigneePositionId = null;
                }
                else
                {
                    await OpenTaskForStageAsync(instance, orderedStages[currentIndex + 1], ct);
                }
                break;
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(r.Action.ToString(), instance.EntityType, instance.EntityId, def.Name, newValues: r.Note, ct: ct);
        return await LoadInstanceResultAsync(instance.Id, ct);
    }

    // ---- helpers ----

    private async Task<List<long>> MyPositionIdsAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (me is null) return [];
        return await db.Positions.Where(p => p.CurrentOccupantUserId == me).Select(p => p.Id).ToListAsync(ct);
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
        var (positionId, userId) = await ResolveAssigneeAsync(stage, ct);
        await OpenTaskCoreAsync(instance, stage, positionId, userId, ct);
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
        db.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = instance.Id,
            StageId = stage.Id,
            AssignedToPositionId = positionId,
            AssignedToUserId = userId,
            Status = WorkflowTaskStatus.Pending,
            DueAt = due,
        });

        instance.CurrentStageId = stage.Id;
        instance.CurrentAssigneePositionId = positionId;
        instance.DueAt = due;

        if (userId is { } uid)
            await NotifyAsync(uid, $"مهمة جديدة: {stage.Name}",
                $"لديك معاملة بانتظار إجراءك في مرحلة \"{stage.Name}\".",
                instance.EntityType, instance.EntityId, ct);
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
        d.Id, d.Name, d.Description, d.TriggerModule, d.DocumentTypeId, d.Version, d.IsActive,
        d.Stages.OrderBy(s => s.Order).Select(s => new WorkflowStageDto(
            s.Id, s.Name, s.Order, s.AssigneeType.ToString(), s.AssigneePositionId, s.AssigneeOrgUnitId,
            s.AssigneeUserId, s.ResponseHours, s.EscalateAfterHours, s.EscalateToPositionId,
            s.AllowedActions.ToString(), s.IsFinal)).ToList());

    private static WorkflowInstanceDto ToInstanceDto(WorkflowInstance i) => new(
        i.Id, i.WorkflowDefinitionId, i.WorkflowDefinition.Name, i.EntityType, i.EntityId,
        i.CurrentStageId, i.CurrentStage?.Name, i.CurrentAssigneePositionId, i.Status.ToString(),
        i.StartedAt, i.DueAt, i.CompletedAt,
        i.Tasks.OrderBy(t => t.Id).Select(t => new WorkflowTaskDto(
            t.Id, t.WorkflowInstanceId, t.StageId, t.Stage.Name, t.AssignedToPositionId, t.AssignedToUserId,
            t.Status.ToString(), t.DueAt, t.ActionTaken.ToString(), t.Note, t.CompletedAt, t.IsEscalated)).ToList());
}
