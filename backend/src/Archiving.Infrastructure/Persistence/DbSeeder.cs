using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Persistence;

/// <summary>Idempotent seed of RBAC (permissions + roles) and the first administrator account.</summary>
public static class DbSeeder
{
    public const string AdminEmail = "admin@archiving.local";
    public const string AdminPassword = "Admin@12345";

    private static readonly string[] Resources =
    [
        "Documents", "IncomingMail", "OutgoingMail", "Workflow", "Archive",
        "Reports", "Users", "Organization", "Settings", "Audit"
    ];

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await SeedPermissionsAsync(db, ct);
        await SeedRolesAsync(db, ct);
        await SeedAdminAsync(db, hasher, ct);
        await SeedOrganizationAsync(db, ct);
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var toAdd = new List<Permission>();

        foreach (var resource in Resources)
            foreach (PermissionAction action in Enum.GetValues<PermissionAction>())
            {
                var code = $"{resource}.{action}";
                if (!existing.Contains(code))
                    toAdd.Add(new Permission { Resource = resource, Action = action, Code = code });
            }

        if (toAdd.Count > 0)
        {
            db.Permissions.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var allPerms = await db.Permissions.ToListAsync(ct);

        // role name -> predicate selecting its permissions
        var roleDefs = new (string Name, string Desc, Func<Permission, bool> Pick)[]
        {
            ("System Administrator", "تحكم كامل بالنظام", _ => true),
            ("Manager", "اعتماد وتظهير المعاملات ومراجعة التقارير",
                p => p.Action is PermissionAction.View or PermissionAction.Approve
                    or PermissionAction.Forward or PermissionAction.Print
                    || p.Resource == "Reports"),
            ("Archive Officer", "إدخال وتصنيف وحفظ الوثائق وإدارة المواقع",
                p => p.Resource is "Documents" or "Archive"
                    || (p.Resource is "IncomingMail" or "OutgoingMail" && p.Action == PermissionAction.View)),
            ("Employee", "استلام المعاملات والرد والإحالة والمتابعة",
                p => (p.Resource is "Documents" or "IncomingMail" or "OutgoingMail")
                    && p.Action is PermissionAction.View or PermissionAction.Create or PermissionAction.Forward),
        };

        foreach (var (name, desc, pick) in roleDefs)
        {
            var role = await db.Roles.Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == name, ct);

            if (role is null)
            {
                role = new Role { Name = name, Description = desc, IsSystem = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
            }

            var current = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var perm in allPerms.Where(pick))
                if (!current.Contains(perm.Id))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == AdminEmail, ct))
            return;

        var adminRole = await db.Roles.FirstAsync(r => r.Name == "System Administrator", ct);

        var admin = new User
        {
            FullName = "مدير النظام",
            Email = AdminEmail,
            JobTitle = "مدير النظام",
            PasswordHash = hasher.Hash(AdminPassword),
            Clearance = ConfidentialityLevel.HighlyConfidential,
            IsActive = true,
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedOrganizationAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Institutions.AnyAsync(ct)) return;

        var institution = new Institution { Name = "مؤسسة حِمم", NameEn = "Himam", Code = "HIMAM" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync(ct);

        var general = new OrgUnit { InstitutionId = institution.Id, Name = "الإدارة العامة", Type = OrgUnitType.Directorate, SortOrder = 0 };
        db.OrgUnits.Add(general);
        await db.SaveChangesAsync(ct);

        var hr = new OrgUnit { InstitutionId = institution.Id, ParentId = general.Id, Name = "الموارد البشرية", Type = OrgUnitType.Department, SortOrder = 1 };
        var finance = new OrgUnit { InstitutionId = institution.Id, ParentId = general.Id, Name = "المالية", Type = OrgUnitType.Department, SortOrder = 2 };
        var it = new OrgUnit { InstitutionId = institution.Id, ParentId = general.Id, Name = "تقنية المعلومات", Type = OrgUnitType.Department, SortOrder = 3 };
        db.OrgUnits.AddRange(hr, finance, it);
        await db.SaveChangesAsync(ct);

        var dg = new Position { Title = "مدير عام", OrgUnitId = general.Id, Rank = 20 };
        db.Positions.AddRange(
            dg,
            new Position { Title = "رئيس قسم الموارد البشرية", OrgUnitId = hr.Id, Rank = 10 },
            new Position { Title = "رئيس قسم المالية", OrgUnitId = finance.Id, Rank = 10 },
            new Position { Title = "موظف تقنية معلومات", OrgUnitId = it.Id, Rank = 1 });
        await db.SaveChangesAsync(ct);

        // Make the seeded admin the Director-General occupant.
        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);
        if (admin is not null)
        {
            dg.CurrentOccupantUserId = admin.Id;
            db.PositionAssignments.Add(new PositionAssignment { PositionId = dg.Id, UserId = admin.Id, IsCurrent = true });
            await db.SaveChangesAsync(ct);
        }
    }
}
