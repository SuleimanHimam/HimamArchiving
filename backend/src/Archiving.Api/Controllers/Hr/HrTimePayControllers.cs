using Archiving.Api.Authorization;
using Archiving.Application.Features.Hr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers.Hr;

// HR phases 2–3. Endpoints only: permission, call, result. Scope and rules live in the services.

[ApiController]
[Route("api/hr/calendar")]
[Authorize]
public sealed class HrCalendarController(IHrCalendarService calendar) : ControllerBase
{
    [HttpGet("settings")]
    [HasPermission("HrAttendance.View")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await calendar.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveHrSettingsRequest request, CancellationToken ct) =>
        this.From(await calendar.SaveSettingsAsync(request, ct));

    [HttpGet("holidays")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> Holidays([FromQuery] int year, CancellationToken ct) => Ok(await calendar.HolidaysAsync(year, ct));

    [HttpPost("holidays")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> AddHoliday([FromBody] SaveHolidayRequest request, CancellationToken ct) =>
        this.From(await calendar.AddHolidayAsync(request, ct));

    [HttpDelete("holidays/{id:long}")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> DeleteHoliday(long id, CancellationToken ct) => this.Deleted(await calendar.DeleteHolidayAsync(id, ct));

    [HttpGet("manager/{employeeId:long}")]
    [HasPermission("Employees.View")]
    public async Task<IActionResult> Manager(long employeeId, CancellationToken ct) => this.From(await calendar.DirectManagerAsync(employeeId, ct));
}

[ApiController]
[Route("api/hr/leave-types")]
[Authorize]
public sealed class HrLeaveTypesController(ILeaveTypeService types) : ControllerBase
{
    [HttpGet]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await types.ListAsync(activeOnly, ct));

    [HttpPost]
    [HasPermission("HrLeave.Edit")]
    public async Task<IActionResult> Create([FromBody] SaveLeaveTypeRequest request, CancellationToken ct) => this.From(await types.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("HrLeave.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveLeaveTypeRequest request, CancellationToken ct) =>
        this.From(await types.UpdateAsync(id, request, ct));
}

[ApiController]
[Route("api/hr/leave")]
[Authorize]
public sealed class HrLeaveController(ILeaveService leave) : ControllerBase
{
    [HttpGet]
    [HasPermission("HrLeave.View")]
    public async Task<IActionResult> List([FromQuery] LeaveListQuery query, CancellationToken ct) => Ok(await leave.ListAsync(query, ct));

    [HttpGet("mine")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await leave.MineAsync(ct));

    /// <summary>Instant check; the service allows the caller's own record or one in scope.</summary>
    [HttpPost("check")]
    [HasPermission("SelfService.Create")]
    public async Task<IActionResult> Check([FromBody] SaveLeaveRequest request, [FromQuery] long? excludeId, CancellationToken ct) =>
        this.From(await leave.CheckAsync(request, excludeId, ct));

    [HttpPost]
    [HasPermission("SelfService.Create")]
    public async Task<IActionResult> Create([FromBody] SaveLeaveRequest request, CancellationToken ct) => this.From(await leave.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("SelfService.Create")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveLeaveRequest request, CancellationToken ct) =>
        this.From(await leave.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/submit")]
    [HasPermission("SelfService.Create")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) => this.From(await leave.SubmitAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("SelfService.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => this.From(await leave.CancelAsync(id, ct));

    [HttpGet("balances/mine")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> MyBalances([FromQuery] int year, CancellationToken ct) => this.From(await leave.BalancesAsync(null, year, ct));

    [HttpGet("balances/{employeeId:long}")]
    [HasPermission("HrLeave.View")]
    public async Task<IActionResult> Balances(long employeeId, [FromQuery] int year, CancellationToken ct) =>
        this.From(await leave.BalancesAsync(employeeId, year, ct));

    [HttpGet("movements/{employeeId:long}")]
    [HasPermission("HrLeave.View")]
    public async Task<IActionResult> Movements(long employeeId, [FromQuery] int year, CancellationToken ct) =>
        this.From(await leave.MovementsAsync(employeeId, year, ct));

    [HttpPost("adjustments")]
    [HasPermission("HrLeave.Edit")]
    public async Task<IActionResult> Adjust([FromBody] LeaveAdjustmentRequest request, CancellationToken ct) => this.From(await leave.AdjustAsync(request, ct));

    [HttpPost("grant/{year:int}")]
    [HasPermission("HrLeave.Edit")]
    public async Task<IActionResult> Grant(int year, CancellationToken ct) => this.From(await leave.GrantYearAsync(year, ct));
}

[ApiController]
[Route("api/hr/shifts")]
[Authorize]
public sealed class HrShiftsController(IShiftService shifts) : ControllerBase
{
    [HttpGet("types")]
    [HasPermission("HrShifts.View")]
    public async Task<IActionResult> Types([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await shifts.TypesAsync(activeOnly, ct));

    [HttpPost("types")]
    [HasPermission("HrShifts.Edit")]
    public async Task<IActionResult> CreateType([FromBody] SaveShiftTypeRequest request, CancellationToken ct) =>
        this.From(await shifts.CreateTypeAsync(request, ct));

    [HttpPut("types/{id:long}")]
    [HasPermission("HrShifts.Edit")]
    public async Task<IActionResult> UpdateType(long id, [FromBody] SaveShiftTypeRequest request, CancellationToken ct) =>
        this.From(await shifts.UpdateTypeAsync(id, request, ct));

    [HttpGet("coverage/{siteId:long}")]
    [HasPermission("HrShifts.View")]
    public async Task<IActionResult> Coverage(long siteId, CancellationToken ct) => Ok(await shifts.CoverageRulesAsync(siteId, ct));

    [HttpPut("coverage")]
    [HasPermission("HrShifts.Edit")]
    public async Task<IActionResult> SaveCoverage([FromBody] SaveCoverageRuleRequest request, CancellationToken ct) =>
        this.From(await shifts.SaveCoverageRuleAsync(request, ct));

    [HttpGet("roster/{siteId:long}/{year:int}/{month:int}")]
    [HasPermission("HrShifts.View")]
    public async Task<IActionResult> Roster(long siteId, int year, int month, CancellationToken ct) =>
        this.From(await shifts.RosterAsync(siteId, year, month, ct));

    [HttpPut("roster/{siteId:long}/{year:int}/{month:int}")]
    [HasPermission("HrShifts.Edit")]
    public async Task<IActionResult> SaveCells(long siteId, int year, int month, [FromBody] SaveRosterCellsRequest request, CancellationToken ct) =>
        this.From(await shifts.SaveCellsAsync(siteId, year, month, request, ct));

    [HttpPost("roster/{siteId:long}/{year:int}/{month:int}/publish")]
    [HasPermission("HrShifts.Edit")]
    public async Task<IActionResult> Publish(long siteId, int year, int month, CancellationToken ct) =>
        this.From(await shifts.PublishAsync(siteId, year, month, ct));
}

[ApiController]
[Route("api/hr/attendance")]
[Authorize]
public sealed class HrAttendanceController(IAttendanceService attendance) : ControllerBase
{
    [HttpPost("punches/import")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> Import([FromBody] ImportPunchesRequest request, CancellationToken ct) =>
        this.From(await attendance.ImportAsync(request, ct));

    [HttpPost("process")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> Process([FromBody] ProcessAttendanceRequest request, CancellationToken ct) =>
        this.From(await attendance.ProcessAsync(request, ct));

    [HttpGet("day")]
    [HasPermission("HrAttendance.View")]
    public async Task<IActionResult> Day([FromQuery] DateOnly date, [FromQuery] long? siteId, CancellationToken ct) =>
        Ok(await attendance.DayAsync(date, siteId, ct));

    [HttpGet("mine/{year:int}/{month:int}")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> Mine(int year, int month, CancellationToken ct) => this.From(await attendance.MineAsync(year, month, ct));

    [HttpGet("exceptions")]
    [HasPermission("HrAttendance.View")]
    public async Task<IActionResult> Exceptions([FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await attendance.ExceptionsAsync(status, from, to, ct));

    [HttpPost("exceptions/{id:long}/resolve")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolveExceptionRequest request, CancellationToken ct) =>
        this.From(await attendance.ResolveAsync(id, request, ct));

    [HttpGet("periods/{year:int}/{month:int}")]
    [HasPermission("HrAttendance.View")]
    public async Task<IActionResult> Period(int year, int month, CancellationToken ct) => Ok(await attendance.PeriodAsync(year, month, ct));

    [HttpPost("periods/{year:int}/{month:int}/close")]
    [HasPermission("HrAttendance.Edit")]
    public async Task<IActionResult> Close(int year, int month, CancellationToken ct) => this.From(await attendance.CloseAsync(year, month, ct));
}

[ApiController]
[Route("api/hr/payroll-settings")]
[Authorize]
public sealed class HrPayrollSettingsController(IPayrollSettingsService settings) : ControllerBase
{
    [HttpGet("elements")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Elements([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await settings.ElementsAsync(activeOnly, ct));

    [HttpPost("elements")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> CreateElement([FromBody] SavePayElementRequest request, CancellationToken ct) =>
        this.From(await settings.CreateElementAsync(request, ct));

    [HttpPut("elements/{id:long}")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> UpdateElement(long id, [FromBody] SavePayElementRequest request, CancellationToken ct) =>
        this.From(await settings.UpdateElementAsync(id, request, ct));

    [HttpGet("rates")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Rates([FromQuery] long? payElementId, CancellationToken ct) => Ok(await settings.RatesAsync(payElementId, ct));

    [HttpPost("rates")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> AddRate([FromBody] AddPayElementRateRequest request, CancellationToken ct) =>
        this.From(await settings.AddRateAsync(request, ct));

    [HttpGet("scale")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Scale(CancellationToken ct) => Ok(await settings.ScaleAsync(ct));

    [HttpPost("scale")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> AddScale([FromBody] AddSalaryScaleRateRequest request, CancellationToken ct) =>
        this.From(await settings.AddScaleRateAsync(request, ct));

    [HttpGet("employees/{employeeId:long}")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> EmployeeElements(long employeeId, CancellationToken ct) =>
        this.From(await settings.EmployeeElementsAsync(employeeId, ct));

    [HttpPost("employees")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> AddEmployeeElement([FromBody] AddEmployePayElementRequest request, CancellationToken ct) =>
        this.From(await settings.AddEmployeeElementAsync(request, ct));

    [HttpPost("employees/items/{id:long}/end")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> EndEmployeeElement(long id, [FromBody] EndEmployeePayElementRequest request, CancellationToken ct) =>
        this.From(await settings.EndEmployeeElementAsync(id, request, ct));

    [HttpGet("tax")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Tax(CancellationToken ct) => Ok(await settings.TaxAsync(ct));

    [HttpPost("tax")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> SaveTax([FromBody] SaveTaxTableRequest request, CancellationToken ct) =>
        this.From(await settings.SaveTaxTableAsync(request, ct));
}

[ApiController]
[Route("api/hr/payroll")]
[Authorize]
public sealed class HrPayrollController(IPayrollService payroll) : ControllerBase
{
    [HttpGet]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Periods(CancellationToken ct) => Ok(await payroll.PeriodsAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await payroll.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> Open([FromBody] OpenPayrollRequest request, CancellationToken ct) => this.From(await payroll.OpenAsync(request, ct));

    [HttpGet("{id:long}/inputs")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Inputs(long id, CancellationToken ct) => this.From(await payroll.InputsAsync(id, ct));

    [HttpPost("{id:long}/inputs")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> AddInput(long id, [FromBody] AddPayrollInputRequest request, CancellationToken ct) =>
        this.From(await payroll.AddInputAsync(id, request, ct));

    [HttpDelete("{id:long}/inputs/{inputId:long}")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> DeleteInput(long id, long inputId, CancellationToken ct) => this.Deleted(await payroll.DeleteInputAsync(id, inputId, ct));

    /// <summary>Open → inputs → calculated → reviewed. The service re-checks the stage's permission.</summary>
    [HttpPost("{id:long}/advance")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> Advance(long id, [FromBody] AdvancePayrollRequest request, CancellationToken ct) =>
        this.From(await payroll.AdvanceAsync(id, request, ct));

    /// <summary>Reviewed → audited → approved, and disbursed → closed: the sign-off stages.</summary>
    [HttpPost("{id:long}/sign-off")]
    [HasPermission("HrPayroll.Approve")]
    public async Task<IActionResult> SignOff(long id, [FromBody] AdvancePayrollRequest request, CancellationToken ct) =>
        this.From(await payroll.AdvanceAsync(id, request, ct));

    [HttpPost("{id:long}/calculate")]
    [HasPermission("HrPayroll.Edit")]
    public async Task<IActionResult> Calculate(long id, CancellationToken ct) => this.From(await payroll.CalculateAsync(id, ct));

    [HttpPost("{id:long}/return")]
    [HasPermission("HrPayroll.Approve")]
    public async Task<IActionResult> Return(long id, [FromBody] ReturnPayrollRequest request, CancellationToken ct) =>
        this.From(await payroll.ReturnAsync(id, request, ct));

    [HttpGet("{id:long}/results")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Results(long id, CancellationToken ct) => this.From(await payroll.ResultsAsync(id, ct));

    [HttpGet("{id:long}/payslips/{employeeId:long}")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Payslip(long id, long employeeId, CancellationToken ct) => this.From(await payroll.PayslipAsync(id, employeeId, ct));

    [HttpGet("{id:long}/disbursement")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Rules(long id, CancellationToken ct) => this.From(await payroll.DisbursementRulesAsync(id, ct));

    [HttpPost("{id:long}/disbursement/simulate")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Simulate(long id, [FromBody] DisbursementRequest request, CancellationToken ct) =>
        this.From(await payroll.SimulateAsync(id, request, ct));

    [HttpPost("{id:long}/disbursement")]
    [HasPermission("HrPayroll.Approve")]
    public async Task<IActionResult> Disburse(long id, [FromBody] DisbursementRequest request, CancellationToken ct) =>
        this.From(await payroll.ApplyDisbursementAsync(id, request, ct));

    [HttpGet("entitlements/{employeeId:long}")]
    [HasPermission("HrPayroll.View")]
    public async Task<IActionResult> Entitlements(long employeeId, CancellationToken ct) => this.From(await payroll.EntitlementsAsync(employeeId, ct));

    [HttpGet("entitlements/mine")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> MyEntitlements(CancellationToken ct) => this.From(await payroll.EntitlementsAsync(null, ct));

    [HttpGet("payslips/mine")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> MyPayslips(CancellationToken ct) => Ok(await payroll.MyPayslipsAsync(ct));
}
