using Archiving.Api.Authorization;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record NavSettingsDto(string[] Hidden);
public sealed record UpdateNavSettingsRequest(string[] Hidden);

/// <summary>Org-wide navbar visibility: which navigation sections are hidden for all users.</summary>
[ApiController]
[Route("api/nav-settings")]
[Authorize]
public sealed class NavSettingsController(AppDbContext db) : ControllerBase
{
    private static string[] Parse(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var inst = await db.Institutions.FirstOrDefaultAsync(ct);
        return Ok(new NavSettingsDto(Parse(inst?.HiddenNavKeys)));
    }

    [HttpPut]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> Update([FromBody] UpdateNavSettingsRequest req, CancellationToken ct)
    {
        var inst = await db.Institutions.FirstOrDefaultAsync(ct);
        if (inst is null) return NotFound(new { error = "لا توجد مؤسسة مسجّلة في النظام" });

        // 'settings' must never be hideable, or an admin could lock themselves out.
        var hidden = (req.Hidden ?? [])
            .Select(k => k.Trim())
            .Where(k => k.Length > 0 && k != "settings")
            .Distinct()
            .ToArray();

        inst.HiddenNavKeys = hidden.Length == 0 ? null : string.Join(',', hidden);
        await db.SaveChangesAsync(ct);
        return Ok(new NavSettingsDto(hidden));
    }
}
