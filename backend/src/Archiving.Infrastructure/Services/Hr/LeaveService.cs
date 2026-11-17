using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// Leave, departures and missions (presentation slide 15): instant balance and overlap check, mandatory
/// substitute where the type says so, approval through the type's path, and on approval a balance
/// movement and leave cells in the roster. The balance is never edited: it is the sum of movements.
/// </summary>
public sealed class LeaveService(
    AppDbContext db,
    IAccessScope scope,
    ICurrentUser user,
    IAuditWriter audit,
    IApprovalService approvals) : ILeaveService
{
    public const string RequestType = "hr.leave";
    private const string NotFound = "الطلب غير موجود";
    private const int MaxPageSize = 200;

    // ---- requests ----

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(LeaveListQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);
        if (q.EmployeeId is { } emp) query = query.Where(r => r.EmployeeId == emp);
        if (Enum.TryParse<LeaveRequestStatus>(q.Status, true, out var st)) query = query.Where(r => r.Status == st);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<LeaveRequestDto> { Items = await ToDtosAsync(db, rows, ct), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> MineAsync(CancellationToken ct = default)
    {
        var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me is null) return [];
        var rows = await db.LeaveRequests.AsNoTracking().Include(r => r.LeaveType)
            .Where(r => r.EmployeeId == me.Id).OrderByDescending(r => r.Id).Take(100).ToListAsync(ct);
        return await ToDtosAsync(db, rows, ct);
    }

    public async Task<Result<LeaveCheckDto>> CheckAsync(SaveLeaveRequest r, long? excludeId, CancellationToken ct = default)
    {
        var (employee, error) = await EmployeeAsync(r.EmployeeId, ct);
        if (employee is null) return Result<LeaveCheckDto>.Fail(error!);
        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.LeaveTypeId && t.IsActive, ct);
        if (type is null) return Result<LeaveCheckDto>.Fail("نوع الإجازة غير موجود أو غير مفعّل");
        return Result<LeaveCheckDto>.Ok(await EvaluateAsync(db, employee, type, r, excludeId, ct));
    }

    public async Task<Result<LeaveRequestDto>> CreateAsync(SaveLeaveRequest r, CancellationToken ct = default)
    {
        var (employee, error) = await EmployeeAsync(r.EmployeeId, ct);
        if (employee is null) return Result<LeaveRequestDto>.Fail(error!);
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == r.LeaveTypeId && t.IsActive, ct);
        if (type is null) return Result<LeaveRequestDto>.Fail("نوع الإجازة غير موجود أو غير مفعّل");
        var check = await EvaluateAsync(db, employee, type, r, null, ct);
        if (check.Problems.Count > 0) return Result<LeaveRequestDto>.Fail(check.Problems[0]);

        var l = new LeaveRequest { EmployeeId = employee.Id, LeaveTypeId = type.Id, LeaveType = type };
        Apply(l, r, type, check.Quantity);
        db.LeaveRequests.Add(l);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "LeaveRequest", l.Id, $"{type.Name}: {employee.FullName}", ct: ct);
        return Result<LeaveRequestDto>.Ok((await ToDtosAsync(db, [l], ct))[0]);
    }

    public async Task<Result<LeaveRequestDto>> UpdateAsync(long id, SaveLeaveRequest r, CancellationToken ct = default)
    {
        var l = await FindTrackedAsync(id, ct);
        if (l is null) return Result<LeaveRequestDto>.Fail(NotFound);
        if (l.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Returned))
            return Result<LeaveRequestDto>.Fail("لا يُعدَّل الطلب بعد تقديمه");
        if (!CoreGuards.IsCurrent(l.RowVersion, r.RowVersion)) return Result<LeaveRequestDto>.Fail(CoreGuards.Stale);
        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == l.EmployeeId, ct);
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == r.LeaveTypeId && t.IsActive, ct);
        if (type is null) return Result<LeaveRequestDto>.Fail("نوع الإجازة غير موجود أو غير مفعّل");
        var check = await EvaluateAsync(db, employee, type, r with { EmployeeId = employee.Id }, id, ct);
        if (check.Problems.Count > 0) return Result<LeaveRequestDto>.Fail(check.Problems[0]);

        l.LeaveTypeId = type.Id;
        l.LeaveType = type;
        Apply(l, r, type, check.Quantity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<LeaveRequestDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "LeaveRequest", l.Id, type.Name, ct: ct);
        return Result<LeaveRequestDto>.Ok((await ToDtosAsync(db, [l], ct))[0]);
    }

    public async Task<Result<LeaveRequestDto>> SubmitAsync(long id, CancellationToken ct = default)
    {
        var l = await FindTrackedAsync(id, ct);
        if (l is null) return Result<LeaveRequestDto>.Fail(NotFound);
        if (l.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Returned))
            return Result<LeaveRequestDto>.Fail("الطلب مقدّم مسبقاً أو مغلق");
        if (!l.LeaveType.IsActive) return Result<LeaveRequestDto>.Fail("نوع الإجازة غير مفعّل");
        if (l.LeaveType.WorkflowDefinitionId is not { } workflowId)
            return Result<LeaveRequestDto>.Fail("لم يُحدَّد مسار اعتماد لهذا النوع. يحدده مدير النظام من إعدادات الإجازات");

        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == l.EmployeeId, ct);
        var check = await EvaluateAsync(db, employee, l.LeaveType, AsRequest(l), l.Id, ct);
        if (check.Problems.Count > 0) return Result<LeaveRequestDto>.Fail(check.Problems[0]);

        var title = $"{l.LeaveType.Name}: {employee.FullName} ({l.FromDate:yyyy-MM-dd} → {l.ToDate:yyyy-MM-dd})";
        return await db.InTransactionAsync(async () =>
        {
            var submitted = await approvals.SubmitAsync(new SubmitApprovalRequest(RequestType, "HR", l.Id, title, workflowId), ct);
            if (!submitted.Succeeded) return Result<LeaveRequestDto>.Fail(submitted.Error!);
            l.Status = LeaveRequestStatus.Submitted;
            l.ApprovalRequestId = submitted.Value!.Id;
            l.DecisionNote = null;
            l.Quantity = check.Quantity;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Submitted", "LeaveRequest", l.Id, title, ct: ct);
            return Result<LeaveRequestDto>.Ok((await ToDtosAsync(db, [l], ct))[0]);
        }, x => x.Succeeded, ct);
    }

    public async Task<Result<LeaveRequestDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var l = await FindTrackedAsync(id, ct);
        if (l is null) return Result<LeaveRequestDto>.Fail(NotFound);
        if (l.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Returned))
            return Result<LeaveRequestDto>.Fail("لا يُلغى إلا طلب مسودة أو معاد");
        l.Status = LeaveRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "LeaveRequest", l.Id, l.LeaveType.Name, ct: ct);
        return Result<LeaveRequestDto>.Ok((await ToDtosAsync(db, [l], ct))[0]);
    }

    // ---- balances ----

    public async Task<Result<IReadOnlyList<LeaveBalanceDto>>> BalancesAsync(long? employeeId, int year, CancellationToken ct = default)
    {
        var (employee, error) = await EmployeeAsync(employeeId, ct);
        if (employee is null) return Result<IReadOnlyList<LeaveBalanceDto>>.Fail(error!);
        var types = (await db.LeaveTypes.AsNoTracking().Where(t => t.IsActive && t.DeductsBalance).OrderBy(t => t.Name).ToListAsync(ct))
            .Where(t => t.AppliesTo(employee.Category)).ToList();
        var moves = await db.LeaveBalanceMovements.AsNoTracking()
            .Where(m => m.EmployeeId == employee.Id && m.Year == year).ToListAsync(ct);
        var pending = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.EmployeeId == employee.Id && r.Status == LeaveRequestStatus.Submitted && r.FromDate.Year == year)
            .GroupBy(r => r.LeaveTypeId).Select(g => new { g.Key, Qty = g.Sum(r => r.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        IReadOnlyList<LeaveBalanceDto> list = types.Select(t =>
        {
            var mine = moves.Where(m => m.LeaveTypeId == t.Id).ToList();
            return new LeaveBalanceDto(t.Id, t.Name, t.Unit.ToString(), year,
                mine.Where(m => m.Quantity > 0).Sum(m => m.Quantity), -mine.Where(m => m.Quantity < 0).Sum(m => m.Quantity),
                pending.GetValueOrDefault(t.Id), mine.Sum(m => m.Quantity));
        }).ToList();
        return Result<IReadOnlyList<LeaveBalanceDto>>.Ok(list);
    }

    public async Task<Result<IReadOnlyList<LeaveMovementDto>>> MovementsAsync(long employeeId, int year, CancellationToken ct = default)
    {
        var (employee, error) = await EmployeeAsync(employeeId, ct);
        if (employee is null) return Result<IReadOnlyList<LeaveMovementDto>>.Fail(error!);
        var names = await db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        IReadOnlyList<LeaveMovementDto> rows = (await db.LeaveBalanceMovements.AsNoTracking()
                .Where(m => m.EmployeeId == employee.Id && m.Year == year).OrderBy(m => m.Id).ToListAsync(ct))
            .Select(m => new LeaveMovementDto(m.Id, m.LeaveTypeId, names.GetValueOrDefault(m.LeaveTypeId) ?? string.Empty, m.Year,
                m.Quantity, m.Kind.ToString(), m.SourceType, m.SourceId, m.Note, m.CreatedAt)).ToList();
        return Result<IReadOnlyList<LeaveMovementDto>>.Ok(rows);
    }

    public async Task<Result<LeaveMovementDto>> AdjustAsync(LeaveAdjustmentRequest r, CancellationToken ct = default)
    {
        var (employee, error) = await EmployeeAsync(r.EmployeeId, ct);
        if (employee is null) return Result<LeaveMovementDto>.Fail(error!);
        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.LeaveTypeId, ct);
        if (type is null) return Result<LeaveMovementDto>.Fail("نوع الإجازة غير موجود");
        if (!Enum.TryParse<LeaveMovementKind>(r.Kind, true, out var kind) || kind is not (LeaveMovementKind.Opening or LeaveMovementKind.Adjustment))
            return Result<LeaveMovementDto>.Fail("نوع الحركة يجب أن يكون رصيداً افتتاحياً أو تسوية");
        var note = CoreGuards.Clean(r.Note);
        if (note.Length is 0 or > 1000) return Result<LeaveMovementDto>.Fail("سبب التسوية مطلوب ولا يتجاوز 1000 حرف");
        if (r.Quantity == 0 || Math.Abs(r.Quantity) > 1000) return Result<LeaveMovementDto>.Fail("الكمية غير صالحة");
        if (r.Year is < 2000 or > 2100) return Result<LeaveMovementDto>.Fail("السنة غير صالحة");
        if (kind == LeaveMovementKind.Opening && await db.LeaveBalanceMovements.AnyAsync(m =>
                m.EmployeeId == employee.Id && m.LeaveTypeId == type.Id && m.Year == r.Year && m.Kind == LeaveMovementKind.Opening, ct))
            return Result<LeaveMovementDto>.Fail("الرصيد الافتتاحي مسجّل لهذه السنة. استخدم التسوية للتصحيح");

        var m = new LeaveBalanceMovement
        {
            EmployeeId = employee.Id, LeaveTypeId = type.Id, Year = r.Year, Quantity = r.Quantity, Kind = kind,
            SourceType = "Manual", Note = note,
        };
        db.LeaveBalanceMovements.Add(m);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Adjusted", "LeaveBalance", employee.Id, type.Name, newValues: $"{kind} {r.Quantity} {r.Year}: {note}", ct: ct);
        return Result<LeaveMovementDto>.Ok(new LeaveMovementDto(m.Id, type.Id, type.Name, m.Year, m.Quantity, kind.ToString(),
            m.SourceType, null, m.Note, m.CreatedAt));
    }

    public async Task<Result<LeaveGrantResult>> GrantYearAsync(int year, CancellationToken ct = default)
    {
        if (year is < 2000 or > 2100) return Result<LeaveGrantResult>.Fail("السنة غير صالحة");
        var types = await db.LeaveTypes.AsNoTracking().Where(t => t.IsActive && t.DeductsBalance).ToListAsync(ct);
        var employees = await (await EmployeeService.ScopedQueryAsync(db, scope, ct))
            .Where(e => e.Status == EmployeeStatus.Active).ToListAsync(ct);
        var empIds = employees.Select(e => e.Id).ToList();
        var typeIds = types.Select(t => t.Id).ToList();
        var existing = await db.LeaveBalanceMovements.AsNoTracking()
            .Where(m => empIds.Contains(m.EmployeeId) && typeIds.Contains(m.LeaveTypeId) && (m.Year == year || m.Year == year - 1))
            .ToListAsync(ct);

        int granted = 0, carried = 0, skipped = 0;
        foreach (var e in employees)
        foreach (var t in types.Where(t => t.AppliesTo(e.Category)))
        {
            var mine = existing.Where(m => m.EmployeeId == e.Id && m.LeaveTypeId == t.Id).ToList();
            if (t.AnnualEntitlement > 0)
            {
                if (mine.Any(m => m.Year == year && m.Kind == LeaveMovementKind.Entitlement)) skipped++;
                else
                {
                    db.LeaveBalanceMovements.Add(Movement(e.Id, t.Id, year, t.AnnualEntitlement, LeaveMovementKind.Entitlement, "Grant", null,
                        $"استحقاق سنة {year}"));
                    granted++;
                }
            }

            var last = mine.Where(m => m.Year == year - 1).ToList();
            if (last.Count == 0 || mine.Any(m => m.Year == year && m.Kind == LeaveMovementKind.CarryOver)) continue;
            var remaining = last.Sum(m => m.Quantity);
            if (remaining <= 0) continue;
            var carry = t.MaxCarryOver is { } cap ? Math.Min(remaining, cap) : remaining;
            if (carry > 0)
            {
                db.LeaveBalanceMovements.Add(Movement(e.Id, t.Id, year, carry, LeaveMovementKind.CarryOver, "Grant", null, $"مرحّل من {year - 1}"));
                carried++;
            }
            if (remaining - carry > 0)
                db.LeaveBalanceMovements.Add(Movement(e.Id, t.Id, year - 1, -(remaining - carry), LeaveMovementKind.Expiry, "Grant", null,
                    $"سقط عند الترحيل إلى {year}"));
            // A carry-over marker even when nothing carried, so a second run does not expire twice.
            if (carry == 0)
                db.LeaveBalanceMovements.Add(Movement(e.Id, t.Id, year, 0, LeaveMovementKind.CarryOver, "Grant", null, $"لا ترحيل من {year - 1}"));
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Granted", "LeaveBalance", year, $"استحقاق إجازات {year}",
            newValues: $"granted={granted}, carried={carried}, skipped={skipped}", ct: ct);
        return Result<LeaveGrantResult>.Ok(new LeaveGrantResult(granted, carried, skipped));
    }

    // ---- rules ----

    internal static LeaveBalanceMovement Movement(long employeeId, long typeId, int year, decimal qty, LeaveMovementKind kind,
        string sourceType, long? sourceId, string? note) => new()
    {
        EmployeeId = employeeId, LeaveTypeId = typeId, Year = year, Quantity = qty, Kind = kind,
        SourceType = sourceType, SourceId = sourceId, Note = note,
    };

    /// <summary>
    /// The instant check: quantity, category, dates, substitute and overlaps, then the balance after this
    /// request and every other pending one. Problems are in display order; the first blocks saving.
    /// </summary>
    internal static async Task<LeaveCheckDto> EvaluateAsync(AppDbContext db, Employee employee, LeaveType type,
        SaveLeaveRequest r, long? excludeId, CancellationToken ct)
    {
        var problems = new List<string>();
        decimal qty = 0;
        if (employee.Status != EmployeeStatus.Active) problems.Add("الموظف ليس على رأس عمله");
        if (!type.AppliesTo(employee.Category)) problems.Add("هذا النوع لا ينطبق على فئة الموظف");
        if (r.FromDate == default || r.ToDate == default || r.ToDate < r.FromDate) problems.Add("تاريخ البداية والنهاية غير صالحين");
        else if (r.FromDate.Year != r.ToDate.Year) problems.Add("قسّم الطلب على سنتين: الرصيد سنوي");
        var reason = CoreGuards.Clean(r.Reason);
        if (reason.Length > 1000) problems.Add("السبب أطول من 1000 حرف");

        TimeOnly fromTime = default, toTime = default;
        if (type.Kind == LeaveKind.Departure)
        {
            if (r.FromDate != r.ToDate) problems.Add("المغادرة في يوم واحد");
            if (!HrCommon.TryTime(r.FromTime, out fromTime) || !HrCommon.TryTime(r.ToTime, out toTime) || toTime <= fromTime)
                problems.Add("وقت المغادرة غير صالح");
        }

        if (problems.Count == 0)
        {
            if (type.Kind == LeaveKind.Departure) qty = WorkCalendar.Hours(fromTime, toTime);
            else
            {
                var settings = await HrCommon.SettingsAsync(db, ct);
                var holidays = await HrCommon.HolidaysAsync(db, r.FromDate, r.ToDate, ct);
                qty = WorkCalendar.CountDays(r.FromDate, r.ToDate, type.CountWorkingDaysOnly, settings.Weekend, holidays);
            }
            if (qty <= 0) problems.Add("لا توجد أيام عمل في الفترة المختارة");
        }

        if (type.RequiresSubstitute && r.SubstituteEmployeeId is null) problems.Add("البديل مطلوب لهذا النوع");
        if (r.SubstituteEmployeeId is { } sub)
        {
            var s = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sub, ct);
            if (s is null || s.Status != EmployeeStatus.Active) problems.Add("البديل غير موجود أو ليس على رأس عمله");
            else if (s.Id == employee.Id) problems.Add("لا يكون الموظف بديلاً عن نفسه");
            else if (r.ToDate >= r.FromDate && await OverlapsAsync(db, s.Id, r.FromDate, r.ToDate, null, ct))
                problems.Add("البديل في إجازة خلال الفترة نفسها");
        }

        if (r.ToDate >= r.FromDate && r.FromDate != default)
        {
            var others = await db.LeaveRequests.AsNoTracking().Include(x => x.LeaveType)
                .Where(x => x.EmployeeId == employee.Id && x.Id != (excludeId ?? 0)
                    && (x.Status == LeaveRequestStatus.Submitted || x.Status == LeaveRequestStatus.Approved)
                    && x.FromDate <= r.ToDate && r.FromDate <= x.ToDate)
                .ToListAsync(ct);
            var clash = others.Any(x =>
                type.Kind != LeaveKind.Departure || x.LeaveType.Kind != LeaveKind.Departure
                || (x.FromTime < toTime && fromTime < x.ToTime));
            if (clash) problems.Add("يتداخل مع طلب آخر للموظف في الفترة نفسها");
        }

        decimal? balance = null, after = null;
        var sufficient = true;
        if (type.DeductsBalance && r.FromDate != default)
        {
            var year = r.FromDate.Year;
            var movements = await db.LeaveBalanceMovements.AsNoTracking()
                .Where(m => m.EmployeeId == employee.Id && m.LeaveTypeId == type.Id && m.Year == year).SumAsync(m => m.Quantity, ct);
            var pending = await db.LeaveRequests.AsNoTracking()
                .Where(x => x.EmployeeId == employee.Id && x.LeaveTypeId == type.Id && x.Id != (excludeId ?? 0)
                    && x.Status == LeaveRequestStatus.Submitted && x.FromDate.Year == year)
                .SumAsync(x => x.Quantity, ct);
            balance = movements - pending;
            after = balance - qty;
            sufficient = after >= 0;
            if (!sufficient) problems.Add("الرصيد لا يكفي");
        }
        return new LeaveCheckDto(qty, balance, after, sufficient, problems);
    }

    internal static Task<bool> OverlapsAsync(AppDbContext db, long employeeId, DateOnly from, DateOnly to, long? excludeId, CancellationToken ct) =>
        db.LeaveRequests.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.Id != (excludeId ?? 0)
            && (x.Status == LeaveRequestStatus.Submitted || x.Status == LeaveRequestStatus.Approved)
            && x.LeaveType.Kind != LeaveKind.Departure && x.FromDate <= to && from <= x.ToDate, ct);

    internal static SaveLeaveRequest AsRequest(LeaveRequest l) => new(l.EmployeeId, l.LeaveTypeId, l.FromDate, l.ToDate,
        l.FromTime?.ToString("HH:mm"), l.ToTime?.ToString("HH:mm"), l.SubstituteEmployeeId, l.Reason);

    private static void Apply(LeaveRequest l, SaveLeaveRequest r, LeaveType type, decimal qty)
    {
        l.FromDate = r.FromDate;
        l.ToDate = r.ToDate;
        l.FromTime = type.Kind == LeaveKind.Departure && HrCommon.TryTime(r.FromTime, out var f) ? f : null;
        l.ToTime = type.Kind == LeaveKind.Departure && HrCommon.TryTime(r.ToTime, out var t) ? t : null;
        l.Quantity = qty;
        l.SubstituteEmployeeId = r.SubstituteEmployeeId;
        l.Reason = CoreGuards.Clean(r.Reason);
    }

    /// <summary>Null id = the caller's own employee record; otherwise one inside the caller's scope.</summary>
    private async Task<(Employee?, string?)> EmployeeAsync(long? employeeId, CancellationToken ct)
    {
        if (employeeId is null)
        {
            var me = await HrCommon.CurrentEmployeeAsync(db, user, ct);
            return me is null ? (null, "حسابك غير مرتبط بسجل موظف") : (me, null);
        }
        var me2 = await HrCommon.CurrentEmployeeAsync(db, user, ct);
        if (me2?.Id == employeeId) return (me2, null);
        // Requests for someone else are HR's job: self-service alone never reaches a colleague's record.
        if (!await scope.HasPermissionAsync("HrLeave.Create", ct)) return (null, "لا تملك صلاحية تقديم طلب عن موظف آخر");
        var e = await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        return e is null ? (null, "الموظف غير موجود") : (e, null);
    }

    /// <summary>Requests of employees in scope, plus the caller's own.</summary>
    private async Task<IQueryable<LeaveRequest>> ScopedAsync(CancellationToken ct)
    {
        var q = db.LeaveRequests.AsNoTracking().Include(r => r.LeaveType);
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var me = (await HrCommon.CurrentEmployeeAsync(db, user, ct))?.Id ?? 0;
        var ids = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Select(e => e.Id);
        return q.Where(r => r.EmployeeId == me || ids.Contains(r.EmployeeId));
    }

    private async Task<LeaveRequest?> FindTrackedAsync(long id, CancellationToken ct)
    {
        if (!await (await ScopedAsync(ct)).AnyAsync(r => r.Id == id, ct)) return null;
        return await db.LeaveRequests.Include(r => r.LeaveType).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    internal static async Task<IReadOnlyList<LeaveRequestDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<LeaveRequest> rows, CancellationToken ct)
    {
        var names = await HrCommon.NamesAsync(db, rows.Select(r => r.EmployeeId)
            .Concat(rows.Where(r => r.SubstituteEmployeeId is not null).Select(r => r.SubstituteEmployeeId!.Value)), ct);
        return rows.Select(r => new LeaveRequestDto(r.Id, r.Number, r.EmployeeId, names.GetValueOrDefault(r.EmployeeId),
            r.LeaveTypeId, r.LeaveType?.Name ?? string.Empty, r.LeaveType?.Kind.ToString() ?? string.Empty,
            r.LeaveType?.Unit.ToString() ?? string.Empty, r.FromDate, r.ToDate, r.FromTime?.ToString("HH:mm"),
            r.ToTime?.ToString("HH:mm"), r.Quantity, r.SubstituteEmployeeId,
            r.SubstituteEmployeeId is { } s ? names.GetValueOrDefault(s) : null, r.Reason, r.Status.ToString(),
            r.ApprovalRequestId, r.DecidedAt, r.DecisionNote, r.CreatedAt, HrCommon.B64(r.RowVersion))).ToList();
    }
}

