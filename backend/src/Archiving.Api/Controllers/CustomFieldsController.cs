using Archiving.Api.Authorization;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record CustomFieldDto(long Id, string EntityType, long? DocumentTypeId, string? DocumentTypeName,
    string FieldKey, string Label, int FieldType, string? Options, bool Searchable, int SortOrder, bool IsActive);
public sealed record CreateCustomFieldRequest(string EntityType, string Label, int FieldType, string? Options, bool Searchable,
    long? DocumentTypeId = null);
public sealed record UpdateCustomFieldRequest(string Label, int FieldType, string? Options, bool Searchable, int SortOrder, bool IsActive,
    long? DocumentTypeId = null);
public sealed record CustomValueDto(long FieldId, string Value);
public sealed record SaveCustomValuesRequest(Dictionary<long, string?> Values);

/// <summary>Admin-defined custom fields (per record type) and their per-record values.</summary>
[ApiController]
[Route("api/custom-fields")]
[Authorize]
public sealed class CustomFieldsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Entities = ["Document", "IncomingMail", "OutgoingMail", "ArchiveItem"];

    private static CustomFieldDto ToDto(CustomFieldDefinition d, string? typeName = null) => new(
        d.Id, d.EntityType, d.DocumentTypeId, typeName,
        d.FieldKey, d.Label, (int)d.FieldType, d.Options, d.Searchable, d.SortOrder, d.IsActive);

    // ---- Definitions ----

    /// <summary>Lists field definitions. With <paramref name="documentTypeId"/> the result is what a record
    /// of that type actually carries — the type's own fields plus the shared ones. With
    /// <paramref name="sharedOnly"/> it is just the shared fields. With neither, every field of the record type.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? entityType,
        [FromQuery] long? documentTypeId,
        [FromQuery] bool sharedOnly,
        CancellationToken ct)
    {
        var q = db.CustomFieldDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(d => d.EntityType == entityType);
        if (documentTypeId is { } dt) q = q.Where(d => d.DocumentTypeId == null || d.DocumentTypeId == dt);
        else if (sharedOnly) q = q.Where(d => d.DocumentTypeId == null);

        // Shared fields first, then the type-specific ones, so a record's form reads general → specific.
        var rows = await q.OrderBy(d => d.DocumentTypeId == null ? 0 : 1)
            .ThenBy(d => d.SortOrder).ThenBy(d => d.Id).ToListAsync(ct);
        var names = await TypeNamesAsync(rows, ct);
        return Ok(rows.Select(d => ToDto(d, Name(names, d.DocumentTypeId))));
    }

    [HttpPost]
    [HasPermission("CustomFields.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateCustomFieldRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Label)) return BadRequest(new { error = "اسم الحقل مطلوب" });
        if (!Entities.Contains(r.EntityType)) return BadRequest(new { error = "نوع السجل غير صالح" });
        var docTypeId = await ResolveDocumentTypeAsync(r.EntityType, r.DocumentTypeId, ct);
        if (docTypeId.Error is { } err) return BadRequest(new { error = err });

        // Order is kept per (record type, document type) so each form numbers its own fields from 1.
        var maxOrder = await db.CustomFieldDefinitions
            .Where(d => d.EntityType == r.EntityType && d.DocumentTypeId == docTypeId.Value)
            .Select(d => (int?)d.SortOrder).MaxAsync(ct) ?? 0;
        var e = new CustomFieldDefinition
        {
            EntityType = r.EntityType, DocumentTypeId = docTypeId.Value,
            Label = r.Label.Trim(), FieldType = (CustomFieldType)r.FieldType,
            Options = NormalizeOptions(r.Options), Searchable = r.Searchable, SortOrder = maxOrder + 1,
        };
        db.CustomFieldDefinitions.Add(e);
        await db.SaveChangesAsync(ct);
        e.FieldKey = $"f{e.Id}";   // stable internal key
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(e, await TypeNameAsync(e.DocumentTypeId, ct)));
    }

    [HttpPut("{id:long}")]
    [HasPermission("CustomFields.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomFieldRequest r, CancellationToken ct)
    {
        var e = await db.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (e is null) return NotFound();
        if (string.IsNullOrWhiteSpace(r.Label)) return BadRequest(new { error = "اسم الحقل مطلوب" });
        var docTypeId = await ResolveDocumentTypeAsync(e.EntityType, r.DocumentTypeId, ct);
        if (docTypeId.Error is { } err) return BadRequest(new { error = err });

        e.DocumentTypeId = docTypeId.Value;
        e.Label = r.Label.Trim();
        e.FieldType = (CustomFieldType)r.FieldType;
        e.Options = NormalizeOptions(r.Options);
        e.Searchable = r.Searchable;
        e.SortOrder = r.SortOrder;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(e, await TypeNameAsync(e.DocumentTypeId, ct)));
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

    /// <summary>Validates the requested narrowing. Only Document fields may be tied to a document type;
    /// for any other record type the request's value is ignored rather than rejected.</summary>
    private async Task<(long? Value, string? Error)> ResolveDocumentTypeAsync(string entityType, long? documentTypeId, CancellationToken ct)
    {
        if (entityType != "Document" || documentTypeId is null) return (null, null);
        var exists = await db.DocumentTypes.AnyAsync(t => t.Id == documentTypeId, ct);
        return exists ? (documentTypeId, null) : (null, "نوع الوثيقة غير موجود");
    }

    private async Task<string?> TypeNameAsync(long? documentTypeId, CancellationToken ct) =>
        documentTypeId is null ? null
            : await db.DocumentTypes.Where(t => t.Id == documentTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct);

    private async Task<Dictionary<long, string>> TypeNamesAsync(IReadOnlyList<CustomFieldDefinition> rows, CancellationToken ct)
    {
        var ids = rows.Where(d => d.DocumentTypeId is not null).Select(d => d.DocumentTypeId!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.DocumentTypes.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
    }

    private static string? Name(Dictionary<long, string> names, long? id) =>
        id is { } v && names.TryGetValue(v, out var n) ? n : null;

    private static string? NormalizeOptions(string? options)
    {
        if (string.IsNullOrWhiteSpace(options)) return null;
        var items = options.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length == 0 ? null : string.Join('\n', items);
    }
}
