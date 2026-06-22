using Archiving.Api.Authorization;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record CustomFieldDto(long Id, string EntityType, string FieldKey, string Label, int FieldType,
    string? Options, bool Searchable, int SortOrder, bool IsActive);
public sealed record CreateCustomFieldRequest(string EntityType, string Label, int FieldType, string? Options, bool Searchable);
public sealed record UpdateCustomFieldRequest(string Label, int FieldType, string? Options, bool Searchable, int SortOrder, bool IsActive);
public sealed record CustomValueDto(long FieldId, string Value);
public sealed record SaveCustomValuesRequest(Dictionary<long, string?> Values);

/// <summary>Admin-defined custom fields (per record type) and their per-record values.</summary>
[ApiController]
[Route("api/custom-fields")]
[Authorize]
public sealed class CustomFieldsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Entities = ["Document", "IncomingMail", "OutgoingMail", "ArchiveItem"];

    private static CustomFieldDto ToDto(CustomFieldDefinition d) => new(
        d.Id, d.EntityType, d.FieldKey, d.Label, (int)d.FieldType, d.Options, d.Searchable, d.SortOrder, d.IsActive);

    // ---- Definitions ----
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? entityType, CancellationToken ct)
    {
        var q = db.CustomFieldDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(d => d.EntityType == entityType);
        var rows = await q.OrderBy(d => d.SortOrder).ThenBy(d => d.Id).ToListAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    [HasPermission("CustomFields.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateCustomFieldRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Label)) return BadRequest(new { error = "اسم الحقل مطلوب" });
        if (!Entities.Contains(r.EntityType)) return BadRequest(new { error = "نوع السجل غير صالح" });
        var maxOrder = await db.CustomFieldDefinitions.Where(d => d.EntityType == r.EntityType)
            .Select(d => (int?)d.SortOrder).MaxAsync(ct) ?? 0;
        var e = new CustomFieldDefinition
        {
            EntityType = r.EntityType, Label = r.Label.Trim(), FieldType = (CustomFieldType)r.FieldType,
            Options = NormalizeOptions(r.Options), Searchable = r.Searchable, SortOrder = maxOrder + 1,
        };
        db.CustomFieldDefinitions.Add(e);
        await db.SaveChangesAsync(ct);
        e.FieldKey = $"f{e.Id}";   // stable internal key
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(e));
    }

    [HttpPut("{id:long}")]
    [HasPermission("CustomFields.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomFieldRequest r, CancellationToken ct)
    {
        var e = await db.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (e is null) return NotFound();
        if (string.IsNullOrWhiteSpace(r.Label)) return BadRequest(new { error = "اسم الحقل مطلوب" });
        e.Label = r.Label.Trim();
        e.FieldType = (CustomFieldType)r.FieldType;
        e.Options = NormalizeOptions(r.Options);
        e.Searchable = r.Searchable;
        e.SortOrder = r.SortOrder;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(e));
    }

    [HttpDelete("{id:long}")]
    [HasPermission("CustomFields.Edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var e = await db.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (e is null) return NotFound();
        var vals = db.CustomFieldValues.Where(v => v.FieldId == id);
        db.CustomFieldValues.RemoveRange(vals);
        db.CustomFieldDefinitions.Remove(e);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- Values ----
    [HttpGet("values/{entityType}/{entityId:long}")]
    public async Task<IActionResult> Values(string entityType, long entityId, CancellationToken ct)
    {
        var rows = await db.CustomFieldValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .Select(v => new CustomValueDto(v.FieldId, v.Value)).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPut("values/{entityType}/{entityId:long}")]
    public async Task<IActionResult> SaveValues(string entityType, long entityId, [FromBody] SaveCustomValuesRequest r, CancellationToken ct)
    {
        var existing = await db.CustomFieldValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId).ToListAsync(ct);
        foreach (var (fieldId, value) in r.Values)
        {
            var cur = existing.FirstOrDefault(v => v.FieldId == fieldId);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (cur is not null) db.CustomFieldValues.Remove(cur);
            }
            else if (cur is null)
            {
                db.CustomFieldValues.Add(new CustomFieldValue
                { FieldId = fieldId, EntityType = entityType, EntityId = entityId, Value = value.Trim() });
            }
            else { cur.Value = value.Trim(); }
        }
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? NormalizeOptions(string? options)
    {
        if (string.IsNullOrWhiteSpace(options)) return null;
        var items = options.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length == 0 ? null : string.Join('\n', items);
    }
}
