using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Organization;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class OrganizationService(AppDbContext db, IAuditWriter audit, ICurrentUser currentUser) : IOrganizationService
{
    public async Task<IReadOnlyList<InstitutionDto>> ListInstitutionsAsync(CancellationToken ct = default) =>
        await db.Institutions.OrderBy(i => i.Name)
            .Select(i => new InstitutionDto(i.Id, i.Name, i.NameEn, i.Code, i.IsActive))
            .ToListAsync(ct);

    public async Task<Result<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest r, CancellationToken ct = default)
    {
        var e = new Institution { Name = r.Name, NameEn = r.NameEn, Code = r.Code };
        db.Institutions.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Institution", e.Id, e.Name, ct: ct);
        return Result<InstitutionDto>.Ok(new InstitutionDto(e.Id, e.Name, e.NameEn, e.Code, e.IsActive));
    }

    public async Task<IReadOnlyList<OrgUnitDto>> ListOrgUnitsAsync(long? institutionId, CancellationToken ct = default)
    {
        var q = db.OrgUnits.AsQueryable();
        if (institutionId is { } id) q = q.Where(u => u.InstitutionId == id);
        return await q.OrderBy(u => u.SortOrder).ThenBy(u => u.Name)
            .Select(u => new OrgUnitDto(u.Id, u.InstitutionId, u.ParentId, u.Name, u.NameEn, u.Code,
                u.Type.ToString(), u.SortOrder, u.IsActive, u.ManagerPositionId, u.SiteId))
            .ToListAsync(ct);
    }

    public async Task<Result<OrgUnitDto>> CreateOrgUnitAsync(CreateOrgUnitRequest r, CancellationToken ct = default)
    {
        if (!await db.Institutions.AnyAsync(i => i.Id == r.InstitutionId, ct))
            return Result<OrgUnitDto>.Fail("المؤسسة غير موجودة");
        if (r.ParentId is { } pid && !await db.OrgUnits.AnyAsync(u => u.Id == pid, ct))
            return Result<OrgUnitDto>.Fail("الوحدة الأم غير موجودة");
        if (await ValidateUnitLinksAsync(null, r.ManagerPositionId, r.SiteId, ct) is { } linkError)
            return Result<OrgUnitDto>.Fail(linkError);

        var e = new OrgUnit
        {
            InstitutionId = r.InstitutionId, ParentId = r.ParentId, Name = r.Name,
            NameEn = r.NameEn, Code = r.Code, Type = r.Type, SortOrder = r.SortOrder,
            ManagerPositionId = r.ManagerPositionId, SiteId = r.SiteId,
        };
        db.OrgUnits.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "OrgUnit", e.Id, e.Name, ct: ct);
        return Result<OrgUnitDto>.Ok(new OrgUnitDto(e.Id, e.InstitutionId, e.ParentId, e.Name, e.NameEn, e.Code,
            e.Type.ToString(), e.SortOrder, e.IsActive, e.ManagerPositionId, e.SiteId));
    }

    public async Task<Result<OrgUnitDto>> UpdateOrgUnitAsync(long id, UpdateOrgUnitRequest r, CancellationToken ct = default)
    {
        var e = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (e is null) return Result<OrgUnitDto>.Fail("الوحدة التنظيمية غير موجودة");
        if (r.ParentId is { } pid)
        {
            if (pid == id) return Result<OrgUnitDto>.Fail("لا يمكن أن تكون الوحدة أباً لنفسها");
            if (!await db.OrgUnits.AnyAsync(u => u.Id == pid, ct))
                return Result<OrgUnitDto>.Fail("الوحدة الأم غير موجودة");
            if (await IsDescendantAsync(pid, id, ct))
                return Result<OrgUnitDto>.Fail("لا يمكن نقل الوحدة تحت إحدى وحداتها الفرعية");
        }
        if (await ValidateUnitLinksAsync(id, r.ManagerPositionId, r.SiteId, ct) is { } linkError)
            return Result<OrgUnitDto>.Fail(linkError);

        e.ManagerPositionId = r.ManagerPositionId;
        e.SiteId = r.SiteId;
        e.ParentId = r.ParentId;
        e.Name = r.Name;
        e.NameEn = r.NameEn;
        e.Code = r.Code;
        e.Type = r.Type;
        e.SortOrder = r.SortOrder;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", "OrgUnit", e.Id, e.Name, ct: ct);
        return Result<OrgUnitDto>.Ok(new OrgUnitDto(e.Id, e.InstitutionId, e.ParentId, e.Name, e.NameEn, e.Code,
            e.Type.ToString(), e.SortOrder, e.IsActive, e.ManagerPositionId, e.SiteId));
    }

    /// <summary>The manager seat must belong to this unit; the site must exist and be active.</summary>
    private async Task<string?> ValidateUnitLinksAsync(long? unitId, long? managerPositionId, long? siteId, CancellationToken ct)
    {
        if (managerPositionId is { } pos)
        {
            var seatUnit = await db.Positions.Where(p => p.Id == pos).Select(p => (long?)p.OrgUnitId).FirstOrDefaultAsync(ct);
            if (seatUnit is null) return "المقعد الوظيفي للمدير غير موجود";
            if (unitId is null || seatUnit != unitId) return "مقعد المدير يجب أن يكون من مقاعد الوحدة نفسها";
        }
        if (siteId is { } site && !await db.Sites.AnyAsync(s => s.Id == site && s.IsActive, ct))
            return "المعبر أو الموقع غير موجود أو غير مفعّل";
        return null;
    }

    /// <summary>True when <paramref name="candidate"/> sits somewhere below <paramref name="ancestor"/>.</summary>
    private async Task<bool> IsDescendantAsync(long candidate, long ancestor, CancellationToken ct)
    {
        var parents = await db.OrgUnits.Select(u => new { u.Id, u.ParentId }).ToDictionaryAsync(u => u.Id, u => u.ParentId, ct);
        var seen = new HashSet<long>();
        for (long? cur = candidate; cur is { } c && seen.Add(c); cur = parents.GetValueOrDefault(c))
            if (c == ancestor) return true;
        return false;
    }

    public async Task<Result<bool>> DeleteOrgUnitAsync(long id, CancellationToken ct = default)
    {
        var e = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الوحدة التنظيمية غير موجودة");
        if (await db.OrgUnits.AnyAsync(u => u.ParentId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف وحدة تحتوي على وحدات فرعية");
        if (await db.Positions.AnyAsync(p => p.OrgUnitId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف وحدة تحتوي على مناصب/موظفين");
        if (await db.DocumentTypes.AnyAsync(t => t.OrgUnitId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف دائرة تحتوي على أنواع وثائق");

        db.OrgUnits.Remove(e);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<bool>.Fail("لا يمكن حذف الوحدة لارتباطها بسجلات أخرى");
        }
        await audit.WriteAsync("Deleted", "OrgUnit", id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<PositionDto>> ListPositionsAsync(long? orgUnitId, CancellationToken ct = default)
    {
        var q = db.Positions.Include(p => p.OrgUnit).Include(p => p.CurrentOccupant).AsQueryable();
        if (orgUnitId is { } id) q = q.Where(p => p.OrgUnitId == id);
        return await q.OrderByDescending(p => p.Rank).ThenBy(p => p.Title)
            .Select(p => new PositionDto(p.Id, p.Title, p.Code, p.OrgUnitId, p.OrgUnit.Name, p.Rank,
                p.CurrentOccupantUserId, p.CurrentOccupant != null ? p.CurrentOccupant.FullName : null, p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<Result<PositionDto>> CreatePositionAsync(CreatePositionRequest r, CancellationToken ct = default)
    {
        var unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == r.OrgUnitId, ct);
        if (unit is null) return Result<PositionDto>.Fail("الوحدة التنظيمية غير موجودة");

        var e = new Position { Title = r.Title, Code = r.Code, OrgUnitId = r.OrgUnitId, Rank = r.Rank };
        db.Positions.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Position", e.Id, e.Title, ct: ct);
        return Result<PositionDto>.Ok(new PositionDto(e.Id, e.Title, e.Code, e.OrgUnitId, unit.Name, e.Rank, null, null, e.IsActive));
    }

    public async Task<Result<PositionDto>> UpdatePositionAsync(long id, UpdatePositionRequest r, CancellationToken ct = default)
    {
        var e = await db.Positions.Include(p => p.CurrentOccupant).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return Result<PositionDto>.Fail("المنصب غير موجود");
        var unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == r.OrgUnitId, ct);
        if (unit is null) return Result<PositionDto>.Fail("الوحدة التنظيمية غير موجودة");

        e.Title = r.Title;
        e.Code = r.Code;
        e.OrgUnitId = r.OrgUnitId;
        e.Rank = r.Rank;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", "Position", e.Id, e.Title, ct: ct);
        return Result<PositionDto>.Ok(new PositionDto(e.Id, e.Title, e.Code, e.OrgUnitId, unit.Name, e.Rank,
            e.CurrentOccupantUserId, e.CurrentOccupant != null ? e.CurrentOccupant.FullName : null, e.IsActive));
    }

    public async Task<Result<bool>> DeletePositionAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Positions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return Result<bool>.Fail("المنصب غير موجود");

        // Remove the seat's assignment history first (owned by the position), then the seat.
        var history = await db.PositionAssignments.Where(a => a.PositionId == id).ToListAsync(ct);
        db.PositionAssignments.RemoveRange(history);
        db.Positions.Remove(e);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<bool>.Fail("لا يمكن حذف المنصب لارتباطه بمعاملات (بريد/تفويض)");
        }
        await audit.WriteAsync("Deleted", "Position", id, e.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<PositionDto>> AssignOccupantAsync(long positionId, AssignOccupantRequest r, CancellationToken ct = default)
    {
        var pos = await db.Positions.Include(p => p.OrgUnit).FirstOrDefaultAsync(p => p.Id == positionId, ct);
        if (pos is null) return Result<PositionDto>.Fail("المنصب غير موجود");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null) return Result<PositionDto>.Fail("المستخدم غير موجود");

        // Close the previous current assignment, open a new one (position-based hand-over).
        var open = await db.PositionAssignments.Where(a => a.PositionId == positionId && a.IsCurrent).ToListAsync(ct);
        foreach (var a in open) { a.IsCurrent = false; a.EndDate = DateTime.UtcNow; }

        var previousOccupant = pos.CurrentOccupantUserId;
        db.PositionAssignments.Add(new PositionAssignment { PositionId = positionId, UserId = r.UserId, IsCurrent = true });
        pos.CurrentOccupantUserId = r.UserId;
        pos.AddDomainEvent(new Archiving.Domain.Events.PositionOccupantChangedEvent(pos.Id, previousOccupant, r.UserId)
        {
            TriggeredByUserId = currentUser.UserId?.ToString(),
        });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("OccupantAssigned", "Position", pos.Id, pos.Title, newValues: user.FullName, ct: ct);

        return Result<PositionDto>.Ok(new PositionDto(pos.Id, pos.Title, pos.Code, pos.OrgUnitId, pos.OrgUnit.Name,
            pos.Rank, user.Id, user.FullName, pos.IsActive));
    }

    public async Task<IReadOnlyList<UserLookupDto>> ListUsersAsync(CancellationToken ct = default) =>
        await db.Users.Where(u => u.IsActive).OrderBy(u => u.FullName)
            .Select(u => new UserLookupDto(u.Id, u.FullName, u.Email))
            .ToListAsync(ct);
}
