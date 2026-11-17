using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Domain.Core;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Core;

/// <summary>
/// The core employee register.
///
/// <para><b>Row scope.</b> A system administrator sees every employee. Anyone else sees the employees
/// of the units they belong to or manage. The platform's wider scope model (own unit / a named site /
/// the whole authority, per role) is not built yet; until it is, this is the narrow default — it can
/// under-show to an HR officer, and never over-shows.</para>
/// </summary>
public sealed class EmployeeService(AppDbContext db, IAuditWriter audit, IAccessScope scope) : IEmployeeService
{
    private const string NotFound = "الموظف غير موجود";
    private const int MaxPageSize = 200;

    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);

        if (q.OrgUnitId is { } unitId) query = query.Where(e => e.OrgUnitId == unitId);
        if (q.SiteId is { } siteId) query = query.Where(e => e.SiteId == siteId);
        if (Enum.TryParse<EmployeeCategory>(q.Category, ignoreCase: true, out var category))
            query = query.Where(e => e.Category == category);
        if (Enum.TryParse<EmployeeStatus>(q.Status, ignoreCase: true, out var status))
            query = query.Where(e => e.Status == status);
        if (CoreGuards.CleanOrNull(q.Search) is { } term)
            query = query.Where(e => e.FullName.Contains(term) || e.EmployeeNumber.Contains(term) || e.JobTitle.Contains(term));

        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(e => e.EmployeeNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<EmployeeDto>
        {
            Items = await ToDtosAsync(rows, ct), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<Result<EmployeeDto>> GetAsync(long id, CancellationToken ct = default)
    {
        // Out of scope and missing answer alike, so the endpoint cannot be used to probe for ids.
        var e = await (await ScopedAsync(ct)).FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? Result<EmployeeDto>.Fail(NotFound) : Result<EmployeeDto>.Ok((await ToDtosAsync([e], ct))[0]);
    }

    public async Task<Result<EmployeeDto>> CreateAsync(SaveEmployeeRequest r, CancellationToken ct = default)
    {
        var (fields, error) = await ValidateAsync(r, existingId: null, ct);
        if (fields is null) return Result<EmployeeDto>.Fail(error!);

        var e = new Employee();
        Apply(e, fields);
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);

        var dto = (await ToDtosAsync([e], ct))[0];
        await audit.WriteAsync("Created", "Employee", e.Id, e.FullName, newValues: CoreGuards.Json(dto), ct: ct);
        return Result<EmployeeDto>.Ok(dto);
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(long id, SaveEmployeeRequest r, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<EmployeeDto>.Fail(NotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<EmployeeDto>.Fail(CoreGuards.Stale);

        var (fields, error) = await ValidateAsync(r, existingId: id, ct);
        if (fields is null) return Result<EmployeeDto>.Fail(error!);

        var before = CoreGuards.Json((await ToDtosAsync([e], ct))[0]);
        Apply(e, fields);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<EmployeeDto>.Fail(CoreGuards.Stale); }

        var dto = (await ToDtosAsync([e], ct))[0];
        await audit.WriteAsync("Updated", "Employee", e.Id, e.FullName, before, CoreGuards.Json(dto), ct);
        return Result<EmployeeDto>.Ok(dto);
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail(NotFound);

        db.Employees.Remove(e);   // soft delete via the interceptor; the row stays for the record
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Employee", id, e.FullName, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- internals ----

    private sealed record Fields(
        string Number, string Name, string? NameEn, EmployeeCategory Category, long OrgUnitId,
        long? PositionId, string JobTitle, long? SiteId, DateOnly? HireDate, long? UserId);

    private static void Apply(Employee e, Fields f)
    {
        e.EmployeeNumber = f.Number;
        e.FullName = f.Name;
        e.FullNameEn = f.NameEn;
        e.Category = f.Category;
        e.OrgUnitId = f.OrgUnitId;
        e.PositionId = f.PositionId;
        e.JobTitle = f.JobTitle;
        e.SiteId = f.SiteId;
        e.HireDate = f.HireDate;
        e.UserId = f.UserId;
    }

    private async Task<(Fields? Fields, string? Error)> ValidateAsync(SaveEmployeeRequest r, long? existingId, CancellationToken ct)
    {
        var number = CoreGuards.Clean(r.EmployeeNumber);
        var name = CoreGuards.Clean(r.FullName);
        var title = CoreGuards.Clean(r.JobTitle);

        if (number.Length is 0 or > 32) return (null, "الرقم الوظيفي مطلوب ولا يتجاوز 32 حرفاً");
        if (name.Length is 0 or > 200) return (null, "اسم الموظف مطلوب ولا يتجاوز 200 حرف");
        if (title.Length is 0 or > 150) return (null, "المسمى الوظيفي مطلوب ولا يتجاوز 150 حرفاً");
        if (CoreGuards.Clean(r.FullNameEn).Length > 200) return (null, "الاسم بالإنجليزية أطول من 200 حرف");
        if (!Enum.TryParse<EmployeeCategory>(r.Category, ignoreCase: true, out var category) || !Enum.IsDefined(category))
            return (null, "فئة الموظف غير صالحة");
        if (r.HireDate is { } hired && hired > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            return (null, "تاريخ التعيين لا يكون في المستقبل");

        // Includes soft-deleted rows: the unique index does, and a number is never reused.
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeNumber == number && e.Id != existingId, ct))
            return (null, "الرقم الوظيفي مستخدم");
        if (!await db.OrgUnits.AnyAsync(u => u.Id == r.OrgUnitId, ct))
            return (null, "الوحدة التنظيمية غير موجودة");
        if (r.PositionId is { } positionId && !await db.Positions.AnyAsync(p => p.Id == positionId, ct))
            return (null, "المقعد الوظيفي غير موجود");
        if (r.SiteId is { } siteId && !await db.Sites.AnyAsync(s => s.Id == siteId, ct))
            return (null, "مكان العمل غير موجود");
        if (r.UserId is { } userId)
        {
            if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return (null, "المستخدم غير موجود");
            if (await db.Employees.AnyAsync(e => e.UserId == userId && e.Id != existingId, ct))
                return (null, "المستخدم مرتبط بموظف آخر");
        }

        return (new Fields(number, name, CoreGuards.CleanOrNull(r.FullNameEn), category, r.OrgUnitId,
            r.PositionId, title, r.SiteId, r.HireDate, r.UserId), null);
    }

    private Task<IQueryable<Employee>> ScopedAsync(CancellationToken ct) => ScopedQueryAsync(db, scope, ct);

    /// <summary>The employees the caller may see. Shared with the HR services so every employee-facing
    /// endpoint applies the same row scope.</summary>
    internal static async Task<IQueryable<Employee>> ScopedQueryAsync(AppDbContext db, IAccessScope scope, CancellationToken ct)
    {
        var query = db.Employees.AsNoTracking();
        if (await scope.IsSystemAdminAsync(ct)) return query;

        var units = (await scope.OrgUnitIdsAsync(ct)).Concat(await scope.ManagedOrgUnitIdsAsync(ct)).Distinct().ToList();
        return query.Where(e => units.Contains(e.OrgUnitId));
    }

    private async Task<IReadOnlyList<EmployeeDto>> ToDtosAsync(IReadOnlyList<Employee> rows, CancellationToken ct)
    {
        var unitIds = rows.Select(e => e.OrgUnitId).Distinct().ToList();
        var siteIds = rows.Where(e => e.SiteId is not null).Select(e => e.SiteId!.Value).Distinct().ToList();

        var units = await db.OrgUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var sites = await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return rows.Select(e => new EmployeeDto(
            e.Id, e.EmployeeNumber, e.FullName, e.FullNameEn, e.Category.ToString(), e.Status.ToString(),
            e.OrgUnitId, units.GetValueOrDefault(e.OrgUnitId), e.PositionId, e.JobTitle,
            e.SiteId, e.SiteId is { } sid ? sites.GetValueOrDefault(sid) : null,
            e.HireDate, e.UserId, Convert.ToBase64String(e.RowVersion))).ToList();
    }
}
