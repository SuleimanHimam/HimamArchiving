using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Core;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>Shared reads for the phase-2/3 HR services: settings, calendar, the caller's employee row, time zone.</summary>
internal static class HrCommon
{
    /// <summary>Times are stored in UTC and shown/entered on the Asia/Hebron clock (standards §4.4).</summary>
    public static readonly TimeZoneInfo Hebron = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hebron");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Hebron);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Hebron);

    public static DateOnly TodayLocal() => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));

    public sealed record Settings(IReadOnlySet<DayOfWeek> Weekend, long? DefaultShiftTypeId, long? AbsenceLeaveTypeId);

    public static async Task<Settings> SettingsAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.HrSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return new Settings(
            WorkCalendar.ParseWeekend(rows.GetValueOrDefault(HrSettingKeys.WeekendDays)),
            long.TryParse(rows.GetValueOrDefault(HrSettingKeys.DefaultShiftTypeId), out var s) ? s : null,
            long.TryParse(rows.GetValueOrDefault(HrSettingKeys.AbsenceLeaveTypeId), out var l) ? l : null);
    }

    public static async Task<IReadOnlySet<DateOnly>> HolidaysAsync(AppDbContext db, DateOnly from, DateOnly to, CancellationToken ct) =>
        (await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync(ct))
        .ToHashSet();

    /// <summary>The employee record linked to the signed-in login, or null.</summary>
    public static async Task<Employee?> CurrentEmployeeAsync(AppDbContext db, ICurrentUser user, CancellationToken ct) =>
        user.UserId is { } uid ? await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid, ct) : null;

    public static async Task<IReadOnlyList<EmployeeDto>> EmployeeDtosAsync(AppDbContext db, IReadOnlyList<Employee> rows, CancellationToken ct)
    {
        var unitIds = rows.Select(e => e.OrgUnitId).Distinct().ToList();
        var siteIds = rows.Where(e => e.SiteId is not null).Select(e => e.SiteId!.Value).Distinct().ToList();
        var units = await db.OrgUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var sites = await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return rows.Select(e => new EmployeeDto(e.Id, e.EmployeeNumber, e.FullName, e.FullNameEn, e.Category.ToString(),
            e.Status.ToString(), e.OrgUnitId, units.GetValueOrDefault(e.OrgUnitId), e.PositionId, e.JobTitle, e.SiteId,
            e.SiteId is { } sid ? sites.GetValueOrDefault(sid) : null, e.HireDate, e.UserId,
            Convert.ToBase64String(e.RowVersion))).ToList();
    }

    public static async Task<Dictionary<long, string>> NamesAsync(AppDbContext db, IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Employees.AsNoTracking().Where(e => list.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
    }

    public static string B64(byte[] v) => Convert.ToBase64String(v);

    public static void Notify(AppDbContext db, long userId, string title, string body, string entityType, long entityId) =>
        db.Notifications.Add(new Notification
        {
            RecipientUserId = userId, Title = title, Body = body, Type = NotificationType.Info,
            EntityType = entityType, EntityId = entityId,
        });

    public static bool TryTime(string? v, out TimeOnly t) =>
        TimeOnly.TryParseExact(v ?? string.Empty, ["HH:mm", "H:mm", "HH:mm:ss"], out t);
}
