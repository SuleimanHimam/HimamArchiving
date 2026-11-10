using Archiving.Application.Common.Models;
using Archiving.Application.Features.Printing;
using Archiving.Domain.Enums;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Manages print/report designs (letterheads) and resolves which one applies to a print job
/// using the User → OrgUnit → Default layering.</summary>
public interface IReportTemplateService
{
    Task<IReadOnlyList<ReportTemplateDto>> ListAsync(ReportTemplateScope? scope, ReportTemplateTarget? target,
        long? documentTypeId = null, long? mailCategoryId = null, CancellationToken ct = default);
    Task<Result<ReportTemplateDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<ReportTemplateDto>> CreateAsync(SaveReportTemplateRequest request, CancellationToken ct = default);
    Task<Result<ReportTemplateDto>> UpdateAsync(long id, SaveReportTemplateRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>Pick the design for a print job: the given user's design, else the given department's,
    /// else the institution default. Within each of those levels a design narrowed to
    /// <paramref name="documentTypeId"/> / <paramref name="mailCategoryId"/> beats the level's catch-all,
    /// so Finance can print salary letters differently from its contracts. When
    /// <paramref name="orgUnitId"/> is null it is taken from the user's own department.
    /// The logo falls back to the institution logo when unset.</summary>
    Task<ResolvedReportTemplateDto> ResolveAsync(ReportTemplateTarget target, long? orgUnitId, long? userId,
        long? documentTypeId = null, long? mailCategoryId = null, CancellationToken ct = default);
}
