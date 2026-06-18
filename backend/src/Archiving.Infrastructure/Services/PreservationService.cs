using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Generates a PDF/A-2b preservation master (AIP) from a submitted file, keeping the original
/// (SIP) intact. Validates the result with veraPDF and records full provenance. ISO 19005 / 14721.</summary>
public sealed class PreservationService(
    AppDbContext db,
    IFileStorage storage,
    IPdfaValidator validator,
    IPreservationPolicyService policy,
    IAuditWriter audit) : IPreservationService
{
    public async Task<Result<PreservationCopyDto>> GeneratePreservationCopyAsync(long attachmentId, CancellationToken ct = default)
    {
        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (att is null) return Result<PreservationCopyDto>.Fail("المرفق غير موجود");
        if (att.Kind == AttachmentKind.PreservationMaster)
            return Result<PreservationCopyDto>.Fail("هذا المرفق نسخة حفظ بالفعل");

        // Idempotent — return the existing master if one was already generated.
        var existing = await db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.SourceAttachmentId == attachmentId && a.Kind == AttachmentKind.PreservationMaster, ct);
        if (existing is not null)
            return Result<PreservationCopyDto>.Ok(new PreservationCopyDto(
                true, existing.Id, attachmentId, existing.FileName, existing.PdfAConformance,
                existing.PreservationValidated, existing.PreservationNote));

        var stream = await storage.OpenAsync(att.StorageKey, ct);
        if (stream is null) return Result<PreservationCopyDto>.Fail("تعذّر العثور على الملف الأصلي");
        byte[] bytes;
        await using (stream) { using var ms = new MemoryStream(); await stream.CopyToAsync(ms, ct); bytes = ms.ToArray(); }

        // Only images can be normalized here (the scan ingest path); arbitrary PDFs need a heavier converter.
        if (!OperatingSystem.IsWindows() || !PdfaNormalizer.CanNormalize(att.ContentType, bytes))
        {
            att.PreservationNote = "صيغة غير قابلة للتحويل التلقائي إلى PDF/A (يلزم محوّل إضافي)";
            await db.SaveChangesAsync(ct);
            return Result<PreservationCopyDto>.Ok(new PreservationCopyDto(
                false, null, attachmentId, null, null, false, att.PreservationNote));
        }

        var pol = await policy.GetAsync(ct);
        var conformance = pol.TargetPdfAConformance;
        var pdfa = PdfaNormalizer.ImageToPdfA(bytes, conformance);
        var baseName = Path.GetFileNameWithoutExtension(att.FileName);
        var stored = await storage.SaveAsync($"documents/{att.DocumentId}/preservation", $"{baseName}.pdf", new MemoryStream(pdfa), ct);

        // veraPDF flavour from the conformance, e.g. "PDF/A-2B" -> "2b"
        var flavour = conformance.Replace("PDF/A-", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var validation = await validator.ValidateAsync(pdfa, flavour, ct);

        var master = new DocumentAttachment
        {
            DocumentId = att.DocumentId,
            FileName = $"{baseName}.pdf",
            ContentType = "application/pdf",
            FileExtension = "pdf",
            SizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            Checksum = stored.Checksum,
            ChecksumAlgorithm = "SHA-256",
            Kind = AttachmentKind.PreservationMaster,
            SourceAttachmentId = att.Id,
            PdfAConformance = conformance,
            PreservationValidated = validation.Validated,
            PreservationNote = validation.Note,
        };
        db.DocumentAttachments.Add(master);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PreservationCopyCreated", "Document", att.DocumentId, master.FileName,
            newValues: $"{conformance}; validated={validation.Validated}", ct: ct);

        return Result<PreservationCopyDto>.Ok(new PreservationCopyDto(
            true, master.Id, att.Id, master.FileName, master.PdfAConformance, master.PreservationValidated, master.PreservationNote));
    }
}
