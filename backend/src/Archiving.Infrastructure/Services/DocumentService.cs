using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Documents;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class DocumentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IAuditWriter audit) : IDocumentService
{
    private const string EntityType = "Document";

    // Formats accepted by the system (spec §3): PDF / DOCX / XLSX / JPG / PNG / ZIP.
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".jpg", ".jpeg", ".png", ".zip" };

    // ---- Document types ----

    public async Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(CancellationToken ct = default) =>
        await db.DocumentTypes.OrderBy(t => t.Name)
            .Select(t => new DocumentTypeDto(t.Id, t.Name, t.NameEn, t.Code, t.CategoryId,
                t.DefaultConfidentiality.ToString(), t.RetentionMonths, t.RequiresApproval,
                t.AllowedUploadSources.ToString(), t.IsActive))
            .ToListAsync(ct);

    public async Task<Result<DocumentTypeDto>> CreateTypeAsync(CreateDocumentTypeRequest r, CancellationToken ct = default)
    {
        var e = new DocumentType
        {
            Name = r.Name, NameEn = r.NameEn, Code = r.Code, CategoryId = r.CategoryId,
            DefaultConfidentiality = r.DefaultConfidentiality,
            RetentionMonths = r.RetentionMonths <= 0 ? 120 : r.RetentionMonths,
            RequiresApproval = r.RequiresApproval,
            AllowedUploadSources = r.AllowedUploadSources == UploadSource.None ? UploadSource.All : r.AllowedUploadSources,
        };
        db.DocumentTypes.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<DocumentTypeDto>.Ok(new DocumentTypeDto(e.Id, e.Name, e.NameEn, e.Code, e.CategoryId,
            e.DefaultConfidentiality.ToString(), e.RetentionMonths, e.RequiresApproval,
            e.AllowedUploadSources.ToString(), e.IsActive));
    }

    // ---- Categories ----

    public async Task<IReadOnlyList<DocumentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await db.DocumentCategories.OrderBy(c => c.Name)
            .Select(c => new DocumentCategoryDto(c.Id, c.ParentId, c.Name, c.Code, c.IsActive))
            .ToListAsync(ct);

    public async Task<Result<DocumentCategoryDto>> CreateCategoryAsync(CreateDocumentCategoryRequest r, CancellationToken ct = default)
    {
        if (r.ParentId is { } pid && !await db.DocumentCategories.AnyAsync(c => c.Id == pid, ct))
            return Result<DocumentCategoryDto>.Fail("التصنيف الأب غير موجود");

        var e = new DocumentCategory { ParentId = r.ParentId, Name = r.Name, Code = r.Code };
        db.DocumentCategories.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<DocumentCategoryDto>.Ok(new DocumentCategoryDto(e.Id, e.ParentId, e.Name, e.Code, e.IsActive));
    }

    // ---- Documents ----

    public async Task<PagedResult<DocumentListItem>> ListAsync(DocumentQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Documents
            .Where(d => d.IsLatestVersion)
            .Where(d => (int)d.Confidentiality <= (int)currentUser.Clearance); // clearance gate

        if (query.Status is { } status) q = q.Where(d => d.Status == status);
        if (query.DocumentTypeId is { } typeId) q = q.Where(d => d.DocumentTypeId == typeId);
        if (query.OwningOrgUnitId is { } unitId) q = q.Where(d => d.OwningOrgUnitId == unitId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(d => d.DocumentNumber.Contains(s) || d.Title.Contains(s)
                || (d.Keywords != null && d.Keywords.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(d => new DocumentListItem(
                d.Id, d.DocumentNumber, d.Title, d.DocumentType.Name,
                d.Confidentiality.ToString(), d.Status.ToString(), d.Version,
                d.DocumentDate, d.ExpiryDate, d.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<DocumentListItem> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<DocumentDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentDetail>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<DocumentDetail>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");
        return Result<DocumentDetail>.Ok(ToDetail(doc));
    }

    public async Task<Result<DocumentDetail>> CreateAsync(CreateDocumentRequest r, CancellationToken ct = default)
    {
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == r.DocumentTypeId, ct);
        if (type is null) return Result<DocumentDetail>.Fail("نوع الوثيقة غير موجود");
        if (!await db.OrgUnits.AnyAsync(u => u.Id == r.OwningOrgUnitId, ct))
            return Result<DocumentDetail>.Fail("الوحدة المالكة غير موجودة");

        var doc = new Document
        {
            DocumentNumber = await NextDocumentNumberAsync(ct),
            Title = r.Title,
            Description = r.Description,
            DocumentTypeId = r.DocumentTypeId,
            CategoryId = r.CategoryId,
            OwningOrgUnitId = r.OwningOrgUnitId,
            OwnerPositionId = r.OwnerPositionId,
            Confidentiality = r.Confidentiality,
            Status = DocumentStatus.Active,
            Keywords = r.Keywords,
            RetentionMonths = type.RetentionMonths,
            DocumentDate = r.DocumentDate,
            ExpiryDate = ComputeExpiry(r.DocumentDate, type.RetentionMonths),
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", EntityType, doc.Id, doc.Title, ct: ct);

        await db.Entry(doc).Reference(d => d.DocumentType).LoadAsync(ct);
        return Result<DocumentDetail>.Ok(ToDetail(doc));
    }

    public async Task<Result<DocumentDetail>> UpdateAsync(long id, UpdateDocumentRequest r, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentDetail>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<DocumentDetail>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        doc.Title = r.Title;
        doc.Description = r.Description;
        doc.CategoryId = r.CategoryId;
        doc.OwnerPositionId = r.OwnerPositionId;
        doc.Confidentiality = r.Confidentiality;
        doc.Keywords = r.Keywords;
        doc.DocumentDate = r.DocumentDate;
        doc.ExpiryDate = ComputeExpiry(r.DocumentDate, doc.RetentionMonths);

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<DocumentDetail>.Ok(ToDetail(doc));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        // The AuditableEntityInterceptor converts this into a soft delete (stamps IsDeleted/DeletedAt/By).
        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- Attachments ----

    public async Task<Result<DocumentAttachmentDto>> AddAttachmentAsync(
        long documentId, string fileName, string contentType, Stream content,
        bool isScanned = false, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<DocumentAttachmentDto>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<DocumentAttachmentDto>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var ext = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(ext))
            return Result<DocumentAttachmentDto>.Fail($"نوع الملف غير مسموح ({ext}). المسموح: PDF, DOCX, XLSX, JPG, PNG, ZIP");

        // Enforce the document type's permitted upload sources (e.g. scanner-only types).
        var sourceCheck = CheckUploadSource(doc.DocumentType.AllowedUploadSources, ext, isScanned);
        if (sourceCheck is not null) return Result<DocumentAttachmentDto>.Fail(sourceCheck);

        var stored = await storage.SaveAsync($"documents/{doc.Id}", fileName, content, ct);

        var att = new DocumentAttachment
        {
            DocumentId = doc.Id,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileExtension = ext.TrimStart('.').ToLowerInvariant(),
            SizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            Checksum = stored.Checksum,
            IsScanned = isScanned,
        };
        db.DocumentAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(isScanned ? "ScannedAttachmentAdded" : "AttachmentAdded",
            EntityType, doc.Id, doc.Title, newValues: fileName, ct: ct);

        return Result<DocumentAttachmentDto>.Ok(new DocumentAttachmentDto(
            att.Id, att.FileName, att.ContentType, att.FileExtension, att.SizeBytes, att.IsScanned, att.CreatedAt,
            att.Kind.ToString(), att.SourceAttachmentId, att.PdfAConformance, att.PreservationValidated));
    }

    /// <summary>Validates an upload against the type's allowed sources. Returns null when allowed,
    /// otherwise an Arabic error message. Scanned uploads need the Scanner flag; file uploads are
    /// classified by extension (PDF / image / any-file).</summary>
    private static string? CheckUploadSource(UploadSource allowed, string ext, bool isScanned)
    {
        if (allowed == UploadSource.None || allowed.HasFlag(UploadSource.AnyFile)) return null;

        if (isScanned)
            return allowed.HasFlag(UploadSource.Scanner) ? null : "هذا النوع من الوثائق لا يسمح بالمسح الضوئي";

        var e = ext.ToLowerInvariant();
        var needed = e == ".pdf" ? UploadSource.Pdf
            : e is ".jpg" or ".jpeg" or ".png" ? UploadSource.Image
            : UploadSource.AnyFile;

        if (allowed.HasFlag(needed)) return null;

        // Most restrictive case worth a clear message: scanner-only types.
        return allowed == UploadSource.Scanner
            ? "هذا النوع من الوثائق يقبل المسح الضوئي فقط — استخدم زر \"مسح ضوئي\""
            : "نوع الملف غير مسموح لهذا النوع من الوثائق";
    }

    public async Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<AttachmentDownload>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<AttachmentDownload>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId, ct);
        if (att is null) return Result<AttachmentDownload>.Fail("المرفق غير موجود");

        var stream = await storage.OpenAsync(att.StorageKey, ct);
        if (stream is null) return Result<AttachmentDownload>.Fail("ملف المرفق غير موجود في وحدة التخزين");

        await audit.WriteAsync("AttachmentDownloaded", EntityType, doc.Id, doc.Title, newValues: att.FileName, ct: ct);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(stream, att.FileName, att.ContentType));
    }

    public async Task<Result<bool>> RemoveAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId, ct);
        if (att is null) return Result<bool>.Fail("المرفق غير موجود");

        await storage.DeleteAsync(att.StorageKey, ct);
        db.DocumentAttachments.Remove(att);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("AttachmentRemoved", EntityType, doc.Id, doc.Title, oldValues: att.FileName, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- helpers ----

    private bool CanAccess(Document d) => (int)d.Confidentiality <= (int)currentUser.Clearance;

    private static DateOnly? ComputeExpiry(DateOnly? documentDate, int retentionMonths) =>
        documentDate is { } date && retentionMonths > 0 ? date.AddMonths(retentionMonths) : null;

    private async Task<string> NextDocumentNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"DOC-{year}-";
        var count = await db.Documents.IgnoreQueryFilters()
            .CountAsync(d => d.DocumentNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:00000}";
    }

    private static DocumentDetail ToDetail(Document d) => new(
        d.Id, d.DocumentNumber, d.Title, d.Description, d.DocumentTypeId, d.DocumentType.Name,
        d.CategoryId, d.OwningOrgUnitId, d.OwnerPositionId, d.Confidentiality.ToString(), d.Status.ToString(),
        d.Keywords, d.RetentionMonths, d.DocumentDate, d.ExpiryDate, d.Version, d.ParentDocumentId, d.IsLatestVersion,
        d.CreatedAt,
        d.Attachments.OrderBy(a => a.Id).Select(a => new DocumentAttachmentDto(
            a.Id, a.FileName, a.ContentType, a.FileExtension, a.SizeBytes, a.IsScanned, a.CreatedAt,
            a.Kind.ToString(), a.SourceAttachmentId, a.PdfAConformance, a.PreservationValidated)).ToList());
}
