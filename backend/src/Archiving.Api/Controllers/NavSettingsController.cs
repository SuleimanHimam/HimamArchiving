using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record NavSettingsDto(string[] Hidden);
public sealed record UpdateNavSettingsRequest(string[] Hidden);

/// <summary>Navbar visibility. The PUT is org-wide — sections hidden for everyone — while the GET
/// answers for whoever is asking, since a department head may also hide sections for one person
/// (<c>PUT /api/users/{id}/nav-sections</c>). The sidebar asks once and gets one list.</summary>
[ApiController]
[Route("api/nav-settings")]
[Authorize]
public sealed class NavSettingsController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    private static string[] Parse(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var inst = await db.Institutions.FirstOrDefaultAsync(ct);
        var mine = currentUser.UserId is { } me
            ? await db.Users.Where(u => u.Id == me).Select(u => u.HiddenNavKeys).FirstOrDefaultAsync(ct)
            : null;

        // Union: a section hidden institution-wide stays hidden whatever the personal list says,
        // and the personal list only ever hides more.
        var hidden = Parse(inst?.HiddenNavKeys).Concat(Parse(mine))
            .Distinct(StringComparer.Ordinal).ToArray();
        return Ok(new NavSettingsDto(hidden));
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
