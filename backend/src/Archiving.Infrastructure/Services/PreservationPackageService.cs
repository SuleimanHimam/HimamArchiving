using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>OAIS information packages (ISO 14721): keeps each document's SIP and AIP (with a manifest
/// snapshot + Representation Information) current, and exports the AIP as a ZIP, logging a DIP.</summary>
public sealed class PreservationPackageService(
    AppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditWriter audit) : IPreservationPackageService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Arabic readable
    };

    public async Task<Result<DocumentPackagesDto>> GetPackagesAsync(long documentId, CancellationToken ct = default)
    {
        var ensured = await EnsureAsync(documentId, ct);
        if (!ensured.Succeeded) return Result<DocumentPackagesDto>.Fail(ensured.Error!);
        var (doc, atts, reps) = ensured.Value;

        var originals = atts.Where(a => a.Kind == AttachmentKind.Original).ToList();
        var masters = atts.Where(a => a.Kind == AttachmentKind.PreservationMaster).ToList();
        var aipFiles = masters.Count > 0 ? masters : originals;

        var sip = await PackageDtoAsync(doc.Id, PackageType.Submission, originals, reps, ct);
        var aip = await PackageDtoAsync(doc.Id, PackageType.Archival, aipFiles, reps, ct);
        var dips = await db.InformationPackages
            .Where(p => p.DocumentId == doc.Id && p.Type == PackageType.Dissemination)
            .OrderByDescending(p => p.Id)
            .Select(p => new InformationPackageDto(p.Id, "DIP", p.DocumentId, p.FileCount, p.TotalBytes, p.CreatedAt, new List<PackageFileDto>()))
            .ToListAsync(ct);

        var repDtos = reps.Select(r => new RepresentationDto(
            r.DocumentAttachmentId, atts.First(a => a.Id == r.DocumentAttachmentId).FileName,
            r.FormatName, r.MimeType, r.PronomPuid, r.RenderingNote)).ToList();

        return Result<DocumentPackagesDto>.Ok(new DocumentPackagesDto(
            doc.Id, doc.DocumentNumber, doc.Title, sip, aip, dips, repDtos));
    }

    public async Task<Result<AipExport>> ExportAipAsync(long documentId, CancellationToken ct = default)
    {
        var ensured = await EnsureAsync(documentId, ct);
        if (!ensured.Succeeded) return Result<AipExport>.Fail(ensured.Error!);
        var (doc, atts, reps) = ensured.Value;

        var masters = atts.Where(a => a.Kind == AttachmentKind.PreservationMaster).ToList();
        var files = masters.Count > 0 ? masters : atts.Where(a => a.Kind == AttachmentKind.Original).ToList();
        if (files.Count == 0) return Result<AipExport>.Fail("لا توجد ملفات في الحزمة");

        var (manifest, totalBytes) = await BuildManifestAsync(doc, PackageType.Archival, files, reps, ct);

        using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            await using (var ms = manifestEntry.Open())
                await ms.WriteAsync(Encoding.UTF8.GetBytes(manifest), ct);

            foreach (var f in files)
            {
                var stream = await storage.OpenAsync(f.StorageKey, ct);
                if (stream is null) continue;
                var entry = zip.CreateEntry($"data/{f.Id}-{f.FileName}");
                await using var es = entry.Open();
                await using (stream) await stream.CopyToAsync(es, ct);
            }
        }
        var zipBytes = zipMs.ToArray();

        // Record the dissemination (DIP).
        db.InformationPackages.Add(new InformationPackage
        {
            DocumentId = doc.Id, Type = PackageType.Dissemination,
            FileCount = files.Count, TotalBytes = totalBytes, Manifest = manifest,
        });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("AipExported", "Document", doc.Id, doc.Title, newValues: $"files={files.Count}", ct: ct);

        return Result<AipExport>.Ok(new AipExport(zipBytes, $"AIP-{doc.DocumentNumber}.zip"));
    }

    // ---- ensure / upsert ----

    private async Task<Result<(Document Doc, List<DocumentAttachment> Atts, List<RepresentationInfo> Reps)>> EnsureAsync(
        long documentId, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.Attachments).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<(Document, List<DocumentAttachment>, List<RepresentationInfo>)>.Fail("الوثيقة غير موجودة");
        if ((int)doc.Confidentiality > (int)currentUser.Clearance)
            return Result<(Document, List<DocumentAttachment>, List<RepresentationInfo>)>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        var atts = doc.Attachments.OrderBy(a => a.Id).ToList();
        var attIds = atts.Select(a => a.Id).ToList();
        var reps = await db.RepresentationInfos.Where(r => attIds.Contains(r.DocumentAttachmentId)).ToListAsync(ct);

        foreach (var att in atts)
        {
            var (puid, fmt, note) = PronomMap.For(att.FileExtension, att.PdfAConformance, att.ContentType);
            var rep = reps.FirstOrDefault(r => r.DocumentAttachmentId == att.Id);
            if (rep is null) { rep = new RepresentationInfo { DocumentAttachmentId = att.Id }; db.RepresentationInfos.Add(rep); reps.Add(rep); }
            rep.FormatName = fmt; rep.MimeType = att.ContentType; rep.PronomPuid = puid; rep.RenderingNote = note;
        }

        var originals = atts.Where(a => a.Kind == AttachmentKind.Original).ToList();
        var masters = atts.Where(a => a.Kind == AttachmentKind.PreservationMaster).ToList();
        await UpsertPackageAsync(doc, PackageType.Submission, originals, reps, ct);
        await UpsertPackageAsync(doc, PackageType.Archival, masters.Count > 0 ? masters : originals, reps, ct);
        await db.SaveChangesAsync(ct);

        return Result<(Document, List<DocumentAttachment>, List<RepresentationInfo>)>.Ok((doc, atts, reps));
    }

    private async Task UpsertPackageAsync(Document doc, PackageType type, List<DocumentAttachment> files, List<RepresentationInfo> reps, CancellationToken ct)
    {
        var pkg = await db.InformationPackages.FirstOrDefaultAsync(p => p.DocumentId == doc.Id && p.Type == type, ct);
        if (pkg is null) { pkg = new InformationPackage { DocumentId = doc.Id, Type = type }; db.InformationPackages.Add(pkg); }
        var (manifest, totalBytes) = await BuildManifestAsync(doc, type, files, reps, ct);
        pkg.Manifest = manifest;
        pkg.FileCount = files.Count;
        pkg.TotalBytes = totalBytes;
    }

    private async Task<(string Json, long TotalBytes)> BuildManifestAsync(
        Document doc, PackageType type, List<DocumentAttachment> files, List<RepresentationInfo> reps, CancellationToken ct)
    {
        var community = await db.DesignatedCommunities.OrderBy(c => c.Id).Select(c => c.Name).FirstOrDefaultAsync(ct);
        var manifest = new
        {
            packageType = TypeCode(type),
            documentNumber = doc.DocumentNumber,
            title = doc.Title,
            generatedAt = DateTime.UtcNow,
            designatedCommunity = community,
            metadata = new
            {
                confidentiality = doc.Confidentiality.ToString(),
                status = doc.Status.ToString(),
                retentionMonths = doc.RetentionMonths,
                documentDate = doc.DocumentDate,
                expiryDate = doc.ExpiryDate,
                owningOrgUnitId = doc.OwningOrgUnitId,
                version = doc.Version,
            },
            files = files.Select(f =>
            {
                var rep = reps.FirstOrDefault(r => r.DocumentAttachmentId == f.Id);
                return new
                {
                    attachmentId = f.Id,
                    fileName = f.FileName,
                    contentType = f.ContentType,
                    sizeBytes = f.SizeBytes,
                    checksum = f.Checksum,
                    algorithm = f.ChecksumAlgorithm,
                    kind = f.Kind.ToString(),
                    pdfaConformance = f.PdfAConformance,
                    pronomPuid = rep?.PronomPuid,
                    format = rep?.FormatName,
                };
            }).ToList(),
        };
        return (JsonSerializer.Serialize(manifest, Json), files.Sum(f => f.SizeBytes));
    }

    private async Task<InformationPackageDto?> PackageDtoAsync(long documentId, PackageType type, List<DocumentAttachment> files, List<RepresentationInfo> reps, CancellationToken ct)
    {
        var pkg = await db.InformationPackages.FirstOrDefaultAsync(p => p.DocumentId == documentId && p.Type == type, ct);
        if (pkg is null) return null;
        var fileDtos = files.Select(f => new PackageFileDto(
            f.Id, f.FileName, f.ContentType, f.SizeBytes, f.Checksum, f.ChecksumAlgorithm, f.Kind.ToString(),
            reps.FirstOrDefault(r => r.DocumentAttachmentId == f.Id)?.PronomPuid)).ToList();
        return new InformationPackageDto(pkg.Id, TypeCode(type), pkg.DocumentId, pkg.FileCount, pkg.TotalBytes, pkg.CreatedAt, fileDtos);
    }

    private static string TypeCode(PackageType t) => t switch
    {
        PackageType.Submission => "SIP",
        PackageType.Archival => "AIP",
        _ => "DIP",
    };
}
