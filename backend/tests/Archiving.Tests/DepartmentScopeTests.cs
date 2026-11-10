using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Diwan;
using Archiving.Application.Features.Printing;
using Archiving.Application.Features.Users;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

/// <summary>
/// Department scope: who a manager may see, and where the line is drawn.
///
/// What is worth asserting here is not that the queries run but that the rules the module exists
/// to enforce hold — a manager reaches their own department and no further, an employee reaches
/// only themselves, a granted department really does widen the reach, and none of it can be
/// widened by the person it applies to.
/// </summary>
public class DepartmentScopeTests
{
    // Departments 10/20, their heads 100/200, and one employee (101) under department 10.
    private const long DeptA = 10, DeptB = 20;
    private const long HeadA = 100, HeadB = 200, StaffA = 101, Admin = 1;

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.Roles.Add(new Role { Id = 1, Name = "System Administrator" });
        db.Roles.Add(new Role { Id = 2, Name = "Manager" });

        db.OrgUnits.Add(new OrgUnit { Id = DeptA, InstitutionId = 1, Name = "قسم أ", ManagerPositionId = 1 });
        db.OrgUnits.Add(new OrgUnit { Id = DeptB, InstitutionId = 1, Name = "قسم ب", ManagerPositionId = 2 });
        db.Positions.Add(new Position { Id = 1, Title = "مدير أ", OrgUnitId = DeptA, CurrentOccupantUserId = HeadA });
        db.Positions.Add(new Position { Id = 2, Title = "مدير ب", OrgUnitId = DeptB, CurrentOccupantUserId = HeadB });

        db.Users.Add(new User { Id = Admin, FullName = "مدير النظام", Email = "admin@x" });
        db.Users.Add(new User { Id = HeadA, FullName = "رئيس أ", Email = "a@x", OrgUnitId = DeptA });
        db.Users.Add(new User { Id = HeadB, FullName = "رئيس ب", Email = "b@x", OrgUnitId = DeptB });
        db.Users.Add(new User { Id = StaffA, FullName = "موظف أ", Email = "s@x", OrgUnitId = DeptA });
        db.UserRoles.Add(new UserRole { UserId = Admin, RoleId = 1 });
        db.UserRoles.Add(new UserRole { UserId = HeadA, RoleId = 2 });
        db.UserRoles.Add(new UserRole { UserId = HeadB, RoleId = 2 });

