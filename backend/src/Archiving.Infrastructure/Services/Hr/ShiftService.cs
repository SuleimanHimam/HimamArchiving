using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Core;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// Shift types, coverage minimums and the monthly roster of a site (presentation slide 14): employee × day,
/// a coverage row against the minimum, leave cells written only by approved leave, publish with notice.
/// </summary>
public sealed class ShiftService(AppDbContext db, IAccessScope scope, IAuditWriter audit) : IShiftService
{
    public async Task<IReadOnlyList<ShiftTypeDto>> TypesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.ShiftTypes.AsNoTracking();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return (await q.OrderBy(t => t.StartTime).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<Result<ShiftTypeDto>> CreateTypeAsync(SaveShiftTypeRequest r, CancellationToken ct = default)
    {
        var t = new ShiftType();
        if (await ApplyAsync(t, r, null, ct) is { } error) return Result<ShiftTypeDto>.Fail(error);
        db.ShiftTypes.Add(t);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "ShiftType", t.Id, t.Name, ct: ct);
        return Result<ShiftTypeDto>.Ok(ToDto(t));
    }

    public async Task<Result<ShiftTypeDto>> UpdateTypeAsync(long id, SaveShiftTypeRequest r, CancellationToken ct = default)
    {
        var t = await db.ShiftTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<ShiftTypeDto>.Fail("المناوبة غير موجودة");
        if (!CoreGuards.IsCurrent(t.RowVersion, r.RowVersion)) return Result<ShiftTypeDto>.Fail(CoreGuards.Stale);
        var before = CoreGuards.Json(ToDto(t));
        if (await ApplyAsync(t, r, id, ct) is { } error) return Result<ShiftTypeDto>.Fail(error);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ShiftTypeDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "ShiftType", t.Id, t.Name, before, CoreGuards.Json(ToDto(t)), ct);
        return Result<ShiftTypeDto>.Ok(ToDto(t));
    }

    private async Task<string?> ApplyAsync(ShiftType t, SaveShiftTypeRequest r, long? id, CancellationToken ct)
    {
        var code = CoreGuards.Clean(r.Code).ToUpperInvariant();
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32) return "الرمز مطلوب ولا يتجاوز 32 حرفاً";
        if (name.Length is 0 or > 100) return "الاسم مطلوب ولا يتجاوز 100 حرف";
        if (!HrCommon.TryTime(r.StartTime, out var start) || !HrCommon.TryTime(r.EndTime, out var end)) return "وقت البداية أو النهاية غير صالح";
        if (start == end) return "البداية والنهاية متساويتان";
        if (r.GraceMinutes is < 0 or > 240) return "دقائق السماح بين 0 و240";
        if (await db.ShiftTypes.IgnoreQueryFilters().AnyAsync(x => x.Code == code && x.Id != (id ?? 0), ct)) return "الرمز مستخدم";
        t.Code = code; t.Name = name; t.StartTime = start; t.EndTime = end;
        t.GraceMinutes = r.GraceMinutes; t.IsNight = r.IsNight; t.IsActive = r.IsActive;
        return null;
    }

    private static ShiftTypeDto ToDto(ShiftType t) => new(t.Id, t.Code, t.Name, t.StartTime.ToString("HH:mm"),
        t.EndTime.ToString("HH:mm"), t.GraceMinutes, t.IsNight, t.IsActive, HrCommon.B64(t.RowVersion));

    // ---- coverage ----

    public async Task<IReadOnlyList<CoverageRuleDto>> CoverageRulesAsync(long siteId, CancellationToken ct = default) =>
        await db.CoverageRules.AsNoTracking().Where(c => c.SiteId == siteId)
            .Select(c => new CoverageRuleDto(c.Id, c.SiteId, c.ShiftTypeId, c.MinCount)).ToListAsync(ct);

    public async Task<Result<CoverageRuleDto>> SaveCoverageRuleAsync(SaveCoverageRuleRequest r, CancellationToken ct = default)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == r.SiteId, ct)) return Result<CoverageRuleDto>.Fail("الموقع غير موجود");
        if (!await db.ShiftTypes.AnyAsync(s => s.Id == r.ShiftTypeId, ct)) return Result<CoverageRuleDto>.Fail("المناوبة غير موجودة");
        if (r.MinCount is < 0 or > 1000) return Result<CoverageRuleDto>.Fail("الحد الأدنى غير صالح");
        var rule = await db.CoverageRules.FirstOrDefaultAsync(c => c.SiteId == r.SiteId && c.ShiftTypeId == r.ShiftTypeId, ct);
        if (rule is null) db.CoverageRules.Add(rule = new CoverageRule { SiteId = r.SiteId, ShiftTypeId = r.ShiftTypeId });
        rule.MinCount = r.MinCount;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", "CoverageRule", rule.Id, $"site {r.SiteId} shift {r.ShiftTypeId}", newValues: $"{r.MinCount}", ct: ct);
        return Result<CoverageRuleDto>.Ok(new CoverageRuleDto(rule.Id, rule.SiteId, rule.ShiftTypeId, rule.MinCount));
    }

    // ---- roster ----

    public async Task<Result<RosterDto>> RosterAsync(long siteId, int year, int month, CancellationToken ct = default)
    {
        if (!ValidMonth(year, month)) return Result<RosterDto>.Fail("الشهر غير صالح");
        if (!await db.Sites.AnyAsync(s => s.Id == siteId, ct)) return Result<RosterDto>.Fail("الموقع غير موجود");
        return Result<RosterDto>.Ok(await BuildAsync(siteId, year, month, ct));
    }

    public async Task<Result<RosterDto>> SaveCellsAsync(long siteId, int year, int month, SaveRosterCellsRequest r, CancellationToken ct = default)
    {
        if (!ValidMonth(year, month)) return Result<RosterDto>.Fail("الشهر غير صالح");
        if (r.Cells is null || r.Cells.Count is 0 or > 5000) return Result<RosterDto>.Fail("لا توجد تغييرات أو العدد كبير");
        var (first, last) = MonthRange(year, month);
        var staff = (await SiteStaffAsync(siteId, ct)).Select(e => e.Id).ToHashSet();
        var types = await db.ShiftTypes.AsNoTracking().Where(t => t.IsActive).Select(t => t.Id).ToListAsync(ct);
        var empIds = r.Cells.Select(c => c.EmployeeId).Distinct().ToList();
        var existing = await db.ShiftAssignments.Where(a => empIds.Contains(a.EmployeeId) && a.Date >= first && a.Date <= last)
            .ToDictionaryAsync(a => (a.EmployeeId, a.Date), ct);

        foreach (var c in r.Cells)
        {
            if (!staff.Contains(c.EmployeeId)) return Result<RosterDto>.Fail("موظف خارج هذا الموقع أو خارج نطاقك");
            if (c.Date < first || c.Date > last) return Result<RosterDto>.Fail("تاريخ خارج الشهر");
            existing.TryGetValue((c.EmployeeId, c.Date), out var cell);
            if (cell?.Kind == RosterCellKind.Leave) return Result<RosterDto>.Fail("خلية الإجازة مقفلة: تتغير بإلغاء الإجازة فقط");

            switch (c.Kind)
            {
                case "Clear":
                    if (cell is not null) db.ShiftAssignments.Remove(cell);
                    break;
                case nameof(RosterCellKind.Rest):
                case nameof(RosterCellKind.Shift):
                    var kind = Enum.Parse<RosterCellKind>(c.Kind);
                    if (kind == RosterCellKind.Shift && (c.ShiftTypeId is not { } st || !types.Contains(st)))
                        return Result<RosterDto>.Fail("المناوبة غير موجودة أو غير مفعّلة");
                    if (cell is null)
                    {
                        cell = new ShiftAssignment { EmployeeId = c.EmployeeId, Date = c.Date };
                        db.ShiftAssignments.Add(cell);
                        existing[(c.EmployeeId, c.Date)] = cell;
                    }
                    cell.Kind = kind;
                    cell.ShiftTypeId = kind == RosterCellKind.Shift ? c.ShiftTypeId : null;
                    cell.SiteId = siteId;
                    break;
                default:
                    return Result<RosterDto>.Fail("نوع الخلية غير صالح");
            }
        }

        // An edit after publishing puts the month back to draft until it is published (and announced) again.
        var roster = await db.ShiftRosters.FirstOrDefaultAsync(x => x.SiteId == siteId && x.Year == year && x.Month == month, ct);
        if (roster is { Status: RosterStatus.Published }) roster.Status = RosterStatus.Draft;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", "ShiftRoster", siteId, $"{year}-{month:00}", newValues: $"{r.Cells.Count} cells", ct: ct);
        return Result<RosterDto>.Ok(await BuildAsync(siteId, year, month, ct));
    }

    public async Task<Result<RosterDto>> PublishAsync(long siteId, int year, int month, CancellationToken ct = default)
    {
        if (!ValidMonth(year, month)) return Result<RosterDto>.Fail("الشهر غير صالح");
        if (!await db.Sites.AnyAsync(s => s.Id == siteId, ct)) return Result<RosterDto>.Fail("الموقع غير موجود");
        var roster = await db.ShiftRosters.FirstOrDefaultAsync(x => x.SiteId == siteId && x.Year == year && x.Month == month, ct);
        if (roster is null) db.ShiftRosters.Add(roster = new ShiftRoster { SiteId = siteId, Year = year, Month = month });
        roster.Status = RosterStatus.Published;
        roster.PublishedAt = DateTime.UtcNow;

        var (first, last) = MonthRange(year, month);
        var staff = await SiteStaffAsync(siteId, ct);
        var onRoster = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.SiteId == siteId && a.Date >= first && a.Date <= last).Select(a => a.EmployeeId).Distinct().ToListAsync(ct);
        foreach (var e in staff.Where(e => e.UserId is not null && onRoster.Contains(e.Id)))
            HrCommon.Notify(db, e.UserId!.Value, "نُشر جدول المناوبات", $"جدولك لشهر {year}-{month:00}", "ShiftRoster", siteId);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Published", "ShiftRoster", siteId, $"{year}-{month:00}", ct: ct);
        return Result<RosterDto>.Ok(await BuildAsync(siteId, year, month, ct));
    }

    private async Task<RosterDto> BuildAsync(long siteId, int year, int month, CancellationToken ct)
    {
        var (first, last) = MonthRange(year, month);
        var staff = await SiteStaffAsync(siteId, ct);
        var ids = staff.Select(e => e.Id).ToList();
        var cells = await db.ShiftAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.Date >= first && a.Date <= last).ToListAsync(ct);
        var rules = await db.CoverageRules.AsNoTracking().Where(c => c.SiteId == siteId && c.MinCount > 0).ToListAsync(ct);
        var roster = await db.ShiftRosters.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Year == year && x.Month == month, ct);

        var coverage = new List<CoverageCellDto>();
        var typeIds = rules.Select(r => r.ShiftTypeId)
            .Union(cells.Where(c => c.ShiftTypeId is not null).Select(c => c.ShiftTypeId!.Value)).Distinct().ToList();
        for (var d = first; d <= last; d = d.AddDays(1))
            foreach (var t in typeIds)
                coverage.Add(new CoverageCellDto(d, t,
                    cells.Count(c => c.Date == d && c.Kind == RosterCellKind.Shift && c.ShiftTypeId == t),
                    rules.FirstOrDefault(r => r.ShiftTypeId == t)?.MinCount ?? 0));

        return new RosterDto(siteId, year, month, (roster?.Status ?? RosterStatus.Draft).ToString(), roster?.PublishedAt,
            staff.Select(e => new RosterEmployeeDto(e.Id, e.EmployeeNumber, e.FullName)).ToList(),
            cells.Select(c => new RosterCellDto(c.EmployeeId, c.Date, c.Kind.ToString(), c.ShiftTypeId)).ToList(),
            coverage);
    }

    /// <summary>Active employees whose place of work is the site, inside the caller's scope.</summary>
    private async Task<List<Employee>> SiteStaffAsync(long siteId, CancellationToken ct) =>
        await (await EmployeeService.ScopedQueryAsync(db, scope, ct))
            .Where(e => e.SiteId == siteId && e.Status == EmployeeStatus.Active)
            .OrderBy(e => e.FullName).ToListAsync(ct);

    internal static bool ValidMonth(int year, int month) => year is >= 2000 and <= 2100 && month is >= 1 and <= 12;

    internal static (DateOnly First, DateOnly Last) MonthRange(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }
}
