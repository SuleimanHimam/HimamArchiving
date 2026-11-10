using Archiving.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(IReportService service) : ControllerBase
{
    // Dashboard summary — available to any authenticated user (counts are clearance-gated in the service).
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(await service.GetDashboardAsync(ct));

    // ---- Phase 10 structured reports ----

    [HttpGet("workflow-performance")]
    public async Task<IActionResult> WorkflowPerformance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] long? definitionId, [FromQuery] long? orgUnitId, CancellationToken ct)
        => Ok(await service.GetWorkflowPerformanceAsync(from, to, definitionId, orgUnitId, ct));

    [HttpGet("department-activity")]
    public async Task<IActionResult> DepartmentActivity(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] long? orgUnitId, CancellationToken ct)
        => Ok(await service.GetDepartmentActivityAsync(from, to, orgUnitId, ct));

    [HttpGet("disposition-status")]
    public async Task<IActionResult> DispositionStatus(CancellationToken ct)
        => Ok(await service.GetDispositionStatusAsync(ct));

    [HttpGet("storage-utilization")]
    public async Task<IActionResult> StorageUtilization(CancellationToken ct)
        => Ok(await service.GetStorageUtilizationAsync(ct));

    // Per-bureau Diwan volumes + real monthly series. Clearance-gated in the service.
    [HttpGet("diwan-dashboard")]
    public async Task<IActionResult> DiwanDashboard(
        [FromQuery] int? year, [FromQuery] long? orgUnitId, CancellationToken ct)
        => Ok(await service.GetDiwanDashboardAsync(year, orgUnitId, ct));

    [HttpGet("trends")]
    public async Task<IActionResult> Trends(
        [FromQuery] string metric, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? granularity, CancellationToken ct)
        => Ok(await service.GetTrendsAsync(metric ?? "", from, to, granularity, ct));

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> DashboardSummary(CancellationToken ct) => Ok(await service.GetDashboardAsync(ct));
}
