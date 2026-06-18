using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Organization;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class OrganizationService(AppDbContext db, IAuditWriter audit) : IOrganizationService
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
                u.Type.ToString(), u.SortOrder, u.IsActive))
            .ToListAsync(ct);
    }

    public async Task<Result<OrgUnitDto>> CreateOrgUnitAsync(CreateOrgUnitRequest r, CancellationToken ct = default)
    {
        if (!await db.Institutions.AnyAsync(i => i.Id == r.InstitutionId, ct))
            return Result<OrgUnitDto>.Fail("المؤسسة غير موجودة");
        if (r.ParentId is { } pid && !await db.OrgUnits.AnyAsync(u => u.Id == pid, ct))
            return Result<OrgUnitDto>.Fail("الوحدة الأم غير موجودة");

        var e = new OrgUnit
        {
            InstitutionId = r.InstitutionId, ParentId = r.ParentId, Name = r.Name,
            NameEn = r.NameEn, Code = r.Code, Type = r.Type, SortOrder = r.SortOrder,
        };
        db.OrgUnits.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "OrgUnit", e.Id, e.Name, ct: ct);
        return Result<OrgUnitDto>.Ok(new OrgUnitDto(e.Id, e.InstitutionId, e.ParentId, e.Name, e.NameEn, e.Code,
            e.Type.ToString(), e.SortOrder, e.IsActive));
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

    public async Task<Result<PositionDto>> AssignOccupantAsync(long positionId, AssignOccupantRequest r, CancellationToken ct = default)
    {
        var pos = await db.Positions.Include(p => p.OrgUnit).FirstOrDefaultAsync(p => p.Id == positionId, ct);
        if (pos is null) return Result<PositionDto>.Fail("المنصب غير موجود");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null) return Result<PositionDto>.Fail("المستخدم غير موجود");

        // Close the previous current assignment, open a new one (position-based hand-over).
        var open = await db.PositionAssignments.Where(a => a.PositionId == positionId && a.IsCurrent).ToListAsync(ct);
        foreach (var a in open) { a.IsCurrent = false; a.EndDate = DateTime.UtcNow; }

        db.PositionAssignments.Add(new PositionAssignment { PositionId = positionId, UserId = r.UserId, IsCurrent = true });
        pos.CurrentOccupantUserId = r.UserId;
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
