using System.Reflection;
using Archiving.Api.Authorization;
using Archiving.Api.Controllers.Hr;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Services;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

/// <summary>HR phases 2–3: calendar, leave, shifts, attendance, payroll. Every figure is illustrative test data.</summary>
public class HrPhase2Tests
{
    private const long Unit = 10, Workflow = 7;
    private static readonly HashSet<DayOfWeek> FriSat = [DayOfWeek.Friday, DayOfWeek.Saturday];

    private sealed class Scope(bool allowed = true) : IAccessScope
    {
        public Task<bool> IsSystemAdminAsync(CancellationToken ct = default) => Task.FromResult(allowed);
        public Task<bool> HasPermissionAsync(string code, CancellationToken ct = default) => Task.FromResult(allowed);
        public Task<long?> OrgUnitIdAsync(CancellationToken ct = default) => Task.FromResult<long?>(Unit);
        public Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([Unit]);
        public Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([]);
        public Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default) => Task.FromResult(allowed);
    }

    private sealed class StubApprovals : IApprovalService
    {
        public Task<Result<ApprovalRequestDto>> SubmitAsync(SubmitApprovalRequest r, CancellationToken ct = default) =>
            Task.FromResult(Result<ApprovalRequestDto>.Ok(new ApprovalRequestDto(0, "REQ", r.RequestType, r.SourceModule, r.SourceId, r.Title,
                "Pending", null, DateTime.UtcNow, null, null)));
        public Task<IReadOnlyList<ApprovalRequestDto>> ListMineAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalRequestDto>>([]);
        public Task<IReadOnlyList<ApprovalInboxItem>> InboxAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalInboxItem>>([]);
    }

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor(new TestCurrentUser())).Options);
        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.OrgUnits.Add(new OrgUnit { Id = Unit, InstitutionId = 1, Name = "دائرة أ" });
        db.Users.Add(new User { Id = 5, FullName = "محاسب تجريبي", Email = "a@test.invalid" });
        db.Users.Add(new User { Id = 6, FullName = "مدقق تجريبي", Email = "b@test.invalid" });
        db.WorkflowDefinitions.Add(new WorkflowDefinition { Id = Workflow, Name = "مسار تجريبي", IsActive = true });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = LeaveEffectHandler.Numbering, Pattern = "HR-LV-{YYYY}-{SEQ:5}" });
        db.HrSettings.Add(new HrSetting { Key = HrSettingKeys.WeekendDays, Value = "Friday,Saturday" });
        db.SaveChanges();
        return db;
    }

    private static Employee AddEmployee(AppDbContext db, string number, long? userId = null, EmployeeCategory cat = EmployeeCategory.Civil)
    {
        var e = new Employee { EmployeeNumber = number, FullName = $"موظف {number}", Category = cat, OrgUnitId = Unit, JobTitle = "وظيفة", UserId = userId };
        db.Employees.Add(e);
        db.SaveChanges();
        return e;
    }

    // ================= pure rules =================

    [Fact]
    public void Working_days_skip_weekend_and_holidays()
    {
        // Tue 1 Dec 2026 → Sun 6 Dec; Thursday 3rd is a holiday; Fri/Sat weekend → Tue, Wed, Sun.
        var holidays = new HashSet<DateOnly> { new(2026, 12, 3) };
        Assert.Equal(3, WorkCalendar.CountDays(new(2026, 12, 1), new(2026, 12, 6), true, FriSat, holidays));
        Assert.Equal(6, WorkCalendar.CountDays(new(2026, 12, 1), new(2026, 12, 6), false, FriSat, holidays));
        Assert.Equal(0, WorkCalendar.CountDays(new(2026, 12, 6), new(2026, 12, 1), true, FriSat, holidays));
        Assert.Equal(1.5m, WorkCalendar.Hours(new(9, 0), new(10, 30)));
        Assert.Equal(0m, WorkCalendar.Hours(new(10, 0), new(9, 0)));
        Assert.Equal(FriSat, WorkCalendar.ParseWeekend(" friday, Saturday ,nonsense"));
    }

    [Fact]
    public void Attendance_classifier_finds_absent_missing_late_and_early()
    {
        var d = new DateOnly(2026, 10, 5);
        var w = AttendanceClassifier.Window(d, new(8, 0), new(15, 0), 10);
        DateTime At(int h, int m) => d.ToDateTime(new TimeOnly(h, m));

        Assert.Equal(AttendanceExceptionKind.Absent, Assert.Single(AttendanceClassifier.Classify(w, [])).Kind);
        Assert.Equal(AttendanceExceptionKind.MissingPunch, Assert.Single(AttendanceClassifier.Classify(w, [At(8, 0)])).Kind);
        Assert.Empty(AttendanceClassifier.Classify(w, [At(8, 10), At(14, 55)]));        // inside grace both ends
        var late = Assert.Single(AttendanceClassifier.Classify(w, [At(8, 30), At(15, 0)]));
        Assert.Equal((AttendanceExceptionKind.Late, 30), (late.Kind, late.Minutes));
        var early = Assert.Single(AttendanceClassifier.Classify(w, [At(7, 55), At(14, 0)]));
        Assert.Equal((AttendanceExceptionKind.EarlyLeave, 60), (early.Kind, early.Minutes));
    }

    [Fact]
    public void Night_shift_crosses_midnight()
    {
        var d = new DateOnly(2026, 10, 5);
        var w = AttendanceClassifier.Window(d, new(23, 0), new(7, 0), 0);
        Assert.Equal(d.AddDays(1).ToDateTime(new TimeOnly(7, 0)), w.EndLocal);
        Assert.Empty(AttendanceClassifier.Classify(w, [d.ToDateTime(new TimeOnly(22, 55)), d.AddDays(1).ToDateTime(new TimeOnly(7, 2))]));
    }

    [Fact]
    public void Dated_rate_prefers_the_category_row_then_the_latest_date()
    {
        var rows = new[]
        {
            (From: new DateOnly(2026, 1, 1), Cat: (EmployeeCategory?)null, V: 100m),
            (From: new DateOnly(2026, 6, 1), Cat: (EmployeeCategory?)null, V: 110m),
            (From: new DateOnly(2026, 3, 1), Cat: (EmployeeCategory?)EmployeeCategory.Military, V: 200m),
            (From: new DateOnly(2027, 1, 1), Cat: (EmployeeCategory?)EmployeeCategory.Military, V: 999m),
        }.Select(r => new Tuple<DateOnly, EmployeeCategory?, decimal>(r.From, r.Cat, r.V)).ToList();
        var on = new DateOnly(2026, 10, 31);
        Assert.Equal(200m, DatedRate.Pick(rows, on, EmployeeCategory.Military, r => r.Item1, r => r.Item2)!.Item3);
        Assert.Equal(110m, DatedRate.Pick(rows, on, EmployeeCategory.Civil, r => r.Item1, r => r.Item2)!.Item3);
        Assert.Null(DatedRate.Pick(rows, new DateOnly(2025, 1, 1), EmployeeCategory.Civil, r => r.Item1, r => r.Item2));
    }

    [Fact]
    public void Tax_is_progressive_per_bracket()
    {
        (decimal, decimal?, decimal)[] brackets = [(0, 3000, 0), (3000, 5000, 10), (5000, null, 20)];
        Assert.Equal(0m, TaxCalculator.Monthly(2500, brackets));
        Assert.Equal(90m, TaxCalculator.Monthly(3900, brackets));
        Assert.Equal(400m, TaxCalculator.Monthly(6000, brackets));   // 2000×10% + 1000×20%
        Assert.Equal(0m, TaxCalculator.Monthly(-5, brackets));
    }

    [Fact]
    public void Payroll_calculator_builds_lines_and_taxes_after_pre_tax_deductions()
    {
        PayLineInput[] elements =
        [
            new(1, "BASIC", "أساسي", PayElementKind.Earning, PayElementMethod.Basic, true, false, 4000, 1),
            new(2, "TRANS", "مواصلات", PayElementKind.Earning, PayElementMethod.Fixed, true, false, 300, 1),
            new(3, "OT", "إضافي", PayElementKind.Earning, PayElementMethod.PerUnit, false, false, 20, 5),
            new(4, "PENS", "تقاعد", PayElementKind.Deduction, PayElementMethod.PercentOfBasic, false, true, 10, 1),
            new(5, "TAX", "ضريبة", PayElementKind.Deduction, PayElementMethod.Tax, false, false, 0, 1),
        ];
        var r = PayrollCalculator.Calculate(elements, [(0m, 3000m, 0m), (3000m, null, 10m)]);
        Assert.Equal(4400m, r.Gross);                                         // 4000 + 300 + 100
        Assert.Equal(400m, r.Lines.Single(l => l.Code == "PENS").Amount);
        Assert.Equal(90m, r.Lines.Single(l => l.Code == "TAX").Amount);       // (4300 − 400 − 3000) × 10%
        Assert.Equal(490m, r.Deductions);
        Assert.Equal(3910m, r.Net);
    }

    [Fact]
    public void Disbursement_pays_the_larger_of_minimum_and_share_capped_at_owed()
    {
        Assert.Equal(2807m, DisbursementCalculator.Paid(4010, 0, 70, 2000, 5));          // 70% of net
        Assert.Equal(2000m, DisbursementCalculator.Paid(2500, 0, 70, 2000, 5));          // minimum wins
        Assert.Equal(1500m, DisbursementCalculator.Paid(1500, 0, 70, 2000, 5));          // never more than owed
        Assert.Equal(2850m, DisbursementCalculator.Paid(3000, 1000, 70, 2000, 75));      // 2100 + 750 of the arrears
    }

    // ================= leave =================

    private static (LeaveService Service, LeaveType Type) LeaveSetup(AppDbContext db, bool substitute = false, bool permission = true)
    {
        var t = new LeaveType
        {
            Code = "ANNUAL", Name = "إجازة تجريبية", Kind = LeaveKind.Leave, Unit = LeaveUnit.Days, AnnualEntitlement = 14,
            MaxCarryOver = 5, RequiresSubstitute = substitute, WorkflowDefinitionId = Workflow,
        };
        db.LeaveTypes.Add(t);
        db.PublicHolidays.Add(new PublicHoliday { Date = new DateOnly(2026, 12, 3), Name = "عطلة تجريبية" });
        db.SaveChanges();
        return (new LeaveService(db, new Scope(permission), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals()), t);
    }

    private static async Task ApproveAsync(AppDbContext db, long leaveId, WorkflowStatus status = WorkflowStatus.Completed, string? note = null)
    {
        var request = new ApprovalRequest { Number = $"REQ-{leaveId}", RequestType = LeaveService.RequestType, SourceModule = "HR", SourceId = leaveId, Title = "t" };
        db.ApprovalRequests.Add(request);
        await db.SaveChangesAsync();
        var effect = new LeaveEffectHandler(db, new NumberingService(db, new NoopAuditWriter()), new NoopAuditWriter());
        await new ApprovalOutcomeHandler(db, [effect]).HandleAsync(request.Id, status, note, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Approved_leave_deducts_working_days_numbers_it_and_locks_roster_cells()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var (svc, type) = LeaveSetup(db);
        Assert.Equal(1, (await svc.GrantYearAsync(2026)).Value!.Granted);
        Assert.Equal(0, (await svc.GrantYearAsync(2026)).Value!.Granted);          // idempotent

        var req = new SaveLeaveRequest(e.Id, type.Id, new(2026, 12, 1), new(2026, 12, 6), null, null, null, "سبب");
        var check = (await svc.CheckAsync(req, null)).Value!;
        Assert.Equal((3m, 14m, 11m, true), (check.Quantity, check.Balance!.Value, check.BalanceAfter!.Value, check.Sufficient));

        var created = await svc.CreateAsync(req);
        Assert.True(created.Succeeded, created.Error);
        Assert.True((await svc.SubmitAsync(created.Value!.Id)).Succeeded);
        await ApproveAsync(db, created.Value.Id);

        var l = await db.LeaveRequests.SingleAsync();
        Assert.Equal(LeaveRequestStatus.Approved, l.Status);
        Assert.Equal("HR-LV-2026-00001", l.Number);
        Assert.Equal(11m, (await svc.BalancesAsync(e.Id, 2026)).Value!.Single().Balance);
        Assert.Equal(6, await db.ShiftAssignments.CountAsync(a => a.EmployeeId == e.Id && a.Kind == RosterCellKind.Leave));

        // The same days again overlap; more days than the balance are refused.
        Assert.False((await svc.CreateAsync(req)).Succeeded);
        var tooLong = await svc.CreateAsync(req with { FromDate = new(2026, 12, 13), ToDate = new(2026, 12, 31) });
        Assert.Equal("الرصيد لا يكفي", tooLong.Error);
    }

    [Fact]
    public async Task Rejection_keeps_the_balance_and_records_the_reason()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var (svc, type) = LeaveSetup(db);
        await svc.GrantYearAsync(2026);
        var created = await svc.CreateAsync(new SaveLeaveRequest(e.Id, type.Id, new(2026, 12, 1), new(2026, 12, 1), null, null, null, "سبب"));
        await svc.SubmitAsync(created.Value!.Id);
        await ApproveAsync(db, created.Value.Id, WorkflowStatus.Rejected, "سبب الرفض");
        var l = await db.LeaveRequests.SingleAsync();
        Assert.Equal((LeaveRequestStatus.Rejected, "سبب الرفض"), (l.Status, l.DecisionNote));
        Assert.Equal(14m, (await svc.BalancesAsync(e.Id, 2026)).Value!.Single().Balance);
    }

    [Fact]
    public async Task Substitute_is_required_when_the_type_says_so_and_cannot_be_on_leave()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var sub = AddEmployee(db, "T-2");
        var (svc, type) = LeaveSetup(db, substitute: true);
        await svc.GrantYearAsync(2026);
        var req = new SaveLeaveRequest(e.Id, type.Id, new(2026, 12, 1), new(2026, 12, 2), null, null, null, "سبب");
        Assert.Equal("البديل مطلوب لهذا النوع", (await svc.CreateAsync(req)).Error);
        Assert.Equal("لا يكون الموظف بديلاً عن نفسه", (await svc.CreateAsync(req with { SubstituteEmployeeId = e.Id })).Error);

        var subLeave = await svc.CreateAsync(new SaveLeaveRequest(sub.Id, type.Id, new(2026, 12, 2), new(2026, 12, 2), null, null, e.Id, "سبب"));
        await svc.SubmitAsync(subLeave.Value!.Id);
        Assert.Equal("البديل في إجازة خلال الفترة نفسها", (await svc.CreateAsync(req with { SubstituteEmployeeId = sub.Id })).Error);
    }

    [Fact]
    public async Task Self_service_cannot_file_leave_for_a_colleague()
    {
        var db = NewDb();
        var other = AddEmployee(db, "T-9");
        AddEmployee(db, "T-ME", userId: 5);                                   // the caller (TestCurrentUser = 5)
        var (svc, type) = LeaveSetup(db, permission: false);
        var r = await svc.CreateAsync(new SaveLeaveRequest(other.Id, type.Id, new(2026, 12, 1), new(2026, 12, 1), null, null, null, "سبب"));
        Assert.False(r.Succeeded);
        Assert.Contains("موظف آخر", r.Error);
    }

    [Fact]
    public async Task Year_grant_carries_over_up_to_the_cap_and_expires_the_rest()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var (svc, type) = LeaveSetup(db);
        await svc.GrantYearAsync(2026);                                         // 14 in 2026, none taken
        await svc.GrantYearAsync(2027);
        var y2027 = await db.LeaveBalanceMovements.Where(m => m.Year == 2027).ToListAsync();
        Assert.Equal(5m, y2027.Single(m => m.Kind == LeaveMovementKind.CarryOver).Quantity);
        Assert.Equal(-9m, (await db.LeaveBalanceMovements.SingleAsync(m => m.Kind == LeaveMovementKind.Expiry)).Quantity);
        Assert.Equal(19m, (await svc.BalancesAsync(e.Id, 2027)).Value!.Single().Balance);
        await svc.GrantYearAsync(2027);                                         // second run changes nothing
        Assert.Equal(1, await db.LeaveBalanceMovements.CountAsync(m => m.Kind == LeaveMovementKind.Expiry));
    }

    [Fact]
    public async Task Adjustment_needs_a_reason_and_opening_is_once_per_year()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var (svc, type) = LeaveSetup(db);
        Assert.False((await svc.AdjustAsync(new LeaveAdjustmentRequest(e.Id, type.Id, 2026, 3, "Opening", " "))).Succeeded);
        Assert.True((await svc.AdjustAsync(new LeaveAdjustmentRequest(e.Id, type.Id, 2026, 3, "Opening", "رصيد الانطلاق"))).Succeeded);
        Assert.False((await svc.AdjustAsync(new LeaveAdjustmentRequest(e.Id, type.Id, 2026, 3, "Opening", "مرة ثانية"))).Succeeded);
        Assert.False((await svc.AdjustAsync(new LeaveAdjustmentRequest(e.Id, type.Id, 2026, 3, "Taken", "ممنوع"))).Succeeded);
    }

    // ================= attendance =================

    [Fact]
    public async Task Attendance_flags_late_and_absent_resolves_with_a_reason_and_blocks_close_while_pending()
    {
        var db = NewDb();
        var e = AddEmployee(db, "T-1");
        var shift = new ShiftType { Code = "DAY", Name = "دوام", StartTime = new(8, 0), EndTime = new(15, 0), GraceMinutes = 10 };
        var annual = new LeaveType { Code = "ANNUAL", Name = "سنوية", Kind = LeaveKind.Leave, Unit = LeaveUnit.Days };
        db.ShiftTypes.Add(shift);
        db.LeaveTypes.Add(annual);
        db.SaveChanges();
        db.HrSettings.Add(new HrSetting { Key = HrSettingKeys.DefaultShiftTypeId, Value = shift.Id.ToString() });
        db.HrSettings.Add(new HrSetting { Key = HrSettingKeys.AbsenceLeaveTypeId, Value = annual.Id.ToString() });
        db.SaveChanges();
        var svc = new AttendanceService(db, new Scope(), new TestCurrentUser(), new NoopAuditWriter());

        var imported = await svc.ImportAsync(new ImportPunchesRequest("Device",
        [
            new("T-1", "2026-10-05T08:30:00", "GATE-1"), new("T-1", "2026-10-05T15:00:00", "GATE-1"),
            new("T-1", "2026-10-05T15:00:00", "GATE-1"), new("NOPE", "2026-10-05T08:00:00", null),
        ]));
        Assert.Equal((2, 1, 1), (imported.Value!.Imported, imported.Value.Duplicates, imported.Value.Errors.Count));

        // Mon 5 Oct late; Tue 6 Oct absent.
        Assert.True((await svc.ProcessAsync(new ProcessAttendanceRequest(new(2026, 10, 5), new(2026, 10, 6)))).Succeeded);
        var found = await db.AttendanceExceptions.OrderBy(x => x.Date).ToListAsync();
        Assert.Equal([(AttendanceExceptionKind.Late, 30), (AttendanceExceptionKind.Absent, 420)], found.Select(x => (x.Kind, x.Minutes)));

        // Reprocessing does not duplicate.
        await svc.ProcessAsync(new ProcessAttendanceRequest(new(2026, 10, 5), new(2026, 10, 6)));
        Assert.Equal(2, await db.AttendanceExceptions.CountAsync());

        var absent = found[1];
        Assert.False((await svc.ResolveAsync(absent.Id, new ResolveExceptionRequest("DeductLeave", ""))).Succeeded);
        Assert.False((await svc.ResolveAsync(found[0].Id, new ResolveExceptionRequest("DeductLeave", "خصم"))).Succeeded); // late → not whole day
        Assert.True((await svc.ResolveAsync(absent.Id, new ResolveExceptionRequest("DeductLeave", "غياب دون عذر"))).Succeeded);
        Assert.Equal(-1m, (await db.LeaveBalanceMovements.SingleAsync()).Quantity);
        Assert.Equal("عولج الاستثناء مسبقاً", (await svc.ResolveAsync(absent.Id, new ResolveExceptionRequest("Justified", "مرة ثانية"))).Error);

        Assert.False((await svc.CloseAsync(2026, 10)).Succeeded);               // late still pending
        Assert.True((await svc.ResolveAsync(found[0].Id, new ResolveExceptionRequest("DeductSalary", "تأخير"))).Succeeded);
        var closed = await svc.CloseAsync(2026, 10);
        Assert.True(closed.Succeeded, closed.Error);
        var late = await svc.ImportAsync(new ImportPunchesRequest("Import", [new("T-1", "2026-10-20T08:00:00", null)]));
        Assert.Equal("سطر 1: الشهر مقفل", Assert.Single(late.Value!.Errors));
    }

    [Fact]
    public void Punch_times_without_offset_are_hebron_wall_clock()
    {
        Assert.True(AttendanceService.TryUtc("2026-10-05T08:30:00", out var local));
        Assert.Equal(new DateTime(2026, 10, 5, 5, 30, 0), local);              // UTC+3 in October
        Assert.True(AttendanceService.TryUtc("2026-10-05T08:30:00Z", out var utc));
        Assert.Equal(new DateTime(2026, 10, 5, 8, 30, 0), utc);
        Assert.False(AttendanceService.TryUtc("not a time", out _));
    }

    // ================= shifts =================

    [Fact]
    public async Task Roster_counts_coverage_and_never_overwrites_leave_cells()
    {
        var db = NewDb();
        var site = new Site { Name = "معبر تجريبي", Code = "S1" };
        db.Sites.Add(site);
        db.SaveChanges();
        var a = AddEmployee(db, "T-1");
        var b = AddEmployee(db, "T-2");
        a.SiteId = b.SiteId = site.Id;
        var night = new ShiftType { Code = "N", Name = "ليلي", StartTime = new(23, 0), EndTime = new(7, 0), IsNight = true };
        db.ShiftTypes.Add(night);
        db.SaveChanges();
        db.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = b.Id, Date = new(2026, 12, 2), Kind = RosterCellKind.Leave });
        db.SaveChanges();

        var svc = new ShiftService(db, new Scope(), new NoopAuditWriter());
        await svc.SaveCoverageRuleAsync(new SaveCoverageRuleRequest(site.Id, night.Id, 2));
        var saved = await svc.SaveCellsAsync(site.Id, 2026, 12, new SaveRosterCellsRequest([new(a.Id, new(2026, 12, 1), "Shift", night.Id)]));
        Assert.True(saved.Succeeded, saved.Error);
        var day1 = saved.Value!.Coverage.Single(c => c.Date == new DateOnly(2026, 12, 1) && c.ShiftTypeId == night.Id);
        Assert.Equal((1, 2), (day1.Count, day1.MinCount));

        var onLeave = await svc.SaveCellsAsync(site.Id, 2026, 12, new SaveRosterCellsRequest([new(b.Id, new(2026, 12, 2), "Shift", night.Id)]));
        Assert.False(onLeave.Succeeded);
        Assert.Equal("Published", (await svc.PublishAsync(site.Id, 2026, 12)).Value!.Status);
    }

    // ================= payroll =================

    [Fact]
    public async Task Payroll_month_runs_its_stages_with_segregation_and_books_entitlements()
    {
        var db = NewDb();
        var grade = new Lookup { Category = LookupCategories.Grade, Code = "G1", Name = "درجة تجريبية" };
        db.Lookups.Add(grade);
        db.SaveChanges();
        var e = AddEmployee(db, "T-1");
        db.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = e.Id, GradeId = grade.Id });
        var night = new ShiftType { Code = "N", Name = "ليلي", StartTime = new(23, 0), EndTime = new(7, 0), IsNight = true };
        db.ShiftTypes.Add(night);
        PayElement El(string code, PayElementKind k, PayElementMethod m, PayElementSource s = PayElementSource.Manual, bool tax = false, bool pre = false)
        {
            var x = new PayElement { Code = code, Name = code, Kind = k, Method = m, Source = s, IsTaxable = tax, IsPreTax = pre };
            db.PayElements.Add(x);
            return x;
        }
        El("BASIC", PayElementKind.Earning, PayElementMethod.Basic, tax: true);
        var trans = El("TRANS", PayElementKind.Earning, PayElementMethod.Fixed, tax: true);
        var ot = El("OT", PayElementKind.Earning, PayElementMethod.PerUnit);
        var nights = El("NIGHT", PayElementKind.Earning, PayElementMethod.PerUnit, PayElementSource.NightShifts);
        var pens = El("PENS", PayElementKind.Deduction, PayElementMethod.PercentOfBasic, pre: true);
        El("TAX", PayElementKind.Deduction, PayElementMethod.Tax);
        db.SaveChanges();
        var from = new DateOnly(2026, 1, 1);
        db.SalaryScaleRates.Add(new SalaryScaleRate { GradeId = grade.Id, EffectiveFrom = from, BasicAmount = 4000 });
        db.PayElementRates.AddRange(
            new PayElementRate { PayElementId = trans.Id, EffectiveFrom = from, Value = 300 },
            new PayElementRate { PayElementId = ot.Id, EffectiveFrom = from, Value = 20 },
            new PayElementRate { PayElementId = nights.Id, EffectiveFrom = from, Value = 50 },
            new PayElementRate { PayElementId = pens.Id, EffectiveFrom = from, Value = 10 });
        db.TaxBrackets.AddRange(new TaxBracket { EffectiveFrom = from, FromAmount = 0, ToAmount = 3000, Rate = 0 },
            new TaxBracket { EffectiveFrom = from, FromAmount = 3000, Rate = 10 });
        db.ShiftAssignments.AddRange(
            new ShiftAssignment { EmployeeId = e.Id, Date = new(2026, 10, 1), Kind = RosterCellKind.Shift, ShiftTypeId = night.Id },
            new ShiftAssignment { EmployeeId = e.Id, Date = new(2026, 10, 2), Kind = RosterCellKind.Shift, ShiftTypeId = night.Id });
        db.SaveChanges();

        var user = new MutableCurrentUser { UserId = 5 };
        var svc = new PayrollService(db, new Scope(), user, new NoopAuditWriter());
        var p = (await svc.OpenAsync(new OpenPayrollRequest(2026, 10))).Value!;
        Assert.False((await svc.OpenAsync(new OpenPayrollRequest(2026, 10))).Succeeded);
        Assert.True((await svc.AddInputAsync(p.Id, new AddPayrollInputRequest(e.Id, ot.Id, 5, null))).Succeeded);   // Open → Inputs
        Assert.Equal("Calculated", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Value!.Stage);

        var result = (await svc.ResultsAsync(p.Id)).Value!.Single();
        // 4000 + 300 + 5×20 + 2×50 = 4500; pension 400; tax (4300 − 400 − 3000) × 10% = 90.
        Assert.Equal((4500m, 490m, 4010m), (result.Gross, result.Deductions, result.Net));

        Assert.Equal("Reviewed", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Value!.Stage);
        Assert.Equal("من احتسب الرواتب لا يدققها ولا يعتمدها", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Error);
        user.UserId = 6;
        Assert.Equal("Audited", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Value!.Stage);
        Assert.Equal("Approved", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Value!.Stage);

        var rules = new DisbursementRequest([new("Civil", 70, 2000, 5)], 2000);
        Assert.False((await svc.SimulateAsync(p.Id, rules)).Value!.WithinLiquidity);
        Assert.False((await svc.ApplyDisbursementAsync(p.Id, rules)).Succeeded);
        var applied = await svc.ApplyDisbursementAsync(p.Id, rules with { AvailableLiquidity = 10_000 });
        Assert.Equal("Disbursed", applied.Value!.Stage);
        var slip = (await svc.PayslipAsync(p.Id, e.Id)).Value!;
        Assert.Equal((2807m, 1203m, 1203m), (slip.Paid!.Value, slip.Carried!.Value, slip.EntitlementBalance));

        Assert.Equal("Closed", (await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Value!.Stage);
        Assert.False((await svc.AddInputAsync(p.Id, new AddPayrollInputRequest(e.Id, ot.Id, 1, null))).Succeeded);
        Assert.False((await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null))).Succeeded);
    }

    [Fact]
    public async Task Payroll_reports_missing_basic_instead_of_guessing()
    {
        var db = NewDb();
        AddEmployee(db, "T-1");
        db.PayElements.Add(new PayElement { Code = "BASIC", Name = "أساسي", Kind = PayElementKind.Earning, Method = PayElementMethod.Basic });
        db.SaveChanges();
        var svc = new PayrollService(db, new Scope(), new MutableCurrentUser(), new NoopAuditWriter());
        var p = (await svc.OpenAsync(new OpenPayrollRequest(2026, 10))).Value!;
        await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null));
        await svc.AdvanceAsync(p.Id, new AdvancePayrollRequest(null));
        var calc = await svc.CalculateAsync(p.Id);
        Assert.Contains("لا راتب أساسي", Assert.Single(calc.Value!.Warnings));
        Assert.Equal(0m, (await svc.ResultsAsync(p.Id)).Value!.Single().Net);
    }

    [Fact]
    public async Task Tax_table_must_be_contiguous()
    {
        var db = NewDb();
        var svc = new PayrollSettingsService(db, new Scope(), new NoopAuditWriter());
        Assert.False((await svc.SaveTaxTableAsync(new SaveTaxTableRequest(new(2026, 1, 1), [new(0, 3000, 0), new(3500, null, 10)]))).Succeeded);
        Assert.True((await svc.SaveTaxTableAsync(new SaveTaxTableRequest(new(2026, 1, 1), [new(0, 3000, 0), new(3000, null, 10)]))).Succeeded);
        Assert.False((await svc.SaveTaxTableAsync(new SaveTaxTableRequest(new(2026, 1, 1), [new(0, null, 5)]))).Succeeded);
    }

    // ================= org structure =================

    [Fact]
    public async Task Direct_manager_walks_up_to_the_first_filled_manager_seat()
    {
        var db = NewDb();
        db.OrgUnits.Add(new OrgUnit { Id = 11, InstitutionId = 1, ParentId = Unit, Name = "قسم فرعي" });
        var seat = new Position { Title = "مدير الدائرة", OrgUnitId = Unit };
        db.Positions.Add(seat);
        db.SaveChanges();
        db.OrgUnits.Find(Unit)!.ManagerPositionId = seat.Id;
        var boss = AddEmployee(db, "T-BOSS");
        boss.PositionId = seat.Id;
        var staff = new Employee { EmployeeNumber = "T-2", FullName = "موظف فرعي", OrgUnitId = 11, JobTitle = "x" };
        db.Employees.Add(staff);
        db.SaveChanges();

        Assert.Equal(boss.Id, await HrCalendarService.ResolveDirectManagerAsync(db, staff.Id, CancellationToken.None));
        Assert.Null(await HrCalendarService.ResolveDirectManagerAsync(db, boss.Id, CancellationToken.None));   // nobody above
    }

    [Fact]
    public async Task Org_unit_cannot_move_under_its_own_child_and_manager_seat_must_be_its_own()
    {
        var db = NewDb();
        db.OrgUnits.Add(new OrgUnit { Id = 11, InstitutionId = 1, ParentId = Unit, Name = "قسم فرعي" });
        var otherSeat = new Position { Title = "مقعد فرعي", OrgUnitId = 11 };
        db.Positions.Add(otherSeat);
        db.SaveChanges();
        var org = new OrganizationService(db, new NoopAuditWriter(), new TestCurrentUser());
        var cycle = await org.UpdateOrgUnitAsync(Unit, new Archiving.Application.Features.Organization.UpdateOrgUnitRequest(
            11, "دائرة أ", null, null, OrgUnitType.Department, 0, true));
        Assert.False(cycle.Succeeded);
        var seat = await org.UpdateOrgUnitAsync(Unit, new Archiving.Application.Features.Organization.UpdateOrgUnitRequest(
            null, "دائرة أ", null, null, OrgUnitType.Department, 0, true, otherSeat.Id));
        Assert.Equal("مقعد المدير يجب أن يكون من مقاعد الوحدة نفسها", seat.Error);
    }

    // ================= endpoints =================

    private static readonly Type[] Controllers =
    [
        typeof(HrCalendarController), typeof(HrLeaveTypesController), typeof(HrLeaveController), typeof(HrShiftsController),
        typeof(HrAttendanceController), typeof(HrPayrollSettingsController), typeof(HrPayrollController),
    ];

    [Fact]
    public void Every_phase2_endpoint_declares_a_known_permission_and_writes_are_not_view_only()
    {
        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attr = action.GetCustomAttribute<HasPermissionAttribute>();
            Assert.True(attr is not null, $"{controller.Name}.{action.Name} has no permission");
            var code = attr!.Policy![HasPermissionAttribute.PolicyPrefix.Length..];
            Assert.Contains(code, PermissionCatalog.AllCodes);
            var verb = action.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault()?.HttpMethods.Single();
            // Simulate is a read that takes a body; it changes nothing.
            if (verb is null or "GET" || action.Name == "Simulate") continue;
            Assert.False(code.EndsWith(".View"), $"{controller.Name}.{action.Name} ({verb}) is guarded only by a View permission");
        }
    }

    [Fact]
    public void Managers_do_not_see_payroll_and_everyone_gets_self_service()
    {
        var manager = PermissionCatalog.RoleDefaults.Single(r => r.Name == "Manager");
        Assert.False(manager.Pick(new Permission { Resource = "HrPayroll", Action = PermissionAction.View }));
        foreach (var role in PermissionCatalog.RoleDefaults)
            Assert.True(role.Pick(new Permission { Resource = "SelfService", Action = PermissionAction.Create }), role.Name);
    }
}
