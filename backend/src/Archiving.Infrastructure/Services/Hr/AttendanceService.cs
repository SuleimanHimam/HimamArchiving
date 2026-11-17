using System.Globalization;
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
/// Attendance (presentation slide 12): raw punches in, each day matched against the roster (or the default
/// shift), leave and missions; every exception waits in its own folder until resolved with a documented
/// decision; a closed month is frozen. Deductions flow on: leave to the balance, minutes to payroll.
/// </summary>
public sealed class AttendanceService(AppDbContext db, IAccessScope scope, ICurrentUser user, IAuditWriter audit) : IAttendanceService
{
    private const int MaxImportLines = 5000;
    private const int MaxProcessDays = 62;

    // ---- import ----

    public async Task<Result<ImportPunchesResult>> ImportAsync(ImportPunchesRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PunchSource>(r.Source, true, out var source) || source is not (PunchSource.Device or PunchSource.Import))
            return Result<ImportPunchesResult>.Fail("مصدر الحركات غير صالح");
        if (r.Lines is null || r.Lines.Count is 0 or > MaxImportLines)
            return Result<ImportPunchesResult>.Fail($"عدد الحركات بين 1 و{MaxImportLines}");

        var numbers = r.Lines.Select(l => CoreGuards.Clean(l.EmployeeNumber)).Distinct().ToList();
        var employees = await (await EmployeeService.ScopedQueryAsync(db, scope, ct))
            .Where(e => numbers.Contains(e.EmployeeNumber)).ToDictionaryAsync(e => e.EmployeeNumber, e => e.Id, ct);
        var closed = await ClosedMonthsAsync(ct);

        var errors = new List<string>();
        var parsed = new List<AttendancePunch>();
        for (var i = 0; i < r.Lines.Count; i++)
        {
            var line = r.Lines[i];
            var row = i + 1;
            if (!employees.TryGetValue(CoreGuards.Clean(line.EmployeeNumber), out var empId)) { errors.Add($"سطر {row}: رقم وظيفي غير معروف"); continue; }
            if (!TryUtc(line.PunchedAt, out var utc)) { errors.Add($"سطر {row}: وقت غير صالح"); continue; }
            var local = HrCommon.ToLocal(utc);
            if (closed.Contains((local.Year, local.Month))) { errors.Add($"سطر {row}: الشهر مقفل"); continue; }
            if (utc > DateTime.UtcNow.AddMinutes(5)) { errors.Add($"سطر {row}: وقت في المستقبل"); continue; }
            var device = CoreGuards.CleanOrNull(line.DeviceCode);
            if (device?.Length > 64) { errors.Add($"سطر {row}: رمز الجهاز أطول من 64"); continue; }
            parsed.Add(new AttendancePunch { EmployeeId = empId, PunchedAt = utc, Source = source, DeviceCode = device });
        }

