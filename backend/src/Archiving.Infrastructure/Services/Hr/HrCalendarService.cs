using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>Working week, holidays, the default shift, and the org-structure manager lookup.</summary>
public sealed class HrCalendarService(AppDbContext db, IAccessScope scope, IAuditWriter audit) : IHrCalendarService
{
    public async Task<HrSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await HrCommon.SettingsAsync(db, ct);
        return new HrSettingsDto(string.Join(',', s.Weekend.Order()), s.DefaultShiftTypeId, s.AbsenceLeaveTypeId);
    }

    public async Task<Result<HrSettingsDto>> SaveSettingsAsync(SaveHrSettingsRequest r, CancellationToken ct = default)
    {
        var weekendNames = (r.WeekendDays ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (weekendNames.Any(n => !Enum.TryParse<DayOfWeek>(n, ignoreCase: true, out _)))
            return Result<HrSettingsDto>.Fail("أيام العطلة الأسبوعية غير صالحة");
        var weekend = WorkCalendar.ParseWeekend(r.WeekendDays);
        if (weekend.Count >= 7) return Result<HrSettingsDto>.Fail("لا يمكن أن تكون كل أيام الأسبوع عطلة");
        if (r.DefaultShiftTypeId is { } st && !await db.ShiftTypes.AnyAsync(x => x.Id == st && x.IsActive, ct))
            return Result<HrSettingsDto>.Fail("المناوبة الافتراضية غير موجودة أو غير مفعّلة");
        if (r.AbsenceLeaveTypeId is { } lt && !await db.LeaveTypes.AnyAsync(x => x.Id == lt && x.IsActive && x.Kind == LeaveKind.Leave, ct))
            return Result<HrSettingsDto>.Fail("نوع الإجازة المخصوم منه غير صالح");

        var before = await GetSettingsAsync(ct);
        await SetAsync(HrSettingKeys.WeekendDays, string.Join(',', weekend.Order()), ct);
        await SetAsync(HrSettingKeys.DefaultShiftTypeId, r.DefaultShiftTypeId?.ToString() ?? string.Empty, ct);
        await SetAsync(HrSettingKeys.AbsenceLeaveTypeId, r.AbsenceLeaveTypeId?.ToString() ?? string.Empty, ct);
        await db.SaveChangesAsync(ct);
        var after = await GetSettingsAsync(ct);
        await audit.WriteAsync("Updated", "HrSettings", 0, "إعدادات الدوام", CoreGuards.Json(before), CoreGuards.Json(after), ct);
        return Result<HrSettingsDto>.Ok(after);
    }

    private async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var row = await db.HrSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) db.HrSettings.Add(new HrSetting { Key = key, Value = value });
        else row.Value = value;
    }

    public async Task<IReadOnlyList<HolidayDto>> HolidaysAsync(int year, CancellationToken ct = default)
    {
        var from = new DateOnly(Math.Clamp(year, 2000, 2100), 1, 1);
        var to = from.AddYears(1).AddDays(-1);
        return await db.PublicHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name, Convert.ToBase64String(h.RowVersion))).ToListAsync(ct);
    }

    public async Task<Result<HolidayDto>> AddHolidayAsync(SaveHolidayRequest r, CancellationToken ct = default)
    {
        var name = CoreGuards.Clean(r.Name);
        if (name.Length is 0 or > 150) return Result<HolidayDto>.Fail("اسم العطلة مطلوب ولا يتجاوز 150 حرفاً");
        if (r.Date == default) return Result<HolidayDto>.Fail("التاريخ مطلوب");
        if (await db.PublicHolidays.AnyAsync(h => h.Date == r.Date, ct)) return Result<HolidayDto>.Fail("هذا اليوم مسجّل عطلة");
        var h = new PublicHoliday { Date = r.Date, Name = name };
        db.PublicHolidays.Add(h);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PublicHoliday", h.Id, $"{h.Date:yyyy-MM-dd} {h.Name}", ct: ct);
        return Result<HolidayDto>.Ok(new HolidayDto(h.Id, h.Date, h.Name, HrCommon.B64(h.RowVersion)));
    }

    public async Task<Result<bool>> DeleteHolidayAsync(long id, CancellationToken ct = default)
    {
        var h = await db.PublicHolidays.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result<bool>.Fail("العطلة غير موجودة");
        db.PublicHolidays.Remove(h);   // soft delete via the auditable interceptor
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "PublicHoliday", h.Id, $"{h.Date:yyyy-MM-dd} {h.Name}", ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<EmployeeDto?>> DirectManagerAsync(long employeeId, CancellationToken ct = default)
    {
        var e = await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return Result<EmployeeDto?>.Fail("الموظف غير موجود");
        var managerId = await ResolveDirectManagerAsync(db, e.Id, ct);
        if (managerId is null) return Result<EmployeeDto?>.Ok(null);
        var m = await db.Employees.AsNoTracking().FirstAsync(x => x.Id == managerId, ct);
        return Result<EmployeeDto?>.Ok((await HrCommon.EmployeeDtosAsync(db, [m], ct))[0]);
    }

    /// <summary>
    /// The active employee sitting in the manager seat of the employee's unit; when that is the employee
    /// (or the seat is empty or unset), the parent unit's manager, and so on up the tree.
    /// </summary>
    internal static async Task<long?> ResolveDirectManagerAsync(AppDbContext db, long employeeId, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return null;
        var units = await db.OrgUnits.AsNoTracking().Select(u => new { u.Id, u.ParentId, u.ManagerPositionId })
            .ToDictionaryAsync(u => u.Id, ct);
        var seen = new HashSet<long>();
        for (long? cur = e.OrgUnitId; cur is { } unitId && seen.Add(unitId) && units.TryGetValue(unitId, out var unit); cur = unit.ParentId)
        {
            if (unit.ManagerPositionId is not { } seat) continue;
            // The seat's employee, or the employee record of the login occupying the seat.
            var occupant = await db.Positions.AsNoTracking().Where(p => p.Id == seat).Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
            var manager = await db.Employees.AsNoTracking()
                .Where(x => (x.PositionId == seat || (occupant != null && x.UserId == occupant))
                    && x.Status == Domain.Core.EmployeeStatus.Active && x.Id != e.Id)
                .OrderByDescending(x => x.PositionId == seat).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
            if (manager is not null) return manager;
        }
        return null;
    }
}

