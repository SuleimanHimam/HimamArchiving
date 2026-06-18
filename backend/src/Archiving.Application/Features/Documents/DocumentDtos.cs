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

public sealed record DocumentCategoryDto(long Id, long? ParentId, string Name, string? Code, bool IsActive);

public sealed record CreateDocumentCategoryRequest(long? ParentId, string Name, string? Code);

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
    DateTime CreatedAt);

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
    IReadOnlyList<DocumentAttachmentDto> Attachments);

public sealed record CreateDocumentRequest(
    string Title,
    string? Description,
    long DocumentTypeId,
    long? CategoryId,
    long OwningOrgUnitId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate);

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description,
    long? CategoryId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate);

public sealed record DocumentQuery(
    string? Search,
    DocumentStatus? Status,
    long? DocumentTypeId,
    long? OwningOrgUnitId,
    int Page = 1,
    int PageSize = 20);
