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
    /// <summary>Name of the seeded scan-to-archive workflow definition (looked up by the document service).</summary>
    public const string ArchiveIntakeWorkflowName = "أرشفة المستندات — استلام ومعالجة";

    /// <param name="includeDemoAccounts">Seed one try-it account per built-in role. Development only —
    /// their password is published by <c>GET api/auth/demo-accounts</c> so the login page can offer
    /// them as buttons, which is exactly what must never happen on a real deployment.</param>
    public static async Task SeedAsync(
        AppDbContext db, IPasswordHasher hasher, bool includeDemoAccounts = false, CancellationToken ct = default)
    {
        await SeedPermissionsAsync(db, ct);
        await SeedRolesAsync(db, ct);
        await SeedAdminAsync(db, hasher, ct);
        await SeedOrganizationAsync(db, ct);
        await SeedDepartmentManagersAsync(db, hasher, ct);
        if (includeDemoAccounts) await SeedDemoAccountsAsync(db, hasher, ct);
        await SeedClassificationTypesAsync(db, ct);
        await SeedRoleClassificationsAsync(db, ct);
        await SeedDestructionMethodsAsync(db, ct);
        await SeedArchiveIntakeAsync(db, ct);
        await SeedLetterheadApprovalAsync(db, ct);
    }

    public const string LetterheadApprovalWorkflowName = "اعتماد ترويسة القسم";

    /// <summary>
    /// The approval route a department letterhead edit must clear before it reaches paper
    /// (requirement 4). One stage, assigned to the System Administrator role: the institution's
    /// brand owner is the only party above a department head. Idempotent by name.
    ///
    /// Without this definition <c>LetterheadService.SaveDepartmentSlotsAsync</c> refuses the edit
    /// outright rather than publishing it unreviewed, so seeding it is not optional.
    /// </summary>
    private static async Task SeedLetterheadApprovalAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.WorkflowDefinitions.AnyAsync(d => d.Name == LetterheadApprovalWorkflowName, ct)) return;

        var adminRoleId = await db.Roles
            .Where(r => r.Name == "System Administrator").Select(r => (long?)r.Id).FirstOrDefaultAsync(ct);
        if (adminRoleId is null) return;   // roles not seeded yet; next startup will pick it up

        db.WorkflowDefinitions.Add(new WorkflowDefinition
        {
            Name = LetterheadApprovalWorkflowName,
            Description = "مراجعة تعديل ترويسة قسم قبل اعتمادها وظهورها على الكتب الصادرة.",
            TriggerModule = "Letterhead",
            Stages =
            {
                new WorkflowStage
                {
                    Name = "اعتماد هوية المؤسسة", Order = 1, IsFinal = true,
                    AssigneeType = StageAssigneeType.Role, AssigneeRoleId = adminRoleId,
                    AllowedActions = WorkflowActionType.Approve | WorkflowActionType.Reject
                                     | WorkflowActionType.Comment,
                },
            },
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ensures an Archive department (org-unit + records-officer seat) and the two-stage
    /// scan-to-archive workflow definition exist. Idempotent — runs every startup.</summary>
    private static async Task SeedArchiveIntakeAsync(AppDbContext db, CancellationToken ct)
    {
        var institutionId = await db.OrgUnits.OrderBy(o => o.Id).Select(o => o.InstitutionId).FirstOrDefaultAsync(ct);
        if (institutionId == 0) return; // organization not seeded yet

        // Archive department (reuse an existing "قسم الأرشيف" if present).
        var archive = await db.OrgUnits.FirstOrDefaultAsync(o => o.Name == "قسم الأرشيف", ct);
        if (archive is null)
        {
            archive = new OrgUnit { InstitutionId = institutionId, Name = "قسم الأرشيف", Type = OrgUnitType.Department, SortOrder = 9 };
            db.OrgUnits.Add(archive);
            await db.SaveChangesAsync(ct);
        }

        // Records-officer seat in the Archive department (the workflow routes here).
        var officer = await db.Positions.FirstOrDefaultAsync(p => p.OrgUnitId == archive.Id && p.Title == "مسؤول السجلات", ct);
        if (officer is null)
        {
            // Occupy the seat with an active user already in the Archive department, if any.
            var occupant = await db.Users.Where(u => u.IsActive && u.OrgUnitId == archive.Id).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
            officer = new Position { Title = "مسؤول السجلات", OrgUnitId = archive.Id, Rank = 10, CurrentOccupantUserId = occupant };
            db.Positions.Add(officer);
            await db.SaveChangesAsync(ct);
        }
        if (archive.ManagerPositionId is null) { archive.ManagerPositionId = officer.Id; await db.SaveChangesAsync(ct); }

        // The two-stage archive-intake workflow (idempotent by name).
        if (await db.WorkflowDefinitions.AnyAsync(d => d.Name == ArchiveIntakeWorkflowName, ct)) return;

        var def = new WorkflowDefinition
        {
            Name = ArchiveIntakeWorkflowName,
            Description = "استلام المستند من القسم المالك ومراجعته وتخصيص موقع فيزيائي له ثم أرشفته.",
            TriggerModule = "Document",
            Stages =
            {
                new WorkflowStage
                {
                    Name = "مراجعة الأرشيف", Order = 1, IsFinal = false,
                    AssigneeType = StageAssigneeType.OrgUnit, AssigneeOrgUnitId = archive.Id,
                    AllowedActions = WorkflowActionType.Approve | WorkflowActionType.Return | WorkflowActionType.Hold | WorkflowActionType.Comment,
                },
                new WorkflowStage
                {
                    Name = "تخصيص الموقع وإنهاء الأرشفة", Order = 2, IsFinal = true,
                    AssigneeType = StageAssigneeType.OrgUnit, AssigneeOrgUnitId = archive.Id,
                    AllowedActions = WorkflowActionType.Close | WorkflowActionType.Return | WorkflowActionType.Comment,
                },
            },
        };
        db.WorkflowDefinitions.Add(def);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDestructionMethodsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.DestructionMethodOptions.AnyAsync(ct)) return;
        string[] defaults =
        [
            "محو التشفير (Crypto-Shred)", "كتابة آمنة فوقية", "حذف وإبطال البصمة",
            "تقطيع", "حرق", "تذويب", "إزالة مغناطيسية",
        ];
        for (var i = 0; i < defaults.Length; i++)
            db.DestructionMethodOptions.Add(new DestructionMethodOption { Label = defaults[i], SortOrder = i + 1 });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRoleClassificationsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.RoleClassifications.AnyAsync(ct)) return;

        var roles = await db.Roles.ToListAsync(ct);
        var types = await db.ClassificationTypes.ToListAsync(ct);
        if (roles.Count == 0 || types.Count == 0) return;

        long AdminId(string name) => roles.FirstOrDefault(r => r.Name == name)?.Id ?? 0;

        var sysAdmin     = AdminId("System Administrator");
        var manager      = AdminId("Manager");
        var archiver     = AdminId("Archive Officer");
        var employee     = AdminId("Employee");

        // Default clearance mapping:
        //   عام / داخلي      → all 4 roles
        //   سري              → sysAdmin, manager, archiver
        //   سري للغاية       → sysAdmin, manager only
        var defaults = new Dictionary<string, long[]>
        {
            ["عام"]         = [sysAdmin, manager, archiver, employee],
            ["داخلي"]       = [sysAdmin, manager, archiver, employee],
            ["سري"]         = [sysAdmin, manager, archiver],
            ["سري للغاية"]  = [sysAdmin, manager],
        };

        foreach (var (nameAr, allowedRoles) in defaults)
        {
            var ctype = types.FirstOrDefault(t => t.NameAr == nameAr);
            if (ctype is null) continue;

            foreach (var roleId in allowedRoles.Where(id => id != 0))
                db.RoleClassifications.Add(new RoleClassification
                {
                    RoleId               = roleId,
                    ClassificationTypeId = ctype.Id,
                });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedClassificationTypesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.ClassificationTypes.AnyAsync(ct)) return;

        db.ClassificationTypes.AddRange(
            new ClassificationType { NameAr = "عام",           NameEn = "Public",             Color = "#2f5d3a", SortOrder = 0, IsSystem = true,  IsActive = true, Description = "وثائق عامة لا تستلزم قيودًا" },
            new ClassificationType { NameAr = "داخلي",         NameEn = "Internal",           Color = "#1e3a8a", SortOrder = 1, IsSystem = true,  IsActive = true, Description = "للاستخدام الداخلي فقط" },
            new ClassificationType { NameAr = "سري",           NameEn = "Confidential",       Color = "#9a6312", SortOrder = 2, IsSystem = true,  IsActive = true, Description = "وثائق سرية مقيّدة الاطلاع" },
            new ClassificationType { NameAr = "سري للغاية",    NameEn = "Highly Confidential",Color = "#9b2226", SortOrder = 3, IsSystem = true,  IsActive = true, Description = "أعلى درجات التصنيف الأمني" }
        );
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reconciles the Permissions table with <see cref="PermissionCatalog"/>: adds what is
    /// missing and removes codes the system no longer enforces (together with any role grants that
    /// referenced them), so the roles matrix never offers a switch that is wired to nothing.</summary>
    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var toAdd = new List<Permission>();

        foreach (var (resource, actions) in PermissionCatalog.Resources)
            foreach (var action in actions)
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

        // Filtered in memory: the table holds a few dozen rows, and a negated Contains over a local
        // collection is not translatable by the MySQL provider.
        var obsolete = (await db.Permissions.ToListAsync(ct))
            .Where(p => !PermissionCatalog.AllCodes.Contains(p.Code)).ToList();
        if (obsolete.Count > 0)
        {
            // RolePermission has no cascade configured on the join, so clear the grants first.
            var ids = obsolete.Select(p => p.Id).ToList();
            var grants = await db.RolePermissions.Where(rp => ids.Contains(rp.PermissionId)).ToListAsync(ct);
            db.RolePermissions.RemoveRange(grants);
            db.Permissions.RemoveRange(obsolete);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var allPerms = await db.Permissions.ToListAsync(ct);

        // Role name -> predicate selecting its permissions. Shared with the "reset to defaults"
        // endpoint so a change here reaches both paths.
        foreach (var (name, desc, pick) in PermissionCatalog.RoleDefaults)
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
        var adminRole = await db.Roles.FirstAsync(r => r.Name == "System Administrator", ct);

        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);
        if (admin is null)
        {
            admin = new User
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
        }

        // Always ensure the admin has the System Administrator role (idempotent).
        var hasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRole.Id, ct);
        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>The password every seeded department manager starts with. It is the same for all
    /// five and is published in the deployment notes, so it is a first-login credential and nothing
    /// more — change it before the system carries real correspondence.</summary>
    public const string DepartmentManagerPassword = "Manager@12345";

    /// <summary>A department the institution has out of the box, and the head who runs it.</summary>
    /// <param name="ManagerTitle">Title of the seat that manages the unit — this is what makes the
    /// occupant a manager as far as <c>IAccessScope.ManagedOrgUnitIdsAsync</c> is concerned.</param>
    public sealed record DefaultDepartment(
        string Name, string NameEn, string Code, string ManagerTitle,
        string ManagerFullName, string ManagerEmail);

    /// <summary>
    /// The five departments the system ships with, each with a head of its own.
    ///
    /// Every scoping rule in the system is written in terms of a department and the person who
    /// heads it — whose mail a manager reads, whose activity appears in their log, which
    /// letterheads they may author. An institution with departments but no heads has all of that
    /// machinery and nobody it applies to, so the two are seeded together.
    ///
    /// The archive department is one of the five rather than a sixth: it is a department that
    /// happens to hold the custody right, and <c>SeedArchiveIntakeAsync</c> finds it by name.
    /// </summary>
    public static readonly DefaultDepartment[] DefaultDepartments =
    [
        new("الموارد البشرية", "Human Resources", "HR", "مدير الموارد البشرية",
            "مدير الموارد البشرية", "hr.manager@archiving.local"),
        new("المالية", "Finance", "FIN", "مدير المالية",
            "مدير المالية", "finance.manager@archiving.local"),
        new("تقنية المعلومات", "Information Technology", "IT", "مدير تقنية المعلومات",
            "مدير تقنية المعلومات", "it.manager@archiving.local"),
        new("الشؤون الإدارية", "Administrative Affairs", "ADM", "مدير الشؤون الإدارية",
            "مدير الشؤون الإدارية", "admin.affairs.manager@archiving.local"),
        new("قسم الأرشيف", "Archive", "ARC", "مدير الأرشيف",
            "مدير الأرشيف", "archive.manager@archiving.local"),
    ];

    /// <summary>
    /// Ensures each of <see cref="DefaultDepartments"/> exists, has a manager seat, and has that
    /// seat filled by an active account holding the Manager role.
    ///
    /// Idempotent in every part, and it never overwrites: a department that has been renamed keeps
    /// its name, a seat that already has an occupant keeps them, and an existing account keeps its
    /// password. Only what is missing is created — so this runs safely on an institution that has
    /// been in use for a year.
    /// </summary>
    private static async Task SeedDepartmentManagersAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        var root = await db.OrgUnits.OrderBy(o => o.Id).FirstOrDefaultAsync(ct);
        if (root is null) return;                        // organization not seeded yet
        var managerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Manager", ct);
        if (managerRole is null) return;                 // roles not seeded yet

        var order = 1;
        foreach (var dept in DefaultDepartments)
        {
            var unit = await db.OrgUnits.FirstOrDefaultAsync(o => o.Name == dept.Name, ct);
            if (unit is null)
            {
                unit = new OrgUnit
                {
                    InstitutionId = root.InstitutionId,
                    ParentId = root.Id,
                    Name = dept.Name,
                    NameEn = dept.NameEn,
                    Code = dept.Code,
                    Type = OrgUnitType.Department,
                    SortOrder = order,
                };
                db.OrgUnits.Add(unit);
                await db.SaveChangesAsync(ct);
            }
            order++;

            var manager = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == dept.ManagerEmail, ct);
            if (manager is null)
            {
                manager = new User
                {
                    FullName = dept.ManagerFullName,
                    Email = dept.ManagerEmail,
                    JobTitle = dept.ManagerTitle,
                    OrgUnitId = unit.Id,
                    PasswordHash = hasher.Hash(DepartmentManagerPassword),
                    // A head reads their department's correspondence, which routinely carries the
                    // confidential grade; the two levels above it stay with the administrator.
                    Clearance = ConfidentialityLevel.Confidential,
                    IsActive = true,
                };
                db.Users.Add(manager);
                await db.SaveChangesAsync(ct);
            }

            if (!await db.UserRoles.AnyAsync(ur => ur.UserId == manager.Id && ur.RoleId == managerRole.Id, ct))
            {
                db.UserRoles.Add(new UserRole { UserId = manager.Id, RoleId = managerRole.Id });
                await db.SaveChangesAsync(ct);
            }

            /* The seat, not the role, is what makes someone this department's head: the Manager
               role says what they may do, the manager position says which department they do it
               to. Both are needed, and the position is the one the scope reads. */
            var seat = await db.Positions.FirstOrDefaultAsync(p => p.OrgUnitId == unit.Id && p.Title == dept.ManagerTitle, ct);
            if (seat is null)
            {
                seat = new Position { Title = dept.ManagerTitle, OrgUnitId = unit.Id, Rank = 10 };
                db.Positions.Add(seat);
                await db.SaveChangesAsync(ct);
            }
            if (seat.CurrentOccupantUserId is null)
            {
                seat.CurrentOccupantUserId = manager.Id;
                db.PositionAssignments.Add(new PositionAssignment { PositionId = seat.Id, UserId = manager.Id, IsCurrent = true });
                await db.SaveChangesAsync(ct);
            }
            if (unit.ManagerPositionId is null)
            {
                unit.ManagerPositionId = seat.Id;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>A try-it account: everything the login page needs to offer it as a button.</summary>
    /// <param name="OrgUnitName">Department to file the user under; skipped if the unit is absent.</param>
    public sealed record DemoAccount(
        string Email, string Password, string FullName, string JobTitle,
        string RoleName, string? OrgUnitName, ConfidentialityLevel Clearance);

    /// <summary>The accounts behind the login page's "try the system as…" buttons.
    ///
    /// One per built-in role, because the whole point is to see how the system changes shape for
    /// each: what the dashboard shows, which menus appear, whose mail and disposal requests are
    /// visible. The seeded administrator is first so the fullest view is one click away.
    ///
    /// Passwords are per-account rather than shared: they are published to anyone who can open the
    /// login page, so nothing here may resemble a password used elsewhere.</summary>
    public static readonly DemoAccount[] DemoAccounts =
    [
        new(AdminEmail, AdminPassword, "مدير النظام", "مدير النظام",
            "System Administrator", null, ConfidentialityLevel.HighlyConfidential),
        new("demo.manager@archiving.local", "DemoManager@2026", "سارة المدير", "مدير إدارة",
            "Manager", "الإدارة العامة", ConfidentialityLevel.Confidential),
        new("demo.archivist@archiving.local", "DemoArchivist@2026", "خالد الأرشيفي", "أمين أرشيف",
            "Archive Officer", "قسم الأرشيف", ConfidentialityLevel.Confidential),
        new("demo.employee@archiving.local", "DemoEmployee@2026", "ليان الموظفة", "موظف",
            "Employee", "تقنية المعلومات", ConfidentialityLevel.Internal),
    ];

    /// <summary>Creates any missing demo account and gives it its role. Idempotent, and it never
    /// touches an account that already exists — a tester who changed one of these passwords or
    /// moved the user to another department keeps that change across restarts.</summary>
    private static async Task SeedDemoAccountsAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        foreach (var demo in DemoAccounts)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == demo.RoleName, ct);
            if (role is null) continue;                 // role missing; next startup will pick it up

            var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == demo.Email, ct);
            if (user is null)
            {
                user = new User
                {
                    FullName = demo.FullName,
                    Email = demo.Email,
                    JobTitle = demo.JobTitle,
                    PasswordHash = hasher.Hash(demo.Password),
                    Clearance = demo.Clearance,
                    IsActive = true,
                    OrgUnitId = demo.OrgUnitName is null
                        ? null
                        : await db.OrgUnits.Where(o => o.Name == demo.OrgUnitName)
                            .Select(o => (long?)o.Id).FirstOrDefaultAsync(ct),
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
            }

            if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static async Task SeedOrganizationAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Institutions.AnyAsync(ct))
        {
            // A database seeded by an earlier build carries the misspelt default. Corrected in
            // place — but only where it is still character-for-character that string: an
            // institution someone has already named is theirs, and the seeder does not rename it.
            var misspelt = await db.Institutions.FirstOrDefaultAsync(i => i.Name == "مؤسسة حِمم", ct);
            if (misspelt is not null)
            {
                misspelt.Name = "مؤسسة همم";
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        // همم — with a hāʾ. Not حمم, which is a different word entirely.
        var institution = new Institution { Name = "مؤسسة همم", NameEn = "Himam", Code = "HIMAM" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync(ct);

        var general = new OrgUnit { InstitutionId = institution.Id, Name = "الإدارة العامة", Type = OrgUnitType.Directorate, SortOrder = 0 };
        db.OrgUnits.Add(general);
        await db.SaveChangesAsync(ct);

        // The departments themselves — and their heads — are seeded by SeedDepartmentManagersAsync,
        // which also reconciles an institution that already exists. Only the directorate they hang
        // from is created here.
        var dg = new Position { Title = "مدير عام", OrgUnitId = general.Id, Rank = 20 };
        db.Positions.Add(dg);
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
