using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Escalation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class EscalationService(AppDbContext db, IAuditWriter audit) : IEscalationService
{
    public async Task<IReadOnlyList<EscalationRuleDto>> ListRulesAsync(CancellationToken ct = default) =>
        await db.EscalationRules.OrderBy(r => r.WorkflowDefinitionId).ThenBy(r => r.EscalationLevel)
            .Select(r => Map(r)).ToListAsync(ct);

    public async Task<Result<EscalationRuleDto>> CreateRuleAsync(CreateEscalationRuleDto dto, CancellationToken ct = default)
    {
        if (dto.EscalationLevel < 1) return Result<EscalationRuleDto>.Fail("مستوى التصعيد يجب أن يكون 1 أو أكثر");
        if (dto.OverdueMinutes < 0) return Result<EscalationRuleDto>.Fail("دقائق التأخير غير صالحة");

        var e = new EscalationRule
        {
            WorkflowDefinitionId = dto.WorkflowDefinitionId,
            WorkflowStageId = dto.WorkflowStageId,
            EscalationLevel = dto.EscalationLevel,
            OverdueMinutes = dto.OverdueMinutes,
            Action = dto.Action,
            EscalateToPositionId = dto.EscalateToPositionId,
            NotifyOriginator = dto.NotifyOriginator,
            IsActive = true,
        };
        db.EscalationRules.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("EscalationRuleCreated", "EscalationRule", e.Id, ct: ct);
        return Result<EscalationRuleDto>.Ok(Map(e));
    }

    public async Task<Result<EscalationRuleDto>> UpdateRuleAsync(long id, CreateEscalationRuleDto dto, CancellationToken ct = default)
    {
        var e = await db.EscalationRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<EscalationRuleDto>.Fail("القاعدة غير موجودة");

        e.WorkflowDefinitionId = dto.WorkflowDefinitionId;
        e.WorkflowStageId = dto.WorkflowStageId;
        e.EscalationLevel = dto.EscalationLevel;
        e.OverdueMinutes = dto.OverdueMinutes;
        e.Action = dto.Action;
        e.EscalateToPositionId = dto.EscalateToPositionId;
        e.NotifyOriginator = dto.NotifyOriginator;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("EscalationRuleUpdated", "EscalationRule", e.Id, ct: ct);
        return Result<EscalationRuleDto>.Ok(Map(e));
    }

    public async Task<Result<bool>> DeleteRuleAsync(long id, CancellationToken ct = default)
    {
        var e = await db.EscalationRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail("القاعدة غير موجودة");
        db.EscalationRules.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("EscalationRuleDeleted", "EscalationRule", id, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<EscalationLogDto>> GetLogAsync(
        long? workflowDefinitionId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = db.EscalationLogs.AsQueryable();
        if (workflowDefinitionId is { } defId)
            q = q.Where(l => db.WorkflowInstances.Any(w => w.Id == l.WorkflowInstanceId && w.WorkflowDefinitionId == defId));
        if (from is { } f) q = q.Where(l => l.EscalatedAt >= f);
        if (to is { } t) q = q.Where(l => l.EscalatedAt <= t);

        return await q.OrderByDescending(l => l.EscalatedAt)
            .Select(l => new EscalationLogDto(l.Id, l.WorkflowInstanceId, l.WorkflowStageId, l.EscalationLevel,
                l.EscalatedToPositionId, l.ActionTaken.ToString(), l.EscalatedAt))
            .ToListAsync(ct);
    }

    private static EscalationRuleDto Map(EscalationRule r) => new(
        r.Id, r.WorkflowDefinitionId, r.WorkflowStageId, r.EscalationLevel, r.OverdueMinutes,
        r.Action.ToString(), r.EscalateToPositionId, r.NotifyOriginator, r.IsActive);
}
