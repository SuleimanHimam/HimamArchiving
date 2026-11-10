using Archiving.Application.Common.Models;
using Archiving.Application.Features.Timeline;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Merges everything that happened to a document into one chronological timeline.</summary>
public interface IDocumentTimelineService
{
    Task<Result<IReadOnlyList<TimelineEntryDto>>> GetTimelineAsync(long documentId, TimelineFilter? filter, CancellationToken ct = default);
}
