using System.Text.RegularExpressions;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Numbering;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed partial class NumberingService(AppDbContext db, IAuditWriter audit) : INumberingService
{
    [GeneratedRegex(@"\{SEQ(?::(\d+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex SeqToken();

    public async Task<string?> GenerateNumberAsync(string entityType, long? institutionId = null,
        long? orgUnitId = null, long? documentTypeId = null,
        IReadOnlyDictionary<string, string>? extraTokens = null, CancellationToken ct = default)
    {
        var candidates = await db.NumberingTemplates
            .Where(t => t.IsActive && t.EntityType == entityType
                && (t.InstitutionId == null || t.InstitutionId == institutionId)
                && (t.OrgUnitId == null || t.OrgUnitId == orgUnitId)
                && (t.DocumentTypeId == null || t.DocumentTypeId == documentTypeId))
            .ToListAsync(ct);
        if (candidates.Count == 0) return null;

        // Most specific wins: OrgUnit (4) + Type (2) + Institution (1).
        var template = candidates
            .OrderByDescending(t => (t.OrgUnitId != null ? 4 : 0) + (t.DocumentTypeId != null ? 2 : 0) + (t.InstitutionId != null ? 1 : 0))
            .First();

        var now = DateTime.UtcNow;
        ApplyReset(template, now);
        template.CurrentSequence += 1;
        await db.SaveChangesAsync(ct);   // sequence consumed

        return await ExpandAsync(template, now, orgUnitId, documentTypeId, institutionId, extraTokens, ct);
    }

    private static void ApplyReset(NumberingTemplate t, DateTime now)
    {
        switch (t.ResetFrequency)
        {
            case ResetFrequency.Yearly when t.CurrentYear != now.Year:
                t.CurrentSequence = 0; t.CurrentYear = now.Year; t.CurrentMonth = now.Month; break;
            case ResetFrequency.Monthly when t.CurrentYear != now.Year || t.CurrentMonth != now.Month:
                t.CurrentSequence = 0; t.CurrentYear = now.Year; t.CurrentMonth = now.Month; break;
            case ResetFrequency.Never:
            default:
                t.CurrentYear ??= now.Year; t.CurrentMonth ??= now.Month; break;
        }
    }

    private async Task<string> ExpandAsync(NumberingTemplate t, DateTime now,
        long? orgUnitId, long? documentTypeId, long? institutionId,
        IReadOnlyDictionary<string, string>? extra, CancellationToken ct)
    {
        var dept = extra?.GetValueOrDefault("DEPT")
            ?? (orgUnitId is { } ou ? await db.OrgUnits.Where(u => u.Id == ou).Select(u => u.Code ?? u.Name).FirstOrDefaultAsync(ct) : null);
        var type = extra?.GetValueOrDefault("TYPE")
            ?? (documentTypeId is { } dt ? await db.DocumentTypes.Where(x => x.Id == dt).Select(x => x.Code ?? x.Name).FirstOrDefaultAsync(ct) : null);
        var inst = extra?.GetValueOrDefault("INST")
            ?? (institutionId is { } ins ? await db.Institutions.Where(i => i.Id == ins).Select(i => i.Code ?? i.Name).FirstOrDefaultAsync(ct) : null);

        var result = t.Pattern
            .Replace("{YYYY}", now.Year.ToString("D4"))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DEPT}", dept ?? "")
            .Replace("{TYPE}", type ?? "")
            .Replace("{INST}", inst ?? "");

        result = SeqToken().Replace(result, m =>
        {
            var width = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            return width > 0 ? t.CurrentSequence.ToString().PadLeft(width, '0') : t.CurrentSequence.ToString();
        });
        return result;
    }

    public string Preview(string pattern)
    {
        var now = DateTime.UtcNow;
        var result = pattern
            .Replace("{YYYY}", now.Year.ToString("D4"))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DEPT}", "DEPT").Replace("{TYPE}", "TYPE").Replace("{INST}", "INST");
        return SeqToken().Replace(result, m =>
        {
            var width = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            return width > 0 ? "1".PadLeft(width, '0') : "1";
        });
    }

    public async Task<IReadOnlyList<NumberingTemplateDto>> ListAsync(CancellationToken ct = default) =>
        await db.NumberingTemplates.OrderBy(t => t.EntityType).Select(t => Map(t)).ToListAsync(ct);

    public async Task<Result<NumberingTemplateDto>> CreateAsync(CreateNumberingTemplateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.EntityType) || string.IsNullOrWhiteSpace(dto.Pattern))
            return Result<NumberingTemplateDto>.Fail("نوع الكيان والنمط مطلوبان");

        var t = new NumberingTemplate
        {
            EntityType = dto.EntityType, InstitutionId = dto.InstitutionId, OrgUnitId = dto.OrgUnitId,
            DocumentTypeId = dto.DocumentTypeId, Pattern = dto.Pattern, ResetFrequency = dto.ResetFrequency,
            IsActive = true,
        };
        db.NumberingTemplates.Add(t);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("NumberingTemplateCreated", "NumberingTemplate", t.Id, ct: ct);
        return Result<NumberingTemplateDto>.Ok(Map(t));
    }

    public async Task<Result<NumberingTemplateDto>> UpdateAsync(long id, CreateNumberingTemplateDto dto, CancellationToken ct = default)
    {
        var t = await db.NumberingTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<NumberingTemplateDto>.Fail("القالب غير موجود");
        t.EntityType = dto.EntityType; t.InstitutionId = dto.InstitutionId; t.OrgUnitId = dto.OrgUnitId;
        t.DocumentTypeId = dto.DocumentTypeId; t.Pattern = dto.Pattern; t.ResetFrequency = dto.ResetFrequency;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("NumberingTemplateUpdated", "NumberingTemplate", t.Id, ct: ct);
        return Result<NumberingTemplateDto>.Ok(Map(t));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var t = await db.NumberingTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<bool>.Fail("القالب غير موجود");
        db.NumberingTemplates.Remove(t);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("NumberingTemplateDeleted", "NumberingTemplate", id, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ResetAsync(long id, CancellationToken ct = default)
    {
        var t = await db.NumberingTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<bool>.Fail("القالب غير موجود");
        t.CurrentSequence = 0;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("NumberingTemplateReset", "NumberingTemplate", id, ct: ct);
        return Result<bool>.Ok(true);
    }

    private static NumberingTemplateDto Map(NumberingTemplate t) => new(
        t.Id, t.EntityType, t.InstitutionId, t.OrgUnitId, t.DocumentTypeId, t.Pattern,
        t.ResetFrequency.ToString(), t.CurrentSequence, t.CurrentYear, t.CurrentMonth, t.IsActive);
}
