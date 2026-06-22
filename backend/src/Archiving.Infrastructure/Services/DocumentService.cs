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
            Name = r.Name, NameEn = r.NameEn, CategoryId = r.CategoryId,
            Code = string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim(),   // optional identification code
            DefaultConfidentiality = r.DefaultConfidentiality,
            RetentionMonths = r.RetentionMonths <= 0 ? 120 : r.RetentionMonths,
            RequiresApproval = r.RequiresApproval,
            AllowedUploadSources = r.AllowedUploadSources == UploadSource.None ? UploadSource.All : r.AllowedUploadSources,
        };
        db.DocumentTypes.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<DocumentTypeDto>.Ok(ToTypeDto(e));
    }

    public async Task<Result<DocumentTypeDto>> UpdateTypeAsync(long id, UpdateDocumentTypeRequest r, CancellationToken ct = default)
    {
        var e = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<DocumentTypeDto>.Fail("نوع الوثيقة غير موجود");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<DocumentTypeDto>.Fail("اسم النوع مطلوب");
        if (r.CategoryId is { } cid && !await db.DocumentCategories.AnyAsync(c => c.Id == cid, ct))
            return Result<DocumentTypeDto>.Fail("التصنيف غير موجود");

        e.Name = r.Name; e.NameEn = r.NameEn; e.CategoryId = r.CategoryId;
        e.Code = string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim();   // optional identification code
        e.DefaultConfidentiality = r.DefaultConfidentiality;
        e.RetentionMonths = r.RetentionMonths <= 0 ? 120 : r.RetentionMonths;
        e.RequiresApproval = r.RequiresApproval;
        e.AllowedUploadSources = r.AllowedUploadSources == UploadSource.None ? UploadSource.All : r.AllowedUploadSources;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<DocumentTypeDto>.Ok(ToTypeDto(e));
    }

    public async Task<Result<bool>> DeleteTypeAsync(long id, CancellationToken ct = default)
    {
        var e = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<bool>.Fail("نوع الوثيقة غير موجود");
        if (await db.Documents.AnyAsync(d => d.DocumentTypeId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف نوع مستخدم في وثائق — يمكنك تعطيله بدلاً من ذلك");

        db.DocumentTypes.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "DocumentType", e.Id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    private static DocumentTypeDto ToTypeDto(DocumentType e) => new(
        e.Id, e.Name, e.NameEn, e.Code, e.CategoryId,
        e.DefaultConfidentiality.ToString(), e.RetentionMonths, e.RequiresApproval,
        e.AllowedUploadSources.ToString(), e.IsActive);

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

    public async Task<Result<DocumentCategoryDto>> UpdateCategoryAsync(long id, UpdateDocumentCategoryRequest r, CancellationToken ct = default)
    {
        var e = await db.DocumentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<DocumentCategoryDto>.Fail("التصنيف غير موجود");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result<DocumentCategoryDto>.Fail("اسم التصنيف مطلوب");
        if (r.ParentId == id) return Result<DocumentCategoryDto>.Fail("لا يمكن جعل التصنيف أبًا لنفسه");
        if (r.ParentId is { } pid && !await db.DocumentCategories.AnyAsync(c => c.Id == pid, ct))
            return Result<DocumentCategoryDto>.Fail("التصنيف الأب غير موجود");

        e.Name = r.Name.Trim(); e.Code = r.Code; e.ParentId = r.ParentId; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<DocumentCategoryDto>.Ok(new DocumentCategoryDto(e.Id, e.ParentId, e.Name, e.Code, e.IsActive));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var e = await db.DocumentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e is null) return Result<bool>.Fail("التصنيف غير موجود");
        if (await db.DocumentCategories.AnyAsync(c => c.ParentId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف تصنيف يحتوي على تصنيفات فرعية");
        if (await db.Documents.AnyAsync(d => d.CategoryId == id, ct) || await db.DocumentTypes.AnyAsync(tp => tp.CategoryId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف تصنيف مستخدم — يمكنك تعطيله بدلاً من ذلك");

        db.DocumentCategories.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "DocumentCategory", e.Id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
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
        if (query.DateFrom is { } df) q = q.Where(d => d.DocumentDate >= df);
        if (query.DateTo is { } dt) q = q.Where(d => d.DocumentDate <= dt);
        if (query.FolderId is { } folderId) q = q.Where(d => d.FolderId == folderId);
        if (query.FavoritesOnly)
            q = q.Where(d => db.DocumentFavorites.Any(f => f.DocumentId == d.Id && f.UserId == currentUser.UserId));
        if (query.SharedWithMe)
            q = q.Where(d => db.DocumentShares.Any(sh => sh.DocumentId == d.Id && sh.SharedWithUserId == currentUser.UserId));

        // Filter by an admin-defined custom field value.
        if (query.CustomFieldId is { } cfId)
        {
            var cfVal = query.CustomFieldValue?.Trim();
            q = q.Where(d => db.CustomFieldValues.Any(v =>
                v.FieldId == cfId && v.EntityType == "Document" && v.EntityId == d.Id
                && (string.IsNullOrEmpty(cfVal) || v.Value.Contains(cfVal!))));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            // Match words *inside* files via the MySQL full-text index, plus metadata and attachment file names.
            var contentDocIds = await db.Database
                .SqlQuery<long>($"SELECT DISTINCT DocumentId AS Value FROM DocumentAttachments WHERE MATCH(ExtractedText) AGAINST({s} IN NATURAL LANGUAGE MODE)")
                .ToListAsync(ct);
            q = q.Where(d => d.DocumentNumber.Contains(s) || d.Title.Contains(s)
                || (d.Keywords != null && d.Keywords.Contains(s))
                || d.Attachments.Any(a => a.FileName.Contains(s))
                || contentDocIds.Contains(d.Id));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(d => new DocumentListItem(
                d.Id, d.DocumentNumber, d.Title, d.DocumentType.Name,
                d.Confidentiality.ToString(), d.Status.ToString(), d.Version,
                d.DocumentDate, d.ExpiryDate, d.CreatedAt,
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.PhysicalLocation.Name).FirstOrDefault(),
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.BoxNumber).FirstOrDefault(),
                db.PhysicalArchiveItems.Where(i => i.DocumentId == d.Id).OrderByDescending(i => i.Id)
                    .Select(i => i.FileNumber).FirstOrDefault(),
                db.Boxes.Where(b => b.Id == d.BoxId).Select(b => b.BoxCode).FirstOrDefault()))
            .ToListAsync(ct);

        // Attach admin-defined custom field values for the documents on this page.
        var ids = items.Select(i => i.Id).ToList();
        if (ids.Count > 0)
        {
            var values = await db.CustomFieldValues
                .Where(v => v.EntityType == "Document" && ids.Contains(v.EntityId))
                .Select(v => new { v.EntityId, v.FieldId, v.Value })
                .ToListAsync(ct);
            if (values.Count > 0)
            {
                var byDoc = values.GroupBy(v => v.EntityId)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.FieldId, x => x.Value));
                items = items.Select(it => byDoc.TryGetValue(it.Id, out var cv) ? it with { CustomValues = cv } : it).ToList();
            }
        }

        return new PagedResult<DocumentListItem> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<Result<DocumentDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.DocumentType).Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<DocumentDetail>.Fail("الوثيقة غير موجودة");
        if (!await CanAccessAsync(doc, ct)) return Result<DocumentDetail>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");
        var isFav = await db.DocumentFavorites.AnyAsync(f => f.DocumentId == id && f.UserId == currentUser.UserId, ct);
        var boxCode = doc.BoxId is { } bid ? await db.Boxes.Where(b => b.Id == bid).Select(b => b.BoxCode).FirstOrDefaultAsync(ct) : null;
        var detail = WithPhysical(ToDetail(doc), await GetPhysicalItemAsync(doc.Id, ct)) with { FolderId = doc.FolderId, IsFavorite = isFav, BoxCode = boxCode };
        return Result<DocumentDetail>.Ok(detail);
    }

    public async Task<Result<DocumentDetail>> CreateAsync(CreateDocumentRequest r, CancellationToken ct = default)
    {
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == r.DocumentTypeId, ct);
        if (type is null) return Result<DocumentDetail>.Fail("نوع الوثيقة غير موجود");
        if (!await db.OrgUnits.AnyAsync(u => u.Id == r.OwningOrgUnitId, ct))
            return Result<DocumentDetail>.Fail("الوحدة المالكة غير موجودة");
        if (r.PhysicalLocationId is { } locId && !await db.PhysicalLocations.AnyAsync(l => l.Id == locId, ct))
            return Result<DocumentDetail>.Fail("مكان الحفظ الفيزيائي غير موجود");

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
            BoxId = r.BoxId,
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        await AdjustBoxCountAsync(null, r.BoxId, ct);   // filing into a box bumps its CurrentCount
        await audit.WriteAsync("Created", EntityType, doc.Id, doc.Title, ct: ct);

        // Link the physical storage location at entry time, if provided.
        if (r.PhysicalLocationId is { } location)
        {
            db.PhysicalArchiveItems.Add(new PhysicalArchiveItem
            {
                DocumentId = doc.Id,
                PhysicalLocationId = location,
                BoxNumber = r.BoxNumber,
                FileNumber = r.FileNumber,
                ArchivedByUserId = currentUser.UserId,
            });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Archived", "Document", doc.Id, doc.Title, newValues: $"location={location}", ct: ct);
        }

        await db.Entry(doc).Reference(d => d.DocumentType).LoadAsync(ct);
        return Result<DocumentDetail>.Ok(WithPhysical(ToDetail(doc), await GetPhysicalItemAsync(doc.Id, ct)));
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
        // Admin-set expiry wins; otherwise derive it from the document date + retention.
        doc.ExpiryDate = r.ExpiryDate ?? ComputeExpiry(r.DocumentDate, doc.RetentionMonths);

        var oldBox = doc.BoxId;
        if (oldBox != r.BoxId)
        {
            doc.BoxId = r.BoxId;
            await AdjustBoxCountAsync(oldBox, r.BoxId, ct);   // move between boxes updates both counts
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", EntityType, doc.Id, doc.Title, ct: ct);
        return Result<DocumentDetail>.Ok(ToDetail(doc));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Result<bool>.Fail("الوثيقة غير موجودة");
        if (!CanAccess(doc)) return Result<bool>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        if (doc.BoxId is not null) await AdjustBoxCountAsync(doc.BoxId, null, ct);   // free a slot in the box

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
        if (!await CanAccessAsync(doc, ct)) return Result<AttachmentDownload>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

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

    // Clearance access, or the document was explicitly shared with the current user.
    private async Task<bool> CanAccessAsync(Document d, CancellationToken ct) =>
        CanAccess(d) || await db.DocumentShares.AnyAsync(s => s.DocumentId == d.Id && s.SharedWithUserId == currentUser.UserId, ct);

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

    private async Task<PhysicalArchiveItem?> GetPhysicalItemAsync(long docId, CancellationToken ct) =>
        await db.PhysicalArchiveItems.Include(i => i.PhysicalLocation)
            .Where(i => i.DocumentId == docId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);

    private static DocumentDetail WithPhysical(DocumentDetail d, PhysicalArchiveItem? item) =>
        item is null ? d : d with
        {
            PhysicalLocationId = item.PhysicalLocationId,
            PhysicalLocationName = item.PhysicalLocation.Name,
            BoxNumber = item.BoxNumber,
            FileNumber = item.FileNumber,
        };

    // Keep Box.CurrentCount in sync as documents are filed / moved / unfiled.
    private async Task AdjustBoxCountAsync(long? oldBoxId, long? newBoxId, CancellationToken ct)
    {
        if (oldBoxId == newBoxId) return;
        if (oldBoxId is { } ob)
        {
            var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == ob, ct);
            if (b is { CurrentCount: > 0 }) { b.CurrentCount--; await db.SaveChangesAsync(ct); }
        }
        if (newBoxId is { } nb)
        {
            var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == nb, ct);
            if (b is not null) { b.CurrentCount++; await db.SaveChangesAsync(ct); }
        }
    }

    private static DocumentDetail ToDetail(Document d) => new(
        d.Id, d.DocumentNumber, d.Title, d.Description, d.DocumentTypeId, d.DocumentType.Name,
        d.CategoryId, d.OwningOrgUnitId, d.OwnerPositionId, d.Confidentiality.ToString(), d.Status.ToString(),
        d.Keywords, d.RetentionMonths, d.DocumentDate, d.ExpiryDate, d.Version, d.ParentDocumentId, d.IsLatestVersion,
        d.CreatedAt,
        d.Attachments.OrderBy(a => a.Id).Select(a => new DocumentAttachmentDto(
            a.Id, a.FileName, a.ContentType, a.FileExtension, a.SizeBytes, a.IsScanned, a.CreatedAt,
            a.Kind.ToString(), a.SourceAttachmentId, a.PdfAConformance, a.PreservationValidated)).ToList())
    {
        IsTombstone = d.IsTombstone,
        DestroyedAtUtc = d.DestroyedAtUtc,
        BoxId = d.BoxId,
    };
}
