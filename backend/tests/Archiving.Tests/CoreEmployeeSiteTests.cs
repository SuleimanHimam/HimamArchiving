using System.Reflection;
using Archiving.Api.Authorization;
using Archiving.Api.Controllers.Core;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Core;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Configurations;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Services.Core;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

/// <summary>The shared core: the employee register and work sites. All data here is generated.</summary>
public class CoreEmployeeSiteTests
{
    private const long UnitA = 10, UnitB = 20;
    private const long UserOne = 100, UserTwo = 200;

    private sealed class FakeScope(bool admin, params long[] units) : IAccessScope
    {
        public Task<bool> IsSystemAdminAsync(CancellationToken ct = default) => Task.FromResult(admin);
        public Task<bool> HasPermissionAsync(string code, CancellationToken ct = default) => Task.FromResult(admin);
        public Task<long?> OrgUnitIdAsync(CancellationToken ct = default) => Task.FromResult(units.Length > 0 ? units[0] : (long?)null);
        public Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(units);
        public Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([]);
        public Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default) => Task.FromResult(admin);
    }

    private static AppDbContext NewDb()
    {
        // The interceptor is what turns Remove() into a soft delete, so it is part of the behaviour under test.
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor(new TestCurrentUser()))
            .Options);

        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.OrgUnits.Add(new OrgUnit { Id = UnitA, InstitutionId = 1, Name = "دائرة أ" });
        db.OrgUnits.Add(new OrgUnit { Id = UnitB, InstitutionId = 1, Name = "دائرة ب" });
        db.Users.Add(new User { Id = UserOne, FullName = "مستخدم تجريبي ١", Email = "one@test.invalid" });
        db.Users.Add(new User { Id = UserTwo, FullName = "مستخدم تجريبي ٢", Email = "two@test.invalid" });
        db.SaveChanges();
        return db;
    }

    private static EmployeeService Employees(AppDbContext db, bool admin = true, params long[] units) =>
        new(db, new NoopAuditWriter(), new FakeScope(admin, units));

    private static SiteService Sites(AppDbContext db) => new(db, new NoopAuditWriter());

    private static SaveEmployeeRequest Request(string number = "T-0001", long unit = UnitA, long? user = null,
        long? site = null, string category = "Civil", string? rowVersion = null) =>
        new(number, "موظف تجريبي", null, category, unit, null, "محاسب", site, new DateOnly(2020, 1, 15), user, rowVersion);

    // ---- Employees ----

    [Fact]
    public async Task Create_stores_an_active_employee()
    {
        using var db = NewDb();
        var r = await Employees(db).CreateAsync(Request());

        Assert.True(r.Succeeded, r.Error);
        Assert.Equal("T-0001", r.Value!.EmployeeNumber);
        Assert.Equal("Active", r.Value.Status);
        Assert.Equal("دائرة أ", r.Value.OrgUnitName);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_employee_number()
    {
        using var db = NewDb();
        var svc = Employees(db);
        await svc.CreateAsync(Request());

        var r = await svc.CreateAsync(Request());

        Assert.False(r.Succeeded);
        Assert.Equal(1, await db.Employees.CountAsync());
    }

    [Theory]
    [InlineData("", "موظف", "محاسب", "Civil")]          // number required
    [InlineData("T-1", "", "محاسب", "Civil")]            // name required
    [InlineData("T-1", "موظف", "", "Civil")]             // job title required
    [InlineData("T-1", "موظف", "محاسب", "Volunteer")]    // unknown category
    [InlineData("T-1", "موظف", "محاسب", "7")]            // numeric value outside the enum
    public async Task Create_rejects_invalid_input(string number, string name, string title, string category)
    {
        using var db = NewDb();
        var r = await Employees(db).CreateAsync(
            new SaveEmployeeRequest(number, name, null, category, UnitA, null, title, null, null, null));

        Assert.False(r.Succeeded);
        Assert.Equal(0, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_references_that_do_not_exist()
    {
        using var db = NewDb();
        var svc = Employees(db);

        Assert.False((await svc.CreateAsync(Request(unit: 999))).Succeeded);
        Assert.False((await svc.CreateAsync(Request(site: 999))).Succeeded);
        Assert.False((await svc.CreateAsync(Request(user: 999))).Succeeded);
    }

    [Fact]
    public async Task A_user_account_links_to_one_employee_only()
    {
        using var db = NewDb();
        var svc = Employees(db);
        Assert.True((await svc.CreateAsync(Request("T-0001", user: UserOne))).Succeeded);

        var second = await svc.CreateAsync(Request("T-0002", user: UserOne));

        Assert.False(second.Succeeded);
        Assert.True((await svc.CreateAsync(Request("T-0002", user: UserTwo))).Succeeded);
    }

    [Fact]
    public async Task Update_with_a_stale_or_missing_row_version_is_refused()
    {
        using var db = NewDb();
        var svc = Employees(db);
        var created = (await svc.CreateAsync(Request())).Value!;

        // Someone else saves in between: the stored token moves on.
        var row = await db.Employees.FirstAsync(e => e.Id == created.Id);
        row.RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        await db.SaveChangesAsync();

        var stale = await svc.UpdateAsync(created.Id, Request(rowVersion: created.RowVersion) with { JobTitle = "مدقق" });
        var missing = await svc.UpdateAsync(created.Id, Request(rowVersion: null) with { JobTitle = "مدقق" });
        var garbage = await svc.UpdateAsync(created.Id, Request(rowVersion: "not base64!") with { JobTitle = "مدقق" });

        Assert.False(stale.Succeeded);
        Assert.False(missing.Succeeded);
        Assert.False(garbage.Succeeded);
        Assert.Equal("محاسب", (await db.Employees.AsNoTracking().FirstAsync(e => e.Id == created.Id)).JobTitle);

        var current = await svc.UpdateAsync(created.Id,
            Request(rowVersion: Convert.ToBase64String(row.RowVersion)) with { JobTitle = "مدقق" });
        Assert.True(current.Succeeded, current.Error);
        Assert.Equal("مدقق", current.Value!.JobTitle);
    }

    [Fact]
    public async Task Update_does_not_change_status()
    {
        using var db = NewDb();
        var svc = Employees(db);
        var created = (await svc.CreateAsync(Request())).Value!;
        var row = await db.Employees.FirstAsync(e => e.Id == created.Id);
        row.Status = EmployeeStatus.Suspended;   // as an approved personnel action would
        await db.SaveChangesAsync();

        var r = await svc.UpdateAsync(created.Id, Request(rowVersion: created.RowVersion) with { JobTitle = "مدقق" });

        Assert.True(r.Succeeded, r.Error);
        Assert.Equal("Suspended", r.Value!.Status);
    }

    [Fact]
    public async Task Delete_is_soft_and_the_number_is_never_reused()
    {
        using var db = NewDb();
        var svc = Employees(db);
        var created = (await svc.CreateAsync(Request())).Value!;

        Assert.True((await svc.DeleteAsync(created.Id)).Succeeded);

        Assert.Equal(0, await db.Employees.CountAsync());                       // hidden from normal reads
        var kept = await db.Employees.IgnoreQueryFilters().SingleAsync();        // still on record
        Assert.True(kept.IsDeleted);
        Assert.NotNull(kept.DeletedAt);
        Assert.Equal(5, kept.DeletedBy);
        Assert.False((await svc.CreateAsync(Request())).Succeeded);              // same number refused
    }

    [Fact]
    public async Task A_non_admin_sees_only_their_own_units()
    {
        using var db = NewDb();
        var admin = Employees(db);
        var inA = (await admin.CreateAsync(Request("T-0001", UnitA))).Value!;
        var inB = (await admin.CreateAsync(Request("T-0002", UnitB))).Value!;

        var scoped = Employees(db, admin: false, UnitA);
        var list = await scoped.ListAsync(new EmployeeListQuery());

        Assert.Equal(["T-0001"], list.Items.Select(e => e.EmployeeNumber));
        Assert.True((await scoped.GetAsync(inA.Id)).Succeeded);
        Assert.False((await scoped.GetAsync(inB.Id)).Succeeded);   // out of scope reads as "not found"
        Assert.Equal(2, (await admin.ListAsync(new EmployeeListQuery())).TotalCount);
    }

    [Fact]
    public async Task List_filters_and_pages()
    {
        using var db = NewDb();
        var svc = Employees(db);
        for (var i = 1; i <= 5; i++)
            await svc.CreateAsync(Request($"T-000{i}", i % 2 == 0 ? UnitB : UnitA, category: i == 5 ? "Contract" : "Civil"));

        Assert.Equal(2, (await svc.ListAsync(new EmployeeListQuery(OrgUnitId: UnitB))).TotalCount);
        Assert.Equal(1, (await svc.ListAsync(new EmployeeListQuery(Category: "contract"))).TotalCount);
        Assert.Equal(1, (await svc.ListAsync(new EmployeeListQuery(Search: "T-0003"))).TotalCount);

        var page2 = await svc.ListAsync(new EmployeeListQuery(Page: 2, PageSize: 2));
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(["T-0003", "T-0004"], page2.Items.Select(e => e.EmployeeNumber));
    }

    // ---- Sites ----

    [Fact]
    public async Task Site_codes_are_unique_and_locations_are_unique_within_a_site()
    {
        using var db = NewDb();
        var svc = Sites(db);
        var a = (await svc.CreateAsync(new SaveSiteRequest("موقع تجريبي أ", null, "SA"))).Value!;
        var b = (await svc.CreateAsync(new SaveSiteRequest("موقع تجريبي ب", null, "SB"))).Value!;

        Assert.False((await svc.CreateAsync(new SaveSiteRequest("مكرر", null, "SA"))).Succeeded);
        Assert.True((await svc.CreateLocationAsync(a.Id, new SaveSiteLocationRequest("بوابة ١", null, "G1", "Gate"))).Succeeded);
        Assert.False((await svc.CreateLocationAsync(a.Id, new SaveSiteLocationRequest("بوابة مكررة", null, "G1", "Gate"))).Succeeded);
        Assert.True((await svc.CreateLocationAsync(b.Id, new SaveSiteLocationRequest("بوابة ١", null, "G1", "Gate"))).Succeeded);
        Assert.False((await svc.CreateLocationAsync(a.Id, new SaveSiteLocationRequest("غير صالح", null, "X1", "Spaceport"))).Succeeded);
        Assert.False((await svc.CreateLocationAsync(999, new SaveSiteLocationRequest("بلا موقع", null, "X2", "Gate"))).Succeeded);
    }

    [Fact]
    public async Task A_site_in_use_cannot_be_deleted()
    {
        using var db = NewDb();
        var sites = Sites(db);
        var staffed = (await sites.CreateAsync(new SaveSiteRequest("موقع فيه موظفون", null, "S1"))).Value!;
        var withLocation = (await sites.CreateAsync(new SaveSiteRequest("موقع فيه مواقع فرعية", null, "S2"))).Value!;
        var empty = (await sites.CreateAsync(new SaveSiteRequest("موقع فارغ", null, "S3"))).Value!;
        await Employees(db).CreateAsync(Request(site: staffed.Id));
        await sites.CreateLocationAsync(withLocation.Id, new SaveSiteLocationRequest("مستودع", null, "W1", "Warehouse"));

        Assert.False((await sites.DeleteAsync(staffed.Id)).Succeeded);
        Assert.False((await sites.DeleteAsync(withLocation.Id)).Succeeded);
        Assert.True((await sites.DeleteAsync(empty.Id)).Succeeded);
        Assert.True((await db.Sites.IgnoreQueryFilters().SingleAsync(s => s.Id == empty.Id)).IsDeleted);
    }

    [Fact]
    public async Task Site_update_needs_the_current_row_version()
    {
        using var db = NewDb();
        var svc = Sites(db);
        var site = (await svc.CreateAsync(new SaveSiteRequest("موقع تجريبي", null, "S1"))).Value!;

        Assert.False((await svc.UpdateAsync(site.Id, new SaveSiteRequest("اسم جديد", null, "S1"))).Succeeded);

        var ok = await svc.UpdateAsync(site.Id, new SaveSiteRequest("اسم جديد", null, "S1", RowVersion: site.RowVersion));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal("اسم جديد", ok.Value!.Name);
    }

    // ---- Conventions the platform standard requires ----

    [Theory]
    [InlineData("EmployeeNumber", "employee_number")]
    [InlineData("OrgUnitId", "org_unit_id")]
    [InlineData("Id", "id")]
    [InlineData("IsDeleted", "is_deleted")]
    [InlineData("RowVersion", "row_version")]
    public void Column_names_are_snake_case(string property, string column) =>
        Assert.Equal(column, PlatformTable.ToSnakeCase(property));

    [Theory]
    [InlineData(typeof(Employee), "employees")]
    [InlineData(typeof(Site), "sites")]
    [InlineData(typeof(SiteLocation), "site_locations")]
    public void Core_tables_carry_the_mandatory_columns(Type entity, string table)
    {
        using var db = NewDb();
        var type = db.Model.FindEntityType(entity)!;

        Assert.Equal(table, type.GetTableName());
        Assert.Equal("core", type.GetSchema());

        var columns = type.GetProperties().Select(p => p.GetColumnName()).ToHashSet();
        string[] mandatory = ["id", "created_at", "created_by", "updated_at", "updated_by", "deleted_at", "deleted_by", "row_version"];
        Assert.All(mandatory, c => Assert.Contains(c, columns));
        Assert.All(columns, c => Assert.Equal(c.ToLowerInvariant(), c));   // nothing left in PascalCase
        Assert.True(type.FindProperty(nameof(CoreEntity.RowVersion))!.IsConcurrencyToken);
    }

    // ---- Every endpoint declares its permission, and the permission exists ----

    [Theory]
    [InlineData(typeof(EmployeesController))]
    [InlineData(typeof(SitesController))]
    public void Every_action_requires_a_catalogued_permission(Type controller)
    {
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();
        Assert.NotEmpty(actions);

        foreach (var action in actions)
        {
            var permission = action.GetCustomAttribute<HasPermissionAttribute>();
            Assert.True(permission is not null, $"{controller.Name}.{action.Name} has no [HasPermission]");

            var code = permission!.Policy![HasPermissionAttribute.PolicyPrefix.Length..];
            Assert.Contains(code, PermissionCatalog.AllCodes);
        }
    }

    [Fact]
    public void Write_actions_need_a_write_permission()
    {
        foreach (var controller in new[] { typeof(EmployeesController), typeof(SitesController) })
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var verb = action.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault()?.HttpMethods.Single();
            if (verb is null or "GET") continue;

            var code = action.GetCustomAttribute<HasPermissionAttribute>()!.Policy!;
            Assert.False(code.EndsWith(".View"), $"{controller.Name}.{action.Name} ({verb}) is guarded only by a View permission");
        }
    }
}
