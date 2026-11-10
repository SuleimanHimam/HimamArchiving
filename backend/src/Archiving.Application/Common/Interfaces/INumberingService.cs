using Archiving.Application.Common.Models;
using Archiving.Application.Features.Numbering;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Generates auto-numbers from the most specific matching <c>NumberingTemplate</c>, and manages templates.</summary>
public interface INumberingService
{
    /// <summary>Generates the next number for an entity, or <c>null</c> if no active template matches
    /// (the caller then falls back to its own legacy numbering).</summary>
    Task<string?> GenerateNumberAsync(string entityType, long? institutionId = null,
        long? orgUnitId = null, long? documentTypeId = null,
        IReadOnlyDictionary<string, string>? extraTokens = null, CancellationToken ct = default);

    Task<IReadOnlyList<NumberingTemplateDto>> ListAsync(CancellationToken ct = default);
    Task<Result<NumberingTemplateDto>> CreateAsync(CreateNumberingTemplateDto dto, CancellationToken ct = default);
    Task<Result<NumberingTemplateDto>> UpdateAsync(long id, CreateNumberingTemplateDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> ResetAsync(long id, CancellationToken ct = default);
    /// <summary>Previews what a raw pattern would render now (no sequence consumed).</summary>
    string Preview(string pattern);
}