public sealed class LeaveTypeService(AppDbContext db, IAuditWriter audit) : ILeaveTypeService
{
    public async Task<IReadOnlyList<LeaveTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.LeaveTypes.AsNoTracking();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return await ToDtosAsync(await q.OrderBy(t => t.Name).ToListAsync(ct), ct);
    }

    public async Task<Result<LeaveTypeDto>> CreateAsync(SaveLeaveTypeRequest r, CancellationToken ct = default)
    {
        var t = new LeaveType();
        if (await ApplyAsync(t, r, null, ct) is { } error) return Result<LeaveTypeDto>.Fail(error);
        db.LeaveTypes.Add(t);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "LeaveType", t.Id, t.Name, ct: ct);
        return Result<LeaveTypeDto>.Ok((await ToDtosAsync([t], ct))[0]);
    }

    public async Task<Result<LeaveTypeDto>> UpdateAsync(long id, SaveLeaveTypeRequest r, CancellationToken ct = default)
    {
        var t = await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<LeaveTypeDto>.Fail("نوع الإجازة غير موجود");
        if (!CoreGuards.IsCurrent(t.RowVersion, r.RowVersion)) return Result<LeaveTypeDto>.Fail(CoreGuards.Stale);
        var before = CoreGuards.Json((await ToDtosAsync([t], ct))[0]);
        if (await ApplyAsync(t, r, id, ct) is { } error) return Result<LeaveTypeDto>.Fail(error);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<LeaveTypeDto>.Fail(CoreGuards.Stale); }
        var dto = (await ToDtosAsync([t], ct))[0];
        await audit.WriteAsync("Updated", "LeaveType", t.Id, t.Name, before, CoreGuards.Json(dto), ct);
        return Result<LeaveTypeDto>.Ok(dto);
    }

    private async Task<string?> ApplyAsync(LeaveType t, SaveLeaveTypeRequest r, long? id, CancellationToken ct)
    {
        var code = CoreGuards.Clean(r.Code).ToUpperInvariant();
        var name = CoreGuards.Clean(r.Name);
        if (code.Length is 0 or > 32) return "الرمز مطلوب ولا يتجاوز 32 حرفاً";
        if (name.Length is 0 or > 150) return "الاسم مطلوب ولا يتجاوز 150 حرفاً";
        if (!Enum.TryParse<LeaveKind>(r.Kind, true, out var kind) || !Enum.IsDefined(kind)) return "نوع الطلب غير صالح";
        if (!Enum.TryParse<LeaveUnit>(r.Unit, true, out var unit) || !Enum.IsDefined(unit)) return "وحدة الرصيد غير صالحة";
        if (kind == LeaveKind.Departure && unit != LeaveUnit.Hours) return "المغادرة تُحسب بالساعات";
        if (kind != LeaveKind.Departure && unit != LeaveUnit.Days) return "الإجازة والمهمة تُحسبان بالأيام";
        if (r.AnnualEntitlement is < 0 or > 1000) return "الاستحقاق السنوي خارج النطاق";
        if (r.MaxCarryOver is < 0 or > 1000) return "حد الترحيل خارج النطاق";
        if (!r.AppliesToMilitary && !r.AppliesToCivil && !r.AppliesToContract) return "اختر فئة واحدة على الأقل";
        if (r.WorkflowDefinitionId is { } wf && !await db.WorkflowDefinitions.AnyAsync(d => d.Id == wf && d.IsActive, ct))
            return "مسار الموافقة غير موجود أو غير مفعّل";
        if (await db.LeaveTypes.IgnoreQueryFilters().AnyAsync(x => x.Code == code && x.Id != (id ?? 0), ct)) return "الرمز مستخدم";

        t.Code = code; t.Name = name; t.Kind = kind; t.Unit = unit;
        // A mission never deducts: it justifies the absence.
        t.DeductsBalance = kind != LeaveKind.Mission && r.DeductsBalance;
        t.CountWorkingDaysOnly = r.CountWorkingDaysOnly;
        t.RequiresSubstitute = r.RequiresSubstitute;
        t.AnnualEntitlement = r.AnnualEntitlement;
        t.MaxCarryOver = r.MaxCarryOver;
        t.AppliesToMilitary = r.AppliesToMilitary; t.AppliesToCivil = r.AppliesToCivil; t.AppliesToContract = r.AppliesToContract;
        t.WorkflowDefinitionId = r.WorkflowDefinitionId;
        t.IsActive = r.IsActive;
        return null;
    }

    private async Task<IReadOnlyList<LeaveTypeDto>> ToDtosAsync(IReadOnlyList<LeaveType> rows, CancellationToken ct)
    {
        var wfIds = rows.Where(r => r.WorkflowDefinitionId is not null).Select(r => r.WorkflowDefinitionId!.Value).Distinct().ToList();
        var names = await db.WorkflowDefinitions.AsNoTracking().Where(d => wfIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        return rows.Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.Kind.ToString(), t.Unit.ToString(), t.DeductsBalance,
            t.CountWorkingDaysOnly, t.RequiresSubstitute, t.AnnualEntitlement, t.MaxCarryOver, t.AppliesToMilitary,
            t.AppliesToCivil, t.AppliesToContract, t.WorkflowDefinitionId,
            t.WorkflowDefinitionId is { } w ? names.GetValueOrDefault(w) : null, t.IsActive, HrCommon.B64(t.RowVersion))).ToList();
    }
}
