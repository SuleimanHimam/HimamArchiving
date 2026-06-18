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
}
