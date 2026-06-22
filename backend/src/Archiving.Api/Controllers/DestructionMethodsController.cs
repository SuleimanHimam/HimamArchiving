using Archiving.Api.Authorization;
using Archiving.Application.Features.Destruction;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

/// <summary>Admin-managed catalog of destruction-method labels (Settings → طرق الإتلاف).</summary>
[ApiController]
[Route("api/destruction/methods")]
[Authorize]
public sealed class DestructionMethodsController(AppDbContext db) : ControllerBase
{
    private static DestructionMethodOptionDto ToDto(DestructionMethodOption m) => new(m.Id, m.Label, m.SortOrder, m.IsActive);

    [HttpGet]
    [HasPermission("Destruction.View")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await db.DestructionMethodOptions.OrderBy(m => m.SortOrder).ThenBy(m => m.Id).ToListAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    [HasPermission("Destruction.Approve")]
    public async Task<IActionResult> Create([FromBody] MethodOptionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) return BadRequest(new { error = "اسم الطريقة مطلوب" });
        var maxOrder = await db.DestructionMethodOptions.Select(m => (int?)m.SortOrder).MaxAsync(ct) ?? 0;
        var m = new DestructionMethodOption { Label = req.Label.Trim(), IsActive = req.IsActive, SortOrder = maxOrder + 1 };
        db.DestructionMethodOptions.Add(m);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(m));
    }

    [HttpPut("{id:long}")]
    [HasPermission("Destruction.Approve")]
    public async Task<IActionResult> Update(long id, [FromBody] MethodOptionRequest req, CancellationToken ct)
    {
        var m = await db.DestructionMethodOptions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Label)) return BadRequest(new { error = "اسم الطريقة مطلوب" });
        m.Label = req.Label.Trim();
        m.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(m));
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Destruction.Approve")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var m = await db.DestructionMethodOptions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return NotFound();
        db.DestructionMethodOptions.Remove(m);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
