using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>Personnel action types: the HR administrator names them and picks their approval path.</summary>
public sealed class ActionTypeService(AppDbContext db, IAuditWriter audit) : IActionTypeService
{
    private const string NotFound = "نوع الحركة غير موجود";

    public async Task<IReadOnlyList<ActionTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.PersonnelActionTypes.AsNoTracking();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return await ToDtosAsync(await q.OrderBy(t => t.Effect).ThenBy(t => t.Name).ToListAsync(ct), ct);
    }

    public async Task<Result<ActionTypeDto>> CreateAsync(SaveActionTypeRequest r, CancellationToken ct = default)
    {
        var (f, error) = await ValidateAsync(r, null, ct);
        if (f is null) return Result<ActionTypeDto>.Fail(error!);

        var e = new PersonnelActionType();
        Apply(e, f);
        db.PersonnelActionTypes.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PersonnelActionType", e.Id, e.Name, ct: ct);
        return Result<ActionTypeDto>.Ok((await ToDtosAsync([e], ct))[0]);
    }

    public async Task<Result<ActionTypeDto>> UpdateAsync(long id, SaveActionTypeRequest r, CancellationToken ct = default)
    {
        var e = await db.PersonnelActionTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<ActionTypeDto>.Fail(NotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<ActionTypeDto>.Fail(CoreGuards.Stale);

        var (f, error) = await ValidateAsync(r, id, ct);
        if (f is null) return Result<ActionTypeDto>.Fail(error!);
        // The effect decides what existing actions of this type did; changing it would rewrite history.
        if (f.Effect != e.Effect && await db.PersonnelActions.AnyAsync(a => a.ActionTypeId == id, ct))
            return Result<ActionTypeDto>.Fail("لا يمكن تغيير أثر نوع حركة استُخدم مسبقاً");

        var before = CoreGuards.Json((await ToDtosAsync([e], ct))[0]);
        Apply(e, f);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ActionTypeDto>.Fail(CoreGuards.Stale); }
        var dto = (await ToDtosAsync([e], ct))[0];
        await audit.WriteAsync("Updated", "PersonnelActionType", e.Id, e.Name, before, CoreGuards.Json(dto), ct);
        return Result<ActionTypeDto>.Ok(dto);
    }

    private sealed record Fields(string Code, string Name, PersonnelActionEffect Effect, long? WorkflowDefinitionId, bool IsActive);

    private static void Apply(PersonnelActionType e, Fields f)
    {
        e.Code = f.Code;
        e.Name = f.Name;
        e.Effect = f.Effect;
        e.WorkflowDefinitionId = f.WorkflowDefinitionId;
        e.IsActive = f.IsActive;
    }

    private async Task<(Fields?, string?)> ValidateAsync(SaveActionTypeRequest r, long? existingId, CancellationToken ct)
    {
        var code = CoreGuards.Clean(r.Code);
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32) return (null, "الرمز مطلوب ولا يتجاوز 32 حرفاً");
        if (name.Length is 0 or > 150) return (null, "الاسم مطلوب ولا يتجاوز 150 حرفاً");
        if (!Enum.TryParse<PersonnelActionEffect>(r.Effect, ignoreCase: true, out var effect) || !Enum.IsDefined(effect))
            return (null, "أثر الحركة غير صالح");
        if (r.WorkflowDefinitionId is { } wf && !await db.WorkflowDefinitions.AnyAsync(d => d.Id == wf && d.IsActive, ct))
            return (null, "مسار الموافقة غير موجود أو غير مفعّل");
        if (await db.PersonnelActionTypes.IgnoreQueryFilters().AnyAsync(t => t.Code == code && t.Id != existingId, ct))
            return (null, "الرمز مستخدم");
        return (new Fields(code, name, effect, r.WorkflowDefinitionId, r.IsActive), null);
    }

    private async Task<IReadOnlyList<ActionTypeDto>> ToDtosAsync(IReadOnlyList<PersonnelActionType> rows, CancellationToken ct)
    {
        var wfIds = rows.Where(r => r.WorkflowDefinitionId is not null).Select(r => r.WorkflowDefinitionId!.Value).Distinct().ToList();
        var names = await db.WorkflowDefinitions.AsNoTracking().Where(d => wfIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return rows.Select(t => new ActionTypeDto(t.Id, t.Code, t.Name, t.Effect.ToString(), t.WorkflowDefinitionId,
            t.WorkflowDefinitionId is { } w ? names.GetValueOrDefault(w) : null, t.IsActive,
            Convert.ToBase64String(t.RowVersion))).ToList();
    }
}
