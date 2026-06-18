using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Serves client-side helper downloads (the local scan agent installer).
/// Anonymous so the link works from the login page.</summary>
[ApiController]
[Route("api/downloads")]
public sealed class DownloadsController(IConfiguration config) : ControllerBase
{
    [HttpGet("scan-agent")]
    [AllowAnonymous]
    public IActionResult ScanAgent()
    {
        var path = config["Downloads:ScanAgentPath"];
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return NotFound(new { error = "ملف برنامج المسح غير متوفر على الخادم بعد" });

        return PhysicalFile(path, "application/octet-stream", "archiving-scan-agent.exe");
    }

    /// <summary>Lets the UI show/hide the download link without attempting a 70 MB request.</summary>
    [HttpGet("scan-agent/available")]
    [AllowAnonymous]
    public IActionResult ScanAgentAvailable()
    {
        var path = config["Downloads:ScanAgentPath"];
        var available = !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);
        return Ok(new { available });
    }
}
