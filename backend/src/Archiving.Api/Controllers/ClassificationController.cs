using Archiving.Api.Authorization;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record ClassificationTypeDto(
    long Id, string NameAr, string? NameEn, string? Description,
    string Color, int SortOrder, bool IsSystem, bool IsActive,
    IReadOnlyList<long> RoleIds);

public sealed record UpsertClassificationRequest(
    string NameAr, string? NameEn, string? Description,
    string Color, int SortOrder, bool IsActive);

public sealed record SetClassificationRolesRequest(IReadOnlyList<long> RoleIds);

[ApiController]
[Route("api/classification-types")]
[Authorize]
public sealed class ClassificationController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [HasPermission("Classification.View")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await db.ClassificationTypes
            .Include(c => c.RoleClassifications)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return Ok(list.Select(c => new ClassificationTypeDto(
            c.Id, c.NameAr, c.NameEn, c.Description,
            c.Color, c.SortOrder, c.IsSystem, c.IsActive,
            c.RoleClassifications.Select(rc => rc.RoleId).ToList())));
    }

    [HttpPost]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> Create([FromBody] UpsertClassificationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return BadRequest(new { error = "اسم التصنيف مطلوب" });

        var entity = new ClassificationType
        {
            NameAr      = req.NameAr.Trim(),
            NameEn      = req.NameEn?.Trim(),
            Description = req.Description?.Trim(),
            Color       = req.Color?.Trim() ?? "#6b7280",
            SortOrder   = req.SortOrder,
            IsSystem    = false,
            IsActive    = req.IsActive,
        };
        db.ClassificationTypes.Add(entity);
        await db.SaveChangesAsync(ct);

        return Ok(new ClassificationTypeDto(
            entity.Id, entity.NameAr, entity.NameEn, entity.Description,
            entity.Color, entity.SortOrder, entity.IsSystem, entity.IsActive, []));
    }

    [HttpPut("{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpsertClassificationRequest req, CancellationToken ct)
    {
        var entity = await db.ClassificationTypes
            .Include(c => c.RoleClassifications)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound(new { error = "التصنيف غير موجود" });
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return BadRequest(new { error = "اسم التصنيف مطلوب" });

        entity.NameAr      = req.NameAr.Trim();
        entity.NameEn      = req.NameEn?.Trim();
        entity.Description = req.Description?.Trim();
        entity.Color       = req.Color?.Trim() ?? entity.Color;
        entity.SortOrder   = req.SortOrder;
        entity.IsActive    = req.IsActive;

        await db.SaveChangesAsync(ct);

        return Ok(new ClassificationTypeDto(
            entity.Id, entity.NameAr, entity.NameEn, entity.Description,
            entity.Color, entity.SortOrder, entity.IsSystem, entity.IsActive,
            entity.RoleClassifications?.Select(rc => rc.RoleId).ToList() ?? []));
    }

    [HttpPut("{id:long}/roles")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> SetRoles(long id, [FromBody] SetClassificationRolesRequest req, CancellationToken ct)
    {
        var entity = await db.ClassificationTypes
            .Include(c => c.RoleClassifications)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound(new { error = "التصنيف غير موجود" });

        var requestedIds = req.RoleIds.ToHashSet();
        var validRoleIds = (await db.Roles.Select(r => r.Id).ToListAsync(ct)).ToHashSet();
        var invalid = requestedIds.Except(validRoleIds).ToList();
        if (invalid.Count > 0)
            return BadRequest(new { error = $"أدوار غير موجودة: {string.Join(", ", invalid)}" });

        db.RoleClassifications.RemoveRange(entity.RoleClassifications);
        foreach (var roleId in requestedIds)
            db.RoleClassifications.Add(new RoleClassification { RoleId = roleId, ClassificationTypeId = id });

        await db.SaveChangesAsync(ct);
        return Ok(new { classificationTypeId = id, roleIds = requestedIds.ToList() });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var entity = await db.ClassificationTypes
            .Include(c => c.RoleClassifications)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound(new { error = "التصنيف غير موجود" });
        if (entity.IsSystem) return BadRequest(new { error = "لا يمكن حذف التصنيفات المدمجة في النظام" });

        db.RoleClassifications.RemoveRange(entity.RoleClassifications);
        db.ClassificationTypes.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
