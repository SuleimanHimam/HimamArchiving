using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Documents;

// ---- Document types & categories (configuration) ----

public sealed record DocumentTypeDto(
    long Id,
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    string DefaultConfidentiality,
    int RetentionMonths,
    bool RequiresApproval,
    string AllowedUploadSources,   // flags, e.g. "Scanner" (scanner-only) or "All"
    bool IsActive);

public sealed record CreateDocumentTypeRequest(
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    ConfidentialityLevel DefaultConfidentiality,
    int RetentionMonths,
    bool RequiresApproval,
    UploadSource AllowedUploadSources = UploadSource.All);

public sealed record UpdateDocumentTypeRequest(
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    ConfidentialityLevel DefaultConfidentiality,
    int RetentionMonths,
    bool RequiresApproval,
    UploadSource AllowedUploadSources,
    bool IsActive);

public sealed record DocumentCategoryDto(long Id, long? ParentId, string Name, string? Code, bool IsActive);

public sealed record CreateDocumentCategoryRequest(long? ParentId, string Name, string? Code);

public sealed record UpdateDocumentCategoryRequest(long? ParentId, string Name, string? Code, bool IsActive);

// ---- Documents ----

public sealed record DocumentListItem(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    string Confidentiality,
    string Status,
    int Version,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate,
    DateTime CreatedAt,
    // Physical storage place (latest linked location)
    string? PhysicalLocationName = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    string? BoxCode = null)
{
    /// <summary>Admin-defined custom field values for this document, keyed by field id.</summary>
    public Dictionary<long, string> CustomValues { get; init; } = new();
}

public sealed record DocumentAttachmentDto(
    long Id,
    string FileName,
    string ContentType,
    string FileExtension,
    long SizeBytes,
    bool IsScanned,
    DateTime CreatedAt,
    string Kind = "Original",            // Original | PreservationMaster
    long? SourceAttachmentId = null,
    string? PdfAConformance = null,
    bool PreservationValidated = false);

public sealed record DocumentDetail(
    long Id,
    string DocumentNumber,
    string Title,
    string? Description,
    long DocumentTypeId,
    string DocumentTypeName,
    long? CategoryId,
    long OwningOrgUnitId,
    long? OwnerPositionId,
    string Confidentiality,
    string Status,
    string? Keywords,
    int RetentionMonths,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate,
    int Version,
    long? ParentDocumentId,
    bool IsLatestVersion,
    DateTime CreatedAt,
    IReadOnlyList<DocumentAttachmentDto> Attachments,
    // Physical archive location (where the paper original is stored)
    long? PhysicalLocationId = null,
    string? PhysicalLocationName = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    long? FolderId = null,
    bool IsFavorite = false,
    bool IsTombstone = false,
    DateTime? DestroyedAtUtc = null,
    long? BoxId = null,
    string? BoxCode = null);

public sealed record CreateDocumentRequest(
    string Title,
    string? Description,
    long DocumentTypeId,
    long? CategoryId,
    long OwningOrgUnitId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate,
    // Optional: link the physical storage location at entry time
    long? PhysicalLocationId = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    long? BoxId = null);   // exact box in the normalized location hierarchy

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description,
    long? CategoryId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate = null,    // admin override; when null, recomputed from retention
    long? BoxId = null);

public sealed record DocumentQuery(
    string? Search,
    DocumentStatus? Status,
    long? DocumentTypeId,
    long? OwningOrgUnitId,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    bool FavoritesOnly = false,
    bool SharedWithMe = false,
    long? FolderId = null,
    long? CustomFieldId = null,
    string? CustomFieldValue = null,
    int Page = 1,
    int PageSize = 20);