        var ids = parsed.Select(p => p.EmployeeId).Distinct().ToList();
        var min = parsed.Count > 0 ? parsed.Min(p => p.PunchedAt) : DateTime.MinValue;
        var max = parsed.Count > 0 ? parsed.Max(p => p.PunchedAt) : DateTime.MinValue;
        var known = (await db.AttendancePunches.AsNoTracking()
                .Where(p => ids.Contains(p.EmployeeId) && p.PunchedAt >= min && p.PunchedAt <= max)
                .Select(p => new { p.EmployeeId, p.PunchedAt }).ToListAsync(ct))
            .Select(p => (p.EmployeeId, p.PunchedAt)).ToHashSet();
        var duplicates = 0;
        foreach (var p in parsed)
        {
            if (!known.Add((p.EmployeeId, p.PunchedAt))) { duplicates++; continue; }
            db.AttendancePunches.Add(p);
        }
        await db.SaveChangesAsync(ct);
        var imported = parsed.Count - duplicates;
        await audit.WriteAsync("Imported", "AttendancePunch", 0, source.ToString(),
            newValues: $"imported={imported}, duplicates={duplicates}, errors={errors.Count}", ct: ct);
        return Result<ImportPunchesResult>.Ok(new ImportPunchesResult(imported, duplicates, errors.Take(200).ToList()));
    }

    internal static bool TryUtc(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        var hasOffset = v.EndsWith('Z') || v.EndsWith('z') || System.Text.RegularExpressions.Regex.IsMatch(v, @"[+-]\d\d:?\d\d$");
        if (hasOffset)
        {
            if (!DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)) return false;
            utc = dto.UtcDateTime;
            return true;
        }
        if (!DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return false;
        utc = HrCommon.ToUtc(local);
        return true;
    }

    // ---- processing ----

    /// <summary>What one employee was expected to work on one day, and why not when nothing is expected.</summary>
    private sealed record Expectation(ShiftType? Shift, string State);

    private sealed class DayContext
    {
        public required HrCommon.Settings Settings { get; init; }
        public required IReadOnlySet<DateOnly> Holidays { get; init; }
        public required Dictionary<long, ShiftType> Types { get; init; }
        public required Dictionary<(long, DateOnly), ShiftAssignment> Cells { get; init; }
        public required List<LeaveRequest> Leaves { get; init; }
        public required ILookup<long, AttendancePunch> Punches { get; init; }

        public Expectation Expect(long employeeId, DateOnly d)
        {
            var leave = Leaves.FirstOrDefault(l => l.EmployeeId == employeeId && l.LeaveType.Kind != LeaveKind.Departure && l.FromDate <= d && d <= l.ToDate);
            if (leave is not null) return new(null, leave.LeaveType.Kind == LeaveKind.Mission ? "Mission" : "Leave");
            if (Cells.TryGetValue((employeeId, d), out var cell))
                return cell.Kind switch
                {
                    RosterCellKind.Rest => new(null, "Rest"),
                    RosterCellKind.Leave => new(null, "Leave"),
                    _ => cell.ShiftTypeId is { } st && Types.TryGetValue(st, out var t) ? new(t, "Shift") : new(null, "NoShift"),
                };
            if (Holidays.Contains(d)) return new(null, "Holiday");
            if (Settings.Weekend.Contains(d.DayOfWeek)) return new(null, "Rest");
            return Settings.DefaultShiftTypeId is { } def && Types.TryGetValue(def, out var dt) ? new(dt, "Shift") : new(null, "NoShift");
        }

        public bool HasDeparture(long employeeId, DateOnly d) =>
            Leaves.Any(l => l.EmployeeId == employeeId && l.LeaveType.Kind == LeaveKind.Departure && l.FromDate == d);

        public IEnumerable<DateTime> LocalPunches(long employeeId) => Punches[employeeId].Select(p => HrCommon.ToLocal(p.PunchedAt));
    }

    private async Task<DayContext> ContextAsync(IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = HrCommon.ToUtc(from.AddDays(-1).ToDateTime(TimeOnly.MinValue));
        var toUtc = HrCommon.ToUtc(to.AddDays(2).ToDateTime(TimeOnly.MinValue));
        return new DayContext
        {
            Settings = await HrCommon.SettingsAsync(db, ct),
            Holidays = await HrCommon.HolidaysAsync(db, from, to, ct),
            Types = await db.ShiftTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct),
            Cells = await db.ShiftAssignments.AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to)
                .ToDictionaryAsync(a => (a.EmployeeId, a.Date), ct),
            Leaves = await db.LeaveRequests.AsNoTracking().Include(l => l.LeaveType)
                .Where(l => employeeIds.Contains(l.EmployeeId) && l.Status == LeaveRequestStatus.Approved && l.FromDate <= to && from <= l.ToDate)
                .ToListAsync(ct),
            Punches = (await db.AttendancePunches.AsNoTracking()
                .Where(p => employeeIds.Contains(p.EmployeeId) && p.PunchedAt >= fromUtc && p.PunchedAt < toUtc).ToListAsync(ct))
                .ToLookup(p => p.EmployeeId),
        };
    }

    public async Task<Result<ProcessAttendanceResult>> ProcessAsync(ProcessAttendanceRequest r, CancellationToken ct = default)
    {
        if (r.To < r.From || r.To.DayNumber - r.From.DayNumber + 1 > MaxProcessDays)
            return Result<ProcessAttendanceResult>.Fail($"الفترة غير صالحة (حتى {MaxProcessDays} يوماً)");
        var to = r.To > HrCommon.TodayLocal() ? HrCommon.TodayLocal() : r.To;
        if (to < r.From) return Result<ProcessAttendanceResult>.Fail("لا تُعالج أيام في المستقبل");
        var closed = await ClosedMonthsAsync(ct);

        var employees = await (await EmployeeService.ScopedQueryAsync(db, scope, ct))
            .Where(e => e.Status == EmployeeStatus.Active).Select(e => e.Id).ToListAsync(ct);
        var ctx = await ContextAsync(employees, r.From, to, ct);
        var existing = await db.AttendanceExceptions
            .Where(x => employees.Contains(x.EmployeeId) && x.Date >= r.From && x.Date <= to).ToListAsync(ct);

        int days = 0, found = 0;
        foreach (var emp in employees)
        for (var d = r.From; d <= to; d = d.AddDays(1))
        {
            if (closed.Contains((d.Year, d.Month))) continue;
            days++;
            var expectation = ctx.Expect(emp, d);
            var findings = expectation.Shift is { } shift
                ? AttendanceClassifier.Classify(AttendanceClassifier.Window(d, shift.StartTime, shift.EndTime, shift.GraceMinutes), ctx.LocalPunches(emp))
                : [];
            // An approved departure that day covers lateness and leaving early.
            if (ctx.HasDeparture(emp, d))
                findings = findings.Where(f => f.Kind is not (AttendanceExceptionKind.Late or AttendanceExceptionKind.EarlyLeave)).ToList();

            var mine = existing.Where(x => x.EmployeeId == emp && x.Date == d).ToList();
            foreach (var stale in mine.Where(x => x.Status == AttendanceExceptionStatus.Pending && findings.All(f => f.Kind != x.Kind)))
                db.AttendanceExceptions.Remove(stale);
            foreach (var f in findings)
            {
                found++;
                var match = mine.FirstOrDefault(x => x.Kind == f.Kind);
                if (match is null)
                    db.AttendanceExceptions.Add(new AttendanceException
                    {
                        EmployeeId = emp, Date = d, Kind = f.Kind, Minutes = f.Minutes, ShiftTypeId = expectation.Shift!.Id,
                    });
                else if (match.Status == AttendanceExceptionStatus.Pending)
                {
                    match.Minutes = f.Minutes;
                    match.ShiftTypeId = expectation.Shift!.Id;
                }
            }
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Processed", "Attendance", 0, $"{r.From:yyyy-MM-dd}..{to:yyyy-MM-dd}",
            newValues: $"days={days}, exceptions={found}", ct: ct);
        return Result<ProcessAttendanceResult>.Ok(new ProcessAttendanceResult(days, found));
    }

    // ---- reads ----

    public async Task<IReadOnlyList<AttendanceDayDto>> DayAsync(DateOnly date, long? siteId, CancellationToken ct = default)
    {
        var q = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Where(e => e.Status == EmployeeStatus.Active);
        if (siteId is { } s) q = q.Where(e => e.SiteId == s);
        var employees = await q.OrderBy(e => e.FullName).Take(1000).ToListAsync(ct);
        return await DaysAsync(employees, date, date, ct);
    }

    public async Task<Result<IReadOnlyList<AttendanceDayDto>>> MineAsync(int year, int month, CancellationToken ct = default)
    {
        if (!ShiftService.ValidMonth(year, month)) return Result<IReadOnlyList<AttendanceDayDto>>.Fail("الشهر غير صالح");
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me is null) return Result<IReadOnlyList<AttendanceDayDto>>.Fail("حسابك غير مرتبط بسجل موظف");
        var (first, last) = ShiftService.MonthRange(year, month);
        var today = HrCommon.TodayLocal();
        if (last > today) last = today;
        if (last < first) return Result<IReadOnlyList<AttendanceDayDto>>.Ok([]);
        return Result<IReadOnlyList<AttendanceDayDto>>.Ok(await DaysAsync([me], first, last, ct));
    }

    private async Task<IReadOnlyList<AttendanceDayDto>> DaysAsync(IReadOnlyList<Employee> employees, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = employees.Select(e => e.Id).ToList();
        var ctx = await ContextAsync(ids, from, to, ct);
        var exceptions = (await db.AttendanceExceptions.AsNoTracking()
            .Where(x => ids.Contains(x.EmployeeId) && x.Date >= from && x.Date <= to).ToListAsync(ct));
        var names = employees.ToDictionary(e => e.Id, e => e.FullName);
        var result = new List<AttendanceDayDto>();
        foreach (var e in employees)
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var exp = ctx.Expect(e.Id, d);
            var window = exp.Shift is { } sh ? AttendanceClassifier.Window(d, sh.StartTime, sh.EndTime, sh.GraceMinutes) : null;
            var dayStart = window?.StartLocal - AttendanceClassifier.EarlyWindow ?? d.ToDateTime(TimeOnly.MinValue);
            var dayEnd = window?.EndLocal + AttendanceClassifier.LateWindow ?? d.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var punches = ctx.Punches[e.Id].Select(p => (Local: HrCommon.ToLocal(p.PunchedAt), p.Source))
                .Where(p => p.Local >= dayStart && p.Local < dayEnd).OrderBy(p => p.Local).ToList();
            var mine = exceptions.Where(x => x.EmployeeId == e.Id && x.Date == d).Select(x => ExceptionDto(x, names)).ToList();
            var state = exp.State == "Shift"
                ? mine.Any(x => x.Status == nameof(AttendanceExceptionStatus.Pending)) ? "Exception" : punches.Count > 0 ? "Present" : "Unprocessed"
                : exp.State;
            result.Add(new AttendanceDayDto(e.Id, e.EmployeeNumber, e.FullName, d, exp.Shift?.Name,
                punches.Count > 0 ? punches[0].Local.ToString("HH:mm") : null,
                punches.Count > 1 ? punches[^1].Local.ToString("HH:mm") : null,
                punches.Select(p => p.Source.ToString()).Distinct().ToList(), state, mine));
        }
        return result;
    }

    public async Task<IReadOnlyList<AttendanceExceptionDto>> ExceptionsAsync(string? status, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var ids = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Select(e => e.Id);
        var q = db.AttendanceExceptions.AsNoTracking().Where(x => ids.Contains(x.EmployeeId));
        if (Enum.TryParse<AttendanceExceptionStatus>(status, true, out var st)) q = q.Where(x => x.Status == st);
        if (from is { } f) q = q.Where(x => x.Date >= f);
        if (to is { } t) q = q.Where(x => x.Date <= t);
        var rows = await q.OrderByDescending(x => x.Date).ThenBy(x => x.EmployeeId).Take(1000).ToListAsync(ct);
        var names = await HrCommon.NamesAsync(db, rows.Select(x => x.EmployeeId), ct);
        return rows.Select(x => ExceptionDto(x, names)).ToList();
    }

    private static AttendanceExceptionDto ExceptionDto(AttendanceException x, IReadOnlyDictionary<long, string> names) =>
        new(x.Id, x.EmployeeId, names.GetValueOrDefault(x.EmployeeId) ?? string.Empty, x.Date, x.Kind.ToString(), x.Minutes,
            x.Status.ToString(), x.Resolution?.ToString(), x.ResolutionNote, x.ResolvedAt);

    // ---- resolution & close ----

    public async Task<Result<AttendanceExceptionDto>> ResolveAsync(long id, ResolveExceptionRequest r, CancellationToken ct = default)
    {
        // Scope check first: composing the no-tracking scope query into this one would make x untracked.
        var x = await db.AttendanceExceptions.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (x is null || !await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).AnyAsync(e => e.Id == x.EmployeeId, ct))
            return Result<AttendanceExceptionDto>.Fail("الاستثناء غير موجود");
        if (x.Status != AttendanceExceptionStatus.Pending) return Result<AttendanceExceptionDto>.Fail("عولج الاستثناء مسبقاً");
        if ((await ClosedMonthsAsync(ct)).Contains((x.Date.Year, x.Date.Month))) return Result<AttendanceExceptionDto>.Fail("الشهر مقفل");
        if (!Enum.TryParse<AttendanceResolution>(r.Resolution, true, out var resolution) || !Enum.IsDefined(resolution))
            return Result<AttendanceExceptionDto>.Fail("قرار المعالجة غير صالح");
        var note = CoreGuards.Clean(r.Note);
        if (note.Length is 0 or > 1000) return Result<AttendanceExceptionDto>.Fail("مبرر القرار مطلوب ولا يتجاوز 1000 حرف");

        switch (resolution)
        {
            case AttendanceResolution.DeductLeave:
            {
                // Only a whole absent day converts to leave: the minute-to-day ratio is not a documented rule.
                if (x.Kind != AttendanceExceptionKind.Absent) return Result<AttendanceExceptionDto>.Fail("الخصم من الإجازات للغياب الكامل فقط");
                var settings = await HrCommon.SettingsAsync(db, ct);
                if (settings.AbsenceLeaveTypeId is not { } typeId)
                    return Result<AttendanceExceptionDto>.Fail("حدّد نوع الإجازة المخصوم منه في إعدادات الدوام");
                db.LeaveBalanceMovements.Add(LeaveService.Movement(x.EmployeeId, typeId, x.Date.Year, -1, LeaveMovementKind.Attendance,
                    "AttendanceException", x.Id, note));
                break;
            }
            case AttendanceResolution.PunchCorrected:
            {
                if (!HrCommon.TryTime(r.CorrectionTime, out var time)) return Result<AttendanceExceptionDto>.Fail("وقت البصمة المصحّحة مطلوب");
                db.AttendancePunches.Add(new AttendancePunch
                {
                    EmployeeId = x.EmployeeId, PunchedAt = HrCommon.ToUtc(x.Date.ToDateTime(time)),
                    Source = PunchSource.Correction, Note = note,
                });
                break;
            }
        }
        x.Status = AttendanceExceptionStatus.Resolved;
        x.Resolution = resolution;
        x.ResolutionNote = note;
        x.ResolvedBy = user.UserId;
        x.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Resolved", "AttendanceException", x.Id, $"{x.Kind} {x.Date:yyyy-MM-dd}",
            newValues: $"{resolution}: {note}", ct: ct);
        var names = await HrCommon.NamesAsync(db, [x.EmployeeId], ct);
        return Result<AttendanceExceptionDto>.Ok(ExceptionDto(x, names));
    }

    public async Task<AttendancePeriodDto> PeriodAsync(int year, int month, CancellationToken ct = default)
    {
        var p = await db.AttendancePeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Year == year && x.Month == month, ct);
        var (first, last) = ShiftService.MonthRange(Math.Clamp(year, 2000, 2100), Math.Clamp(month, 1, 12));
        var pending = await db.AttendanceExceptions.CountAsync(x => x.Status == AttendanceExceptionStatus.Pending && x.Date >= first && x.Date <= last, ct);
        return new AttendancePeriodDto(year, month, p is not null, p?.ClosedAt, pending);
    }

    public async Task<Result<AttendancePeriodDto>> CloseAsync(int year, int month, CancellationToken ct = default)
    {
        if (!ShiftService.ValidMonth(year, month)) return Result<AttendancePeriodDto>.Fail("الشهر غير صالح");
        var (_, last) = ShiftService.MonthRange(year, month);
        if (last >= HrCommon.TodayLocal()) return Result<AttendancePeriodDto>.Fail("لا يُقفل شهر قبل انتهائه");
        var current = await PeriodAsync(year, month, ct);
        if (current.IsClosed) return Result<AttendancePeriodDto>.Fail("الشهر مقفل مسبقاً");
        if (current.PendingExceptions > 0)
            return Result<AttendancePeriodDto>.Fail($"عالج الاستثناءات المعلّقة أولاً ({current.PendingExceptions})");
        db.AttendancePeriods.Add(new AttendancePeriod { Year = year, Month = month, ClosedAt = DateTime.UtcNow, ClosedBy = user.UserId });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Closed", "AttendancePeriod", year * 100 + month, $"{year}-{month:00}", ct: ct);
        return Result<AttendancePeriodDto>.Ok(await PeriodAsync(year, month, ct));
    }

    private async Task<HashSet<(int, int)>> ClosedMonthsAsync(CancellationToken ct) =>
        (await db.AttendancePeriods.AsNoTracking().Select(p => new { p.Year, p.Month }).ToListAsync(ct))
        .Select(p => (p.Year, p.Month)).ToHashSet();
}