        db.SaveChanges();
        return db;
    }

    private static (AccessScope Scope, MutableCurrentUser Me) ScopeFor(AppDbContext db, long userId)
    {
        var me = new MutableCurrentUser { UserId = userId };
        return (new AccessScope(db, me), me);
    }

    // ── Who the scope thinks you are ─────────────────────────────────────────

    [Fact]
    public async Task Head_of_a_department_manages_it_but_an_employee_of_it_does_not()
    {
        var db = NewDb();
        Assert.Equal([DeptA], await ScopeFor(db, HeadA).Scope.ManagedOrgUnitIdsAsync());
        // The employee belongs to the department without heading it — the distinction the whole
        // mail and monitoring split rests on.
        Assert.Empty(await ScopeFor(db, StaffA).Scope.ManagedOrgUnitIdsAsync());
        Assert.Equal([DeptA], await ScopeFor(db, StaffA).Scope.OrgUnitIdsAsync());
    }

    [Fact]
    public async Task A_granted_department_is_reached_and_managed_alongside_the_users_own()
    {
        var db = NewDb();
        db.UserOrgUnitAccess.Add(new UserOrgUnitAccess { UserId = HeadA, OrgUnitId = DeptB, GrantedByUserId = Admin });
        await db.SaveChangesAsync();

        var scope = ScopeFor(db, HeadA).Scope;
        // Their own department leads: it is still where anything they create is filed.
        Assert.Equal([DeptA, DeptB], await scope.OrgUnitIdsAsync());
        Assert.Equal([DeptA, DeptB], (await scope.ManagedOrgUnitIdsAsync()).Order().ToList());
    }

    [Fact]
    public async Task The_administrator_manages_no_department_because_they_are_not_scoped_at_all()
    {
        var db = NewDb();
        var scope = ScopeFor(db, Admin).Scope;
        Assert.True(await scope.IsSystemAdminAsync());
        Assert.Empty(await scope.ManagedOrgUnitIdsAsync());
    }

    // ── The staff directory ──────────────────────────────────────────────────

    private static UserAdminService Users(AppDbContext db, long asUser)
    {
        var (scope, me) = ScopeFor(db, asUser);
        return new UserAdminService(db, new FakePasswordHasher(), new NoopAuditWriter(), scope, me);
    }

    [Fact]
    public async Task A_head_sees_their_own_department_and_a_grant_extends_that_to_the_other()
    {
        var db = NewDb();
        var before = await Users(db, HeadA).ListUsersAsync();
        Assert.Equal(["a@x", "s@x"], before.Select(u => u.Email).Order());

        db.UserOrgUnitAccess.Add(new UserOrgUnitAccess { UserId = HeadA, OrgUnitId = DeptB });
        await db.SaveChangesAsync();

        var after = await Users(db, HeadA).ListUsersAsync();
        Assert.Equal(["a@x", "b@x", "s@x"], after.Select(u => u.Email).Order());
        // The administrator is in no department, so no grant ever brings them into view.
        Assert.DoesNotContain(after, u => u.Email == "admin@x");
    }

    [Fact]
    public async Task Granting_departments_is_refused_to_everyone_but_the_administrator()
    {
        var db = NewDb();
        // This is the escalation that matters: a head who could operate it would simply grant
        // themselves the institution, and Users.Edit is exactly what a head holds.
        var denied = await Users(db, HeadA).SetDepartmentsAsync(HeadA, new SetUserDepartmentsRequest([DeptB]));
        Assert.False(denied.Succeeded);
        Assert.Empty(db.UserOrgUnitAccess);

        var granted = await Users(db, Admin).SetDepartmentsAsync(HeadA, new SetUserDepartmentsRequest([DeptB]));
        Assert.True(granted.Succeeded);
        Assert.Equal([DeptB], granted.Value!.ExtraOrgUnitIds!);

        // Replaces rather than accumulates, so an empty list takes the grant back.
        var revoked = await Users(db, Admin).SetDepartmentsAsync(HeadA, new SetUserDepartmentsRequest([]));
        Assert.True(revoked.Succeeded);
        Assert.Empty(db.UserOrgUnitAccess);
    }

    // ── Permissions given to one person ──────────────────────────────────────

    /// <summary>Seeds a permission catalogue and gives the head of A a working set. The head holds
    /// Documents.Print and Export.Create; the employee starts with neither.</summary>
    private static void SeedPermissions(AppDbContext db)
    {
        db.Permissions.AddRange(
            new Permission { Id = 1, Code = "Documents.Print", Resource = "Documents", Action = PermissionAction.Print },
            new Permission { Id = 2, Code = "Export.Create", Resource = "Export", Action = PermissionAction.Create },
            // Held by nobody here — the thing a head must not be able to conjure.
            new Permission { Id = 3, Code = "Documents.Delete", Resource = "Documents", Action = PermissionAction.Delete },
            // Custody and staff administration: excluded outright, even from a head who holds them.
            new Permission { Id = 4, Code = "Archive.Archive", Resource = "Archive", Action = PermissionAction.Archive },
            new Permission { Id = 5, Code = "Users.Edit", Resource = "Users", Action = PermissionAction.Edit });
        // Role 2 is Manager, which both heads hold.
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = 2, PermissionId = 1 },
            new RolePermission { RoleId = 2, PermissionId = 2 },
            new RolePermission { RoleId = 2, PermissionId = 4 },
            new RolePermission { RoleId = 2, PermissionId = 5 });
        db.SaveChanges();
    }

    [Fact]
    public async Task A_head_grants_and_revokes_permissions_for_their_own_staff()
    {
        var db = NewDb();
        SeedPermissions(db);
        var svc = Users(db, HeadA);

        var granted = await svc.SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest(["Documents.Print"]));
        Assert.True(granted.Succeeded);
        Assert.Equal(["Documents.Print"], granted.Value!.ExtraPermissionCodes);

        // Replaces rather than accumulates, so dropping a code is how a permission is taken back.
        var revoked = await svc.SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest([]));
        Assert.True(revoked.Succeeded);
        Assert.Empty(revoked.Value!.ExtraPermissionCodes);
        Assert.Empty(db.UserPermissionGrants);
    }

    /// <summary>The ceiling. A head passes on what they hold and nothing else, so no chain of grants
    /// can produce an account stronger than the person who made it — and custody and staff
    /// administration are off the table even though the head's own role carries them.</summary>
    [Fact]
    public async Task A_head_can_only_pass_on_what_they_hold_and_never_custody_or_user_admin()
    {
        var db = NewDb();
        SeedPermissions(db);
        var svc = Users(db, HeadA);

        foreach (var code in new[] { "Documents.Delete", "Archive.Archive", "Users.Edit" })
        {
            var refused = await svc.SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest([code]));
            Assert.False(refused.Succeeded);
        }
        Assert.Empty(db.UserPermissionGrants);

        // The screen is told the same thing, so the limit is visible rather than discovered.
        var view = await svc.GetUserPermissionsAsync(StaffA);
        Assert.Equal(["Documents.Print", "Export.Create"], view.Value!.GrantableCodes.Order());

        // The administrator is under no such ceiling.
        var admin = await Users(db, Admin).SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest(["Documents.Delete"]));
        Assert.True(admin.Succeeded);
    }

    /// <summary>A head must not undo the administrator's exceptions while tidying their own. The
    /// grant above is outside what this head could have made, so it survives their save.</summary>
    [Fact]
    public async Task A_head_cannot_strip_a_grant_they_could_not_have_made()
    {
        var db = NewDb();
        SeedPermissions(db);
        Assert.True((await Users(db, Admin).SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest(["Documents.Delete"]))).Succeeded);

        var head = Users(db, HeadA);
        Assert.False((await head.SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest([]))).Succeeded);
        Assert.Single(db.UserPermissionGrants);

        // Keeping it while adding one of their own is fine.
        var ok = await head.SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest(["Documents.Delete", "Documents.Print"]));
        Assert.True(ok.Succeeded);
        Assert.Equal(["Documents.Delete", "Documents.Print"], ok.Value!.ExtraPermissionCodes.Order());
    }

    [Fact]
    public async Task Permissions_of_staff_outside_the_department_are_out_of_reach()
    {
        var db = NewDb();
        SeedPermissions(db);
        var svc = Users(db, HeadA);

        Assert.False((await svc.GetUserPermissionsAsync(HeadB)).Succeeded);
        Assert.False((await svc.SetUserPermissionsAsync(HeadB, new SetUserPermissionsRequest(["Documents.Print"]))).Succeeded);
        // Nor the administrator's own account, for the same reason a password reset is refused.
        Assert.False((await svc.SetUserPermissionsAsync(Admin, new SetUserPermissionsRequest(["Documents.Print"]))).Succeeded);
        Assert.Empty(db.UserPermissionGrants);
    }

    /// <summary>A granted permission has to reach the places that actually decide access: the token
    /// claims the endpoint gate reads, and the scope the services re-check against.</summary>
    [Fact]
    public async Task A_granted_permission_takes_effect_in_the_access_scope()
    {
        var db = NewDb();
        SeedPermissions(db);
        var (scope, _) = ScopeFor(db, StaffA);
        Assert.False(await scope.HasPermissionAsync("Documents.Print"));

        Assert.True((await Users(db, HeadA).SetUserPermissionsAsync(StaffA, new SetUserPermissionsRequest(["Documents.Print"]))).Succeeded);

        // A fresh scope, as the next request would build.
        Assert.True(await ScopeFor(db, StaffA).Scope.HasPermissionAsync("Documents.Print"));
    }

    // ── Sidebar sections hidden for one person ───────────────────────────────

    /// <summary>Hiding is housekeeping, so a head may do it for their own staff — but «settings» is
    /// never hideable: it is the way back to the screens that would restore anything else.</summary>
    [Fact]
    public async Task A_head_hides_sections_for_their_own_staff_but_never_settings()
    {
        var db = NewDb();
        var svc = Users(db, HeadA);

        var saved = await svc.SetUserNavSectionsAsync(StaffA, new SetUserNavSectionsRequest(["diwan", "backup", "settings"]));
        Assert.True(saved.Succeeded);
        Assert.Equal(["backup", "diwan"], saved.Value!.Hidden.Order());
        Assert.Equal("diwan,backup", db.Users.Single(u => u.Id == StaffA).HiddenNavKeys);

        // Replaces rather than accumulates, so an empty list restores the whole rail.
        var cleared = await svc.SetUserNavSectionsAsync(StaffA, new SetUserNavSectionsRequest([]));
        Assert.True(cleared.Succeeded);
        Assert.Empty(cleared.Value!.Hidden);
        Assert.Null(db.Users.Single(u => u.Id == StaffA).HiddenNavKeys);
    }

    [Fact]
    public async Task Hiding_sections_stops_at_the_department_boundary()
    {
        var db = NewDb();
        var svc = Users(db, HeadA);

        Assert.False((await svc.GetUserNavSectionsAsync(HeadB)).Succeeded);
        Assert.False((await svc.SetUserNavSectionsAsync(HeadB, new SetUserNavSectionsRequest(["diwan"]))).Succeeded);
        // Nor the administrator's rail.
        Assert.False((await svc.SetUserNavSectionsAsync(Admin, new SetUserNavSectionsRequest(["diwan"]))).Succeeded);
        Assert.Null(db.Users.Single(u => u.Id == HeadB).HiddenNavKeys);
    }

    /// <summary>What the institution hides is reported separately from what the head hid, so the
    /// screen can show it as already gone rather than offering a tick that changes nothing.</summary>
    [Fact]
    public async Task Institution_wide_hidden_sections_are_reported_apart_from_the_personal_ones()
    {
        var db = NewDb();
        db.Institutions.Single().HiddenNavKeys = "scanner";
        await db.SaveChangesAsync();

        var svc = Users(db, HeadA);
        Assert.True((await svc.SetUserNavSectionsAsync(StaffA, new SetUserNavSectionsRequest(["diwan"]))).Succeeded);

        var view = await svc.GetUserNavSectionsAsync(StaffA);
        Assert.Equal(["diwan"], view.Value!.Hidden);
        Assert.Equal(["scanner"], view.Value.HiddenForEveryone);
    }

    [Fact]
    public async Task A_head_cannot_act_on_an_account_outside_their_department()
    {
        var db = NewDb();
        var svc = Users(db, HeadA);
        Assert.False((await svc.SetActiveAsync(HeadB, false)).Succeeded);
        Assert.False((await svc.ResetPasswordAsync(HeadB, "Zq7!vBn2026x")).Succeeded);
        // An administrator's account is off limits even to a head who could otherwise reach it:
        // resetting its password would hand over the institution.
        Assert.False((await svc.ResetPasswordAsync(Admin, "Zq7!vBn2026x")).Succeeded);
        Assert.True((await svc.SetActiveAsync(StaffA, false)).Succeeded);
    }

    /// <summary>
    /// A head adds staff and only staff.
    ///
    /// Every other role reaches past the department, and Archive Officer is the sharpest case: it
    /// carries the custody right, so it sees every document in the institution. This was open —
    /// a head who could see 50 documents could create an archivist who saw 188, then sign in as
    /// them. Both the direct grant and the create-then-promote route are closed.
    /// </summary>
    [Fact]
    public async Task A_head_may_assign_the_staff_role_and_no_other()
    {
        var db = NewDb();
        db.Roles.AddRange(
            new Role { Id = 3, Name = "Employee" },
            new Role { Id = 4, Name = "Archive Officer" });
        await db.SaveChangesAsync();
        var svc = Users(db, HeadA);

        foreach (var privileged in new[] { 1L /* System Administrator */, 2L /* Manager */, 4L /* Archive Officer */ })
        {
            var created = await svc.CreateUserAsync(NewUser($"r{privileged}@x", null) with { RoleIds = [privileged] });
            Assert.False(created.Succeeded);
            var promoted = await svc.SetRolesAsync(StaffA, new SetUserRolesRequest([privileged]));
            Assert.False(promoted.Succeeded);
        }

        var staff = await svc.CreateUserAsync(NewUser("ok@x", null) with { RoleIds = [3] });
        Assert.True(staff.Succeeded);
        Assert.Equal(["Employee"], staff.Value!.Roles);

        // Naming no role must not leave an account that can reach nothing at all.
        var bare = await svc.CreateUserAsync(NewUser("bare@x", null));
        Assert.True(bare.Succeeded);
        Assert.Equal(["Employee"], bare.Value!.Roles);

        // The administrator is unaffected.
        var byAdmin = await Users(db, Admin).CreateUserAsync(NewUser("arc@x", DeptA) with { RoleIds = [4] });
        Assert.True(byAdmin.Succeeded);
        Assert.Equal(["Archive Officer"], byAdmin.Value!.Roles);
    }

    [Fact]
    public async Task A_head_cannot_grant_the_administrator_role_or_staff_another_department()
    {
        var db = NewDb();
        var svc = Users(db, HeadA);

        var escalate = await svc.SetRolesAsync(StaffA, new SetUserRolesRequest([1]));
        Assert.False(escalate.Succeeded);

        var elsewhere = await svc.CreateUserAsync(NewUser("x@x", DeptB));
        Assert.False(elsewhere.Succeeded);

        // Unstated department means their own, never "none" — a user filed under no department is
        // visible to nobody's scoped queries.
        var mine = await svc.CreateUserAsync(NewUser("y@x", null));
        Assert.True(mine.Succeeded);
        Assert.Equal(DeptA, mine.Value!.OrgUnitId);
    }

    private static CreateUserRequest NewUser(string email, long? orgUnitId) => new(
        FullName: "موظف جديد", Email: email, Password: "Zq7!vBn2026x",
        FirstName: null, SecondName: null, ThirdName: null, FamilyName: null,
        Gender: Gender.NotSpecified, NationalId: null, JobTitle: null,
        Clearance: ConfidentialityLevel.Internal, OrgUnitId: orgUnitId, RoleIds: []);

    // ── Print designs ────────────────────────────────────────────────────────

    private static ReportTemplateService Designs(AppDbContext db, long asUser)
    {
        var (scope, me) = ScopeFor(db, asUser);
        return new ReportTemplateService(db, new NoopAuditWriter(), me, scope);
    }

    private static void SeedDesigns(AppDbContext db)
    {
        db.ReportTemplates.AddRange(
            new ReportTemplate { Id = 1, InstitutionId = 1, Scope = ReportTemplateScope.Default, Name = "افتراضي" },
            new ReportTemplate { Id = 2, InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit, OrgUnitId = DeptA, Name = "ترويسة أ" },
            new ReportTemplate { Id = 3, InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit, OrgUnitId = DeptB, Name = "ترويسة ب" },
            new ReportTemplate { Id = 4, InstitutionId = 1, Scope = ReportTemplateScope.User, UserId = HeadB, Name = "ترويسة رئيس ب" });
        db.SaveChanges();
    }

    [Fact]
    public async Task A_department_sees_its_own_designs_and_the_institution_defaults_only()
    {
        var db = NewDb();
        SeedDesigns(db);

        var seen = await Designs(db, HeadA).ListAsync(null, null);
        Assert.Equal(["افتراضي", "ترويسة أ"], seen.Select(t => t.Name));
        // Opening one by id follows the list, or a hidden design opens anyway.
        Assert.False((await Designs(db, HeadA).GetAsync(3)).Succeeded);
        Assert.False((await Designs(db, HeadA).DeleteAsync(3)).Succeeded);
    }

    /// <summary>A department's design is the department's, and nothing publishes it to another —
    /// not even the administrator editing it. Something meant for the whole institution is
    /// authored at Default scope, which every department already falls back to.</summary>
    [Fact]
    public async Task A_design_is_never_published_across_departments()
    {
        var db = NewDb();
        SeedDesigns(db);

        var admin = await Designs(db, Admin).UpdateAsync(3, Save(ReportTemplateScope.OrgUnit, DeptB));
        Assert.True(admin.Succeeded);

        // Still department B's, as far as department A's head is concerned.
        var seen = await Designs(db, HeadA).ListAsync(null, null);
        Assert.DoesNotContain(seen, t => t.Id == 3);
        Assert.False((await Designs(db, HeadA).GetAsync(3)).Succeeded);
        Assert.False((await Designs(db, HeadA).UpdateAsync(3, Save(ReportTemplateScope.OrgUnit, DeptB))).Succeeded);

        // The institution default is the one thing everyone sees — and only the administrator
        // changes it.
        var fallback = Assert.Single(seen, t => t.Id == 1);
        Assert.False(fallback.CanEdit);
    }

    [Fact]
    public async Task A_head_cannot_author_a_design_for_another_department_or_the_institution()
    {
        var db = NewDb();
        var svc = Designs(db, HeadA);
        Assert.False((await svc.CreateAsync(Save(ReportTemplateScope.OrgUnit, DeptB))).Succeeded);
        Assert.False((await svc.CreateAsync(Save(ReportTemplateScope.Default, null))).Succeeded);
        Assert.False((await svc.CreateAsync(Save(ReportTemplateScope.User, null, userId: HeadB))).Succeeded);
        Assert.True((await svc.CreateAsync(Save(ReportTemplateScope.OrgUnit, DeptA))).Succeeded);
    }

    // ── The dashboard has to agree with the register it summarises ───────────

    /// <summary>Two letters: one routed to department A, one to department B and registered by
    /// B's head. Neither is anything to do with A's employee beyond the first.</summary>
    private static void SeedRegister(AppDbContext db)
    {
        db.DiwanMails.AddRange(
            new DiwanMail
            {
                Id = 1, InstitutionId = 1, RegisterNumber = "IN-2026-1", Subject = "إلى قسم أ",
                Direction = DiwanDirection.Incoming, MailDate = new DateTime(2026, 3, 1),
                RegistrationDate = new DateTime(2026, 3, 1), CreatedBy = HeadA,
                ToDiwanOrgUnits = { new DiwanBureau { OrgUnitId = DeptA } },
            },
            new DiwanMail
            {
                Id = 2, InstitutionId = 1, RegisterNumber = "IN-2026-2", Subject = "إلى قسم ب",
                Direction = DiwanDirection.Incoming, MailDate = new DateTime(2026, 3, 2),
                RegistrationDate = new DateTime(2026, 3, 2), CreatedBy = HeadB,
                ToDiwanOrgUnits = { new DiwanBureau { OrgUnitId = DeptB } },
            });
        db.SaveChanges();
    }

    private static ReportService Reports(AppDbContext db, long asUser)
    {
        var (scope, me) = ScopeFor(db, asUser);
        return new ReportService(db, me, scope);
    }

    [Fact]
    public async Task The_dashboard_counts_only_the_letters_the_caller_could_open()
    {
        var db = NewDb();
        SeedRegister(db);

        // The administrator summarises the institution.
        var admin = await Reports(db, Admin).GetDiwanDashboardAsync(2026);
        Assert.Equal(2, admin.Bureaus.Sum(b => b.Incoming + b.Outgoing));

        /* A's head sees the one letter routed to their department. Before this, the charts were
           filtered by clearance alone: every signed-in user was shown the whole institution's
           traffic, which is a leak with a nicer presentation. */
        var head = await Reports(db, HeadA).GetDiwanDashboardAsync(2026);
        Assert.Equal(1, head.Bureaus.Sum(b => b.Incoming + b.Outgoing));
        Assert.Equal(1, head.MonthlyMail.Sum(p => p.Incoming + p.Outgoing));

        // Their employee sits in the same department and is counted nothing: the letter was
        // addressed to the department, and being in it is not being handed the department's post.
        var staff = await Reports(db, StaffA).GetDiwanDashboardAsync(2026);
        Assert.Equal(0, staff.Bureaus.Sum(b => b.Incoming + b.Outgoing));

        // Let in on it, they are counted it — and still nothing of department B's.
        db.DiwanAccess.Add(new DiwanAccess { DiwanMailId = 1, UserId = StaffA, GrantedByUserId = HeadA });
        await db.SaveChangesAsync();
        var admitted = await Reports(db, StaffA).GetDiwanDashboardAsync(2026);
        Assert.Equal(1, admitted.Bureaus.Sum(b => b.Incoming + b.Outgoing));
    }

    [Fact]
    public async Task The_bureau_legend_never_names_a_department_the_caller_is_not_in()
    {
        var db = NewDb();
        // One letter reaching both departments: visible to A's head, but half of its routing is
        // somebody else's business.
        db.DiwanMails.Add(new DiwanMail
        {
            Id = 3, InstitutionId = 1, RegisterNumber = "IN-2026-3", Subject = "إلى القسمين",
            Direction = DiwanDirection.Incoming, MailDate = new DateTime(2026, 4, 1),
            RegistrationDate = new DateTime(2026, 4, 1), CreatedBy = HeadA,
            ToDiwanOrgUnits = { new DiwanBureau { OrgUnitId = DeptA }, new DiwanBureau { OrgUnitId = DeptB } },
        });
        await db.SaveChangesAsync();

        var head = await Reports(db, HeadA).GetDiwanDashboardAsync(2026);
        Assert.Contains(head.Bureaus, b => b.Name == "قسم أ");
        Assert.DoesNotContain(head.Bureaus, b => b.Name == "قسم ب");
        // The volume is not dropped, only unnamed — otherwise the chart stops matching the register.
        Assert.Contains(head.Bureaus, b => b.Name == "أقسام أخرى");
        Assert.Equal(2, head.Bureaus.Sum(b => b.Incoming + b.Outgoing));

        var admin = await Reports(db, Admin).GetDiwanDashboardAsync(2026);
        Assert.Contains(admin.Bureaus, b => b.Name == "قسم ب");
    }

    // ── من يطّلع على البريد — who reads one external letter ────────────────────

    private static DiwanService Register(AppDbContext db, long asUser)
    {
        var (scope, me) = ScopeFor(db, asUser);
        // Archiving and converting-to-internal are not exercised here, so the two collaborators
        // they need are deliberately absent: a fake for them would assert nothing.
        return new DiwanService(db, me, new NoopFileStorage(), new NoopAuditWriter(), null!, null!, scope);
    }

    private static async Task<IReadOnlyList<string>> RegisterNumbersAsync(AppDbContext db, long asUser) =>
        (await Register(db, asUser).ListAsync(new DiwanListFilter(null, null, null, null, null, null)))
            .Items.Select(i => i.RegisterNumber).Order().ToList();

    [Fact]
    public async Task An_employee_reads_a_departments_external_letter_only_once_their_head_lets_them_in()
    {
        var db = NewDb();
        SeedRegister(db);

        // The head sees what was addressed to the department they answer for.
        Assert.Equal(["IN-2026-1"], await RegisterNumbersAsync(db, HeadA));
        // Their employee sits in that department and sees none of it: routing addresses a letter
        // to a department, not to everybody in it.
        Assert.Empty(await RegisterNumbersAsync(db, StaffA));

        var granted = await Register(db, HeadA).SetAccessAsync(1, new SetDiwanAccessRequest([StaffA], "للمتابعة"));
        Assert.True(granted.Succeeded);

        Assert.Equal(["IN-2026-1"], await RegisterNumbersAsync(db, StaffA));
        // …and still nothing of the other department's, which no grant here ever touches.
        Assert.DoesNotContain("IN-2026-2", await RegisterNumbersAsync(db, StaffA));
    }

    [Fact]
    public async Task Only_the_head_of_a_department_the_letter_reached_may_say_who_reads_it()
    {
        var db = NewDb();
        SeedRegister(db);

        // An employee of the very department it went to cannot pass it on…
        var byStaff = await Register(db, StaffA).SetAccessAsync(1, new SetDiwanAccessRequest([StaffA], null));
        Assert.False(byStaff.Succeeded);

        // …and the head of another department cannot reach the letter at all, which is the same
        // answer arrived at one step earlier.
        var byOtherHead = await Register(db, HeadB).SetAccessAsync(1, new SetDiwanAccessRequest([StaffA], null));
        Assert.False(byOtherHead.Succeeded);
        Assert.Empty(db.DiwanAccess);
    }

    [Fact]
    public async Task A_head_can_only_admit_their_own_staff_and_revoking_takes_the_letter_back()
    {
        var db = NewDb();
        SeedRegister(db);

        // HeadB is a real user and a valid id — but not one of A's, so the grant is dropped rather
        // than trusted: the candidate list is the rule, not just the screen that renders it.
        var r = await Register(db, HeadA).SetAccessAsync(1, new SetDiwanAccessRequest([StaffA, HeadB], null));
        Assert.True(r.Succeeded);
        Assert.Equal([StaffA], db.DiwanAccess.Select(a => a.UserId).ToList());

        // The panel shows the access that came with the letter beside the access that was given.
        var view = await Register(db, HeadA).GetAccessAsync(1);
        Assert.True(view.Value!.CanManage);
        Assert.Contains(view.Value!.Entries, e => e.UserId == HeadA && e.Reason is "Registrar" or "Head");
        Assert.Contains(view.Value!.Entries, e => e.UserId == StaffA && e.Reason == "Granted");
        // The head chooses from their own department only.
        Assert.All(view.Value!.Candidates, c => Assert.Contains(c.UserId, new[] { HeadA, StaffA }));

        // Naming nobody takes it back, and the letter goes with it.
        Assert.True((await Register(db, HeadA).SetAccessAsync(1, new SetDiwanAccessRequest([], null))).Succeeded);
        Assert.Empty(await RegisterNumbersAsync(db, StaffA));
    }

    [Fact]
    public async Task An_employee_who_was_let_in_cannot_hand_the_letter_on()
    {
        var db = NewDb();
        SeedRegister(db);
        db.DiwanAccess.Add(new DiwanAccess { DiwanMailId = 1, UserId = StaffA, GrantedByUserId = HeadA });
        await db.SaveChangesAsync();

        // They can read it — and read who else can, which the register shows anyone it shows the
        // letter to — but the panel is theirs to look at, not to edit.
        var view = await Register(db, StaffA).GetAccessAsync(1);
        Assert.True(view.Succeeded);
        Assert.False(view.Value!.CanManage);
        Assert.Empty(view.Value!.Candidates);

        var passedOn = await Register(db, StaffA).SetAccessAsync(1, new SetDiwanAccessRequest([StaffA, HeadA], null));
        Assert.False(passedOn.Succeeded);
    }

    [Fact]
    public async Task The_disposal_tile_counts_what_its_own_screen_would_list()
    {
        var db = NewDb();
        // Grant both heads the approval right the tile used to gate on, so the only thing left
        // deciding the number is department scope.
        db.Permissions.Add(new Permission { Id = 1, Resource = "Disposition", Action = PermissionAction.Approve, Code = "Disposition.Approve" });
        db.RolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = 1 });
        db.DispositionRequests.AddRange(
            new DispositionRequest { Id = 1, RequestedByUserId = StaffA, Status = DispositionStatus.PendingVerification },
            new DispositionRequest { Id = 2, RequestedByUserId = HeadB, Status = DispositionStatus.PendingVerification },
            new DispositionRequest { Id = 3, RequestedByUserId = HeadB, Status = DispositionStatus.PendingFinalApproval });
        await db.SaveChangesAsync();

        /* A's head sees the one their own staff raised — not all three. The tile used to gate on
           the permission alone and report the institution's total, so a head was shown a queue of
           requests and then an empty screen when they clicked it. */
        var a = await Reports(db, HeadA).GetDashboardAsync();
        Assert.Equal(1, a.PendingDisposals);

        var b = await Reports(db, HeadB).GetDashboardAsync();
        Assert.Equal(2, b.PendingDisposals);

        // An employee sees only what they raised themselves, permission or not.
        var staff = await Reports(db, StaffA).GetDashboardAsync();
        Assert.Equal(1, staff.PendingDisposals);

        var admin = await Reports(db, Admin).GetDashboardAsync();
        Assert.Equal(3, admin.PendingDisposals);
    }

    private static SaveReportTemplateRequest Save(
        ReportTemplateScope scope, long? orgUnitId, long? userId = null) => new(
            Scope: scope, OrgUnitId: orgUnitId, UserId: userId,
            Target: ReportTemplateTarget.Document, Name: "تصميم",
            LogoBase64: null, LayoutJson: "[]", IsActive: true);
}