/// <summary>
/// Approved leave: number, balance movement and roster cells in the approval transaction. Throwing
/// <see cref="DomainException"/> rolls the approval back — e.g. when the balance no longer covers it.
/// </summary>
public sealed class LeaveEffectHandler(AppDbContext db, INumberingService numbering, IAuditWriter audit) : IApprovalEffect
{
    public const string Numbering = "LeaveRequest";
    public string RequestType => LeaveService.RequestType;

    public async Task OnApprovedAsync(ApprovalRequest request, CancellationToken ct)
    {
        var l = await LoadAsync(request, ct);
        if (l.Status != LeaveRequestStatus.Submitted) throw new DomainException("الطلب ليس بانتظار الاعتماد");
        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == l.EmployeeId, ct);
        var check = await LeaveService.EvaluateAsync(db, employee, l.LeaveType, LeaveService.AsRequest(l), l.Id, ct);
        if (check.Problems.Count > 0) throw new DomainException(check.Problems[0]);

        l.Number = await numbering.GenerateNumberAsync(Numbering, ct: ct)
            ?? throw new DomainException("قالب ترقيم الإجازات غير معرّف");
        l.Status = LeaveRequestStatus.Approved;
        l.DecidedAt = DateTime.UtcNow;
        l.Quantity = check.Quantity;
        if (l.LeaveType.DeductsBalance)
            db.LeaveBalanceMovements.Add(LeaveService.Movement(l.EmployeeId, l.LeaveTypeId, l.FromDate.Year, -l.Quantity,
                LeaveMovementKind.Taken, "LeaveRequest", l.Id, l.Number));

