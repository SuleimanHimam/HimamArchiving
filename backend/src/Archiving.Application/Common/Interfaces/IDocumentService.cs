using Archiving.Application.Common.Models;
using Archiving.Application.Features.Documents;

namespace Archiving.Application.Common.Interfaces;

/// <summary>An attachment stream plus the metadata needed to return it to the client.</summary>
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public interface IDocumentService
{
    // Configuration
    Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(CancellationToken ct = default);
    Task<Result<DocumentTypeDto>> CreateTypeAsync(CreateDocumentTypeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<Result<DocumentCategoryDto>> CreateCategoryAsync(CreateDocumentCategoryRequest request, CancellationToken ct = default);

    // Documents
    Task<PagedResult<DocumentListItem>> ListAsync(DocumentQuery query, CancellationToken ct = default);
    Task<Result<DocumentDetail>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<DocumentDetail>> CreateAsync(CreateDocumentRequest request, CancellationToken ct = default);
    Task<Result<DocumentDetail>> UpdateAsync(long id, UpdateDocumentRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);

    // Attachments. <paramref name="isScanned"/> marks the upload as coming from a scanner
    // (via the user's local scan agent) and is validated against the type's allowed upload sources.
    Task<Result<DocumentAttachmentDto>> AddAttachmentAsync(
        long documentId, string fileName, string contentType, Stream content,
        bool isScanned = false, CancellationToken ct = default);
    Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default);
    Task<Result<bool>> RemoveAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default);
}
