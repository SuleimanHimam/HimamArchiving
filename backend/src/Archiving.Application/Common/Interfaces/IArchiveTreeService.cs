using Archiving.Application.Common.Models;
using Archiving.Application.Features.Archive;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Browse the archive as a tree of origins — which department a document is owned by, or
/// which external party it arrived from — and list the documents under any node of that tree.</summary>
public interface IArchiveTreeService
{
    Task<IReadOnlyList<ArchiveTreeNodeDto>> GetTreeAsync(ArchiveTreeMode mode, CancellationToken ct = default);

    /// <summary>Documents under <paramref name="nodeId"/>, descendants included. A null/blank node
    /// means the whole archive.</summary>
    Task<PagedResult<ArchiveTreeDocumentDto>> GetNodeDocumentsAsync(
        string? nodeId, string? search, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