        // Whole-day leave and missions turn the roster cells into leave (slide 14).
        if (l.LeaveType.Kind != LeaveKind.Departure)
        {
            var cells = await db.ShiftAssignments.Where(a => a.EmployeeId == l.EmployeeId && a.Date >= l.FromDate && a.Date <= l.ToDate)
                .ToDictionaryAsync(a => a.Date, ct);
            for (var d = l.FromDate; d <= l.ToDate; d = d.AddDays(1))
            {
                if (!cells.TryGetValue(d, out var cell))
                    db.ShiftAssignments.Add(cell = new ShiftAssignment { EmployeeId = l.EmployeeId, Date = d, SiteId = employee.SiteId });
                cell.Kind = RosterCellKind.Leave;
                cell.ShiftTypeId = null;
                cell.LeaveRequestId = l.Id;
            }
        }
        if (employee.UserId is { } uid)
            HrCommon.Notify(db, uid, "اعتُمد طلبك", $"{l.LeaveType.Name} {l.Number}", "LeaveRequest", l.Id);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Approved", "LeaveRequest", l.Id, l.Number, newValues: $"{l.Quantity}", ct: ct);
    }

    public async Task OnRejectedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        await DecideAsync(request, LeaveRequestStatus.Rejected, reason, "رُفض طلبك", ct);

    public async Task OnReturnedAsync(ApprovalRequest request, string reason, CancellationToken ct) =>
        await DecideAsync(request, LeaveRequestStatus.Returned, reason, "أُعيد طلبك للتعديل", ct);

    private async Task DecideAsync(ApprovalRequest request, LeaveRequestStatus status, string reason, string title, CancellationToken ct)
    {
        var l = await LoadAsync(request, ct);
        l.Status = status;
        l.DecisionNote = reason;
        l.DecidedAt = DateTime.UtcNow;
        var uid = await db.Employees.Where(e => e.Id == l.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (uid is { } u) HrCommon.Notify(db, u, title, $"{l.LeaveType.Name}: {reason}", "LeaveRequest", l.Id);
        await audit.WriteAsync(status.ToString(), "LeaveRequest", l.Id, l.LeaveType.Name, newValues: reason, ct: ct);
    }

    private async Task<LeaveRequest> LoadAsync(ApprovalRequest request, CancellationToken ct) =>
        await db.LeaveRequests.Include(x => x.LeaveType).FirstOrDefaultAsync(x => x.Id == request.SourceId, ct)
        ?? throw new DomainException("طلب الإجازة غير موجود");
}
