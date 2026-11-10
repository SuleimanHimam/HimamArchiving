using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Archive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>شجرة الأرشيف — browse the archive by where a document came from.</summary>
[ApiController]
[Route("api/archive/tree")]
[Authorize]
public sealed class ArchiveTreeController(IArchiveTreeService service) : ControllerBase
{
    [HttpGet]
    [HasPermission("Archive.View")]
    public async Task<IActionResult> Tree([FromQuery] ArchiveTreeMode mode = ArchiveTreeMode.Department,
        CancellationToken ct = default) =>
        Ok(await service.GetTreeAsync(mode, ct));

    /// <summary>Documents under a node, descendants included. Omit <c>node</c> for the whole archive.</summary>
    [HttpGet("documents")]
    [HasPermission("Archive.View")]
    public async Task<IActionResult> Documents(
        [FromQuery] string? node, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetNodeDocumentsAsync(node, search, page, pageSize, ct));
}
