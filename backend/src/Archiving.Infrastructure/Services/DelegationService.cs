using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Delegation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class DelegationService(AppDbContext db, ICurrentUser currentUser, IAuditWriter audit) : IDelegationService
{
    public async Task<Result<DelegationDto>> CreateDelegationAsync(CreateDelegationDto dto, CancellationToken ct = default)
    {
        if (dto.FromPositionId == dto.ToPositionId)
            return Result<DelegationDto>.Fail("لا يمكن التفويض لنفس المنصب");
        if (dto.EndDate <= dto.StartDate)
            return Result<DelegationDto>.Fail("تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");

        var positions = await db.Positions.Where(p => p.Id == dto.FromPositionId || p.Id == dto.ToPositionId)
            .Select(p => p.Id).ToListAsync(ct);
        if (!positions.Contains(dto.FromPositionId) || !positions.Contains(dto.ToPositionId))
            return Result<DelegationDto>.Fail("أحد المناصب غير موجود");

        var entity = new PositionDelegation
        {
            FromPositionId = dto.FromPositionId,
            ToPositionId = dto.ToPositionId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Scope = dto.Scope,
            WorkflowDefinitionId = dto.WorkflowDefinitionId,
            Reason = dto.Reason,
            IsActive = true,
        };
        db.PositionDelegations.Add(entity);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DelegationCreated", "PositionDelegation", entity.Id,
            newValues: $"from={dto.FromPositionId};to={dto.ToPositionId}", ct: ct);

        return Result<DelegationDto>.Ok(await MapAsync(entity.Id, ct));
    }

    public async Task<Result<bool>> RevokeDelegationAsync(long delegationId, CancellationToken ct = default)
    {
        var d = await db.PositionDelegations.FirstOrDefaultAsync(x => x.Id == delegationId, ct);
        if (d is null) return Result<bool>.Fail("التفويض غير موجود");
        d.IsActive = false;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DelegationRevoked", "PositionDelegation", d.Id, ct: ct);
        return Result<bool>.Ok(true);
    }

    public Task<IReadOnlyList<DelegationDto>> GetDelegationsFromPositionAsync(long positionId, CancellationToken ct = default) =>
        QueryAsync(x => x.FromPositionId == positionId, ct);

    public Task<IReadOnlyList<DelegationDto>> GetDelegationsToPositionAsync(long positionId, CancellationToken ct = default) =>
        QueryAsync(x => x.ToPositionId == positionId, ct);

    public async Task<IReadOnlyList<DelegationDto>> GetMyDelegationsAsync(CancellationToken ct = default)
    {
        var me = currentUser.UserId;
        if (me is null) return [];
        var myPositions = await db.Positions.Where(p => p.CurrentOccupantUserId == me).Select(p => p.Id).ToListAsync(ct);
        return await QueryAsync(x => myPositions.Contains(x.FromPositionId) || myPositions.Contains(x.ToPositionId), ct);
    }

    public async Task<List<long>> GetEffectivePositionIdsForUserAsync(long userId, CancellationToken ct = default)
    {
        var own = await db.Positions.Where(p => p.CurrentOccupantUserId == userId).Select(p => p.Id).ToListAsync(ct);
        var delegated = await GetDelegatedPositionIdsForUserAsync(userId, ct);
        return own.Concat(delegated).Distinct().ToList();
    }

    public async Task<List<long>> GetDelegatedPositionIdsForUserAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var myPositions = await db.Positions.Where(p => p.CurrentOccupantUserId == userId).Select(p => p.Id).ToListAsync(ct);
        if (myPositions.Count == 0) return [];

        return await db.PositionDelegations
            .Where(x => x.IsActive && x.StartDate <= now && x.EndDate >= now && myPositions.Contains(x.ToPositionId))
            .Select(x => x.FromPositionId).Distinct().ToListAsync(ct);
    }

    private async Task<IReadOnlyList<DelegationDto>> QueryAsync(
        System.Linq.Expressions.Expression<Func<PositionDelegation, bool>> predicate, CancellationToken ct)
    {
        return await db.PositionDelegations.Where(predicate)
            .OrderByDescending(x => x.Id)
            .Select(x => new DelegationDto(
                x.Id, x.FromPositionId, x.FromPosition.Title, x.ToPositionId, x.ToPosition.Title,
                x.StartDate, x.EndDate, x.Scope.ToString(), x.WorkflowDefinitionId, x.IsActive, x.Reason))
            .ToListAsync(ct);
    }

    private async Task<DelegationDto> MapAsync(long id, CancellationToken ct) =>
        (await QueryAsync(x => x.Id == id, ct))[0];
}
