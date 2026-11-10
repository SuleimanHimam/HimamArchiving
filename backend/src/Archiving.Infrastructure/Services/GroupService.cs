using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Groups;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class GroupService(AppDbContext db, ICurrentUser currentUser, IAuditWriter audit) : IGroupService
{
    private const string EntityType = "CorrespondenceGroup";

    public async Task<IReadOnlyList<GroupDto>> ListAsync(bool activeOnly, bool memberOnly = false, CancellationToken ct = default)
    {
        var q = db.CorrespondenceGroups.Include(g => g.Members).AsQueryable();
        if (activeOnly) q = q.Where(g => g.IsActive);

        if (memberOnly)
        {
            var uid = currentUser.UserId ?? 0;
            var ouId = await db.Users.Where(u => u.Id == uid).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
            // Membership is either the user themselves or the department they sit in — a group that
            // names a department addresses everyone in it, so those people are members of it too.
            q = q.Where(g => g.Members.Any(m =>
                (m.MemberType == GroupMemberType.User && m.UserId == uid)
                || (m.MemberType == GroupMemberType.OrgUnit && ouId != null && m.OrgUnitId == ouId)));
        }

        var groups = await q.OrderBy(g => g.Name).ToListAsync(ct);
        return await ToDtosAsync(groups, ct);
    }

    public async Task<IReadOnlyList<GroupDto>> ListForUserAsync(long userId, CancellationToken ct = default)
    {
        var ouId = await db.Users.Where(u => u.Id == userId).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        var groups = await db.CorrespondenceGroups.Include(g => g.Members)
            .Where(g => g.IsActive && g.Members.Any(m =>
                (m.MemberType == GroupMemberType.User && m.UserId == userId)
                || (m.MemberType == GroupMemberType.OrgUnit && ouId != null && m.OrgUnitId == ouId)))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
        return await ToDtosAsync(groups, ct);
    }

    public async Task<bool> IsMemberAsync(long groupId, long userId, CancellationToken ct = default)
    {
        var ouId = await db.Users.Where(u => u.Id == userId).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        return await db.CorrespondenceGroupMembers.AnyAsync(m => m.GroupId == groupId && (
            (m.MemberType == GroupMemberType.User && m.UserId == userId)
            || (m.MemberType == GroupMemberType.OrgUnit && ouId != null && m.OrgUnitId == ouId)), ct);
    }

    public async Task<Result<GroupDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var g = await db.CorrespondenceGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return Result<GroupDto>.Fail("المجموعة غير موجودة");
        return Result<GroupDto>.Ok((await ToDtosAsync([g], ct))[0]);
    }

    public async Task<Result<GroupDto>> CreateAsync(SaveGroupRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<GroupDto>.Fail("اسم المجموعة مطلوب");
        var institutionId = await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);
        if (institutionId is null) return Result<GroupDto>.Fail("لا توجد مؤسسة مُعرّفة");

        var g = new CorrespondenceGroup
        {
            InstitutionId = institutionId.Value,
            Name = r.Name.Trim(),
            Description = r.Description,
            IsActive = r.IsActive,
            Members = BuildMembers(r.Members),
        };
        db.CorrespondenceGroups.Add(g);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", EntityType, g.Id, g.Name, ct: ct);
        return Result<GroupDto>.Ok((await ToDtosAsync([g], ct))[0]);
    }

    public async Task<Result<GroupDto>> UpdateAsync(long id, SaveGroupRequest r, CancellationToken ct = default)
    {
        var g = await db.CorrespondenceGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return Result<GroupDto>.Fail("المجموعة غير موجودة");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<GroupDto>.Fail("اسم المجموعة مطلوب");

        g.Name = r.Name.Trim();
        g.Description = r.Description;
        g.IsActive = r.IsActive;
        db.CorrespondenceGroupMembers.RemoveRange(g.Members);
        g.Members = BuildMembers(r.Members);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", EntityType, g.Id, g.Name, ct: ct);
        return Result<GroupDto>.Ok((await ToDtosAsync([g], ct))[0]);
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var g = await db.CorrespondenceGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return Result<bool>.Fail("المجموعة غير موجودة");
        db.CorrespondenceGroupMembers.RemoveRange(g.Members);   // remove members explicitly (not all providers cascade)
        db.CorrespondenceGroups.Remove(g);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", EntityType, id, g.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- helpers ----

    private static List<CorrespondenceGroupMember> BuildMembers(IReadOnlyList<GroupMemberInput>? members) =>
        (members ?? [])
            .Where(m => m.MemberType == GroupMemberType.User ? m.UserId is not null : m.OrgUnitId is not null)
            .Select(m => new CorrespondenceGroupMember
            {
                MemberType = m.MemberType,
                UserId = m.MemberType == GroupMemberType.User ? m.UserId : null,
                OrgUnitId = m.MemberType == GroupMemberType.OrgUnit ? m.OrgUnitId : null,
            }).ToList();

    private async Task<List<GroupDto>> ToDtosAsync(IReadOnlyList<CorrespondenceGroup> groups, CancellationToken ct)
    {
        var userIds = groups.SelectMany(g => g.Members).Where(m => m.UserId is not null).Select(m => m.UserId!.Value).Distinct().ToList();
        var unitIds = groups.SelectMany(g => g.Members).Where(m => m.OrgUnitId is not null).Select(m => m.OrgUnitId!.Value).Distinct().ToList();
        var userNames = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var unitNames = await db.OrgUnits.Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return groups.Select(g => new GroupDto(
            g.Id, g.Name, g.Description, g.IsActive, g.Members.Count,
            g.Members.Select(m => new GroupMemberDto(
                m.Id, m.MemberType.ToString(),
                m.UserId, m.UserId is { } uid && userNames.TryGetValue(uid, out var un) ? un : null,
                m.OrgUnitId, m.OrgUnitId is { } oid && unitNames.TryGetValue(oid, out var on) ? on : null)).ToList()
        )).ToList();
    }
}
