using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Documents;
using Archiving.Application.Features.Metadata;
using Archiving.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(
    IDocumentService service,
    IPreservationService preservation,
    IPreservationPackageService packages,
    IRecordMetadataService metadata,
    IPreservationPolicyService policy,
    IAuditWriter audit,
    ITextIndexingService indexing) : ControllerBase
{
    // ---- Full-text search reindex (re-extract text/OCR from every attachment) ----
    [HttpPost("reindex")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        var queued = await indexing.ReindexAllAsync(ct);
        return Ok(new { queued });
    }

    // ---- Configuration: types & categories ----
    [HttpGet("types")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Types([FromQuery] long? orgUnitId, CancellationToken ct)
        => Ok(await service.ListTypesAsync(orgUnitId, ct));

    [HttpPost("types")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> CreateType([FromBody] CreateDocumentTypeRequest req, CancellationToken ct)
    {
        var r = await service.CreateTypeAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("types/{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> UpdateType(long id, [FromBody] UpdateDocumentTypeRequest req, CancellationToken ct)
    {
        var r = await service.UpdateTypeAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("types/{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> DeleteType(long id, CancellationToken ct)
    {
        var r = await service.DeleteTypeAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpGet("categories")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await service.ListCategoriesAsync(ct));

    [HttpPost("categories")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateDocumentCategoryRequest req, CancellationToken ct)
    {
        var r = await service.CreateCategoryAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("categories/{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateDocumentCategoryRequest req, CancellationToken ct)
    {
        var r = await service.UpdateCategoryAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("categories/{id:long}")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct)
    {
        var r = await service.DeleteCategoryAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    // ---- Documents ----
    [HttpGet]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] DocumentStatus? status,
        [FromQuery] long? documentTypeId,
        [FromQuery] long? owningOrgUnitId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] bool favoritesOnly = false,
        [FromQuery] bool sharedWithMe = false,
        [FromQuery] long? folderId = null,
        [FromQuery] long? customFieldId = null,
        [FromQuery] string? customFieldValue = null,
        [FromQuery] long? boxId = null,
        [FromQuery] string? locationKind = null,
        [FromQuery] long? locationId = null,
        [FromQuery] bool unfiledOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(new DocumentQuery(search, status, documentTypeId, owningOrgUnitId,
            dateFrom, dateTo, favoritesOnly, sharedWithMe, folderId, customFieldId, customFieldValue, boxId,
            locationKind, locationId, unfiledOnly, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await service.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>The verification code and QR paths for this document, minting the code if needed.</summary>
    [HttpGet("{id:long}/verification")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Verification(long id, CancellationToken ct)
    {
        var r = await service.GetVerificationAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost]
    [HasPermission("Documents.Create")]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest req, CancellationToken ct)
    {
        var r = await service.CreateAsync(req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value)
            : BadRequest(new { error = r.Error });
    }

    /// <summary>The document's unified chronological timeline (audit, workflow, correspondence, …).</summary>
    // ---- Trash ----

    [HttpGet("trash")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Trash(CancellationToken ct) => Ok(await service.ListTrashAsync(ct));

    [HttpPost("{id:long}/restore")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Restore(long id, CancellationToken ct)
    {
        var r = await service.RestoreAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    /// <summary>Permanent: the content is crypto-shredded and cannot be brought back.</summary>
    [HttpDelete("{id:long}/purge")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Purge(long id, CancellationToken ct)
    {
        var r = await service.PurgeAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpDelete("trash")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> EmptyTrash(CancellationToken ct)
    {
        var r = await service.EmptyTrashAsync(ct);
        return r.Succeeded ? Ok(new { destroyed = r.Value }) : BadRequest(new { error = r.Error });
    }

    [HttpGet("{id:long}/properties")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Properties(long id, CancellationToken ct)
    {
        var r = await service.GetPropertiesAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("{id:long}/timeline")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Timeline(
        long id, [FromQuery] string? types, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromServices] Archiving.Application.Common.Interfaces.IDocumentTimelineService timeline, CancellationToken ct)
    {
        var r = await timeline.GetTimelineAsync(id, new Archiving.Application.Features.Timeline.TimelineFilter(types, from, to), ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Documents.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateDocumentRequest req, CancellationToken ct)
    {
        var r = await service.UpdateAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Documents.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await service.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    /// <summary>Submit the document to the Archive department for review + physical filing.</summary>
    [HttpPost("{id:long}/send-to-archive")]
    [HasPermission("Documents.Archive")]
    public async Task<IActionResult> SendToArchive(long id, CancellationToken ct)
    {
        var r = await service.SendToArchiveAsync(id, ct);
        return r.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = r.Error });
    }

    // ---- Archive intake: the receiving department's queue ----------------------------------
    // Gated on Archive.Archive, the custody right, rather than on Documents.* — this is the archive
    // acting on other departments' documents, which no Documents permission is meant to allow.

    /// <summary>Documents waiting for the archive to give them a physical place, oldest first.</summary>
    [HttpGet("archive-queue")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> ArchiveQueue(CancellationToken ct)
        => Ok(await service.ListArchiveIntakeAsync(ct));

    /// <summary>File a waiting document into a box — this is what makes it مؤرشف.</summary>
    [HttpPost("{id:long}/file-to-archive")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> FileToArchive(long id, [FromBody] FileToArchiveRequest req, CancellationToken ct)
    {
        var r = await service.FileToArchiveAsync(id, req, ct);
        return r.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = r.Error });
    }

    /// <summary>Hand a waiting document back to whoever submitted it, with a reason.</summary>
    [HttpPost("{id:long}/return-from-archive")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> ReturnFromArchive(long id, [FromBody] ReturnFromArchiveRequest req, CancellationToken ct)
    {
        var r = await service.ReturnFromArchiveAsync(id, req, ct);
        return r.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = r.Error });
    }

    // ---- Attachments ----
    [HttpPost("{id:long}/attachments")]
    [HasPermission("Documents.Edit")]
    [RequestSizeLimit(104_857_600)] // 100 MB
    public async Task<IActionResult> Upload(long id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "لم يتم إرفاق ملف" });
        await using var stream = file.OpenReadStream();
        var r = await service.AddAttachmentAsync(id, file.FileName, file.ContentType, stream, isScanned: false, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Ingests an image/PDF captured from the user's scanner (via their local scan agent).
    /// The resulting attachment is flagged as scanned and validated against the type's allowed sources.</summary>
    [HttpPost("{id:long}/scan")]
    [HasPermission("Documents.Edit")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Scan(long id, IFormFile file, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "لم يصل أي مسح ضوئي" });
        await using var input = file.OpenReadStream();
        // Produce the user-chosen output format (pdf | jpg | png).
        var (stream, fileName, contentType) = await ScanPdf.ToOutputAsync(
            input, file.FileName, file.ContentType, (format ?? "pdf").ToLowerInvariant(), ct);
        await using (stream)
        {
            var r = await service.AddAttachmentAsync(id, fileName, contentType, stream, isScanned: true, ct);
            if (!r.Succeeded) return BadRequest(new { error = r.Error });

            // ISO 19005: generate a PDF/A preservation master from the scan when policy allows
            // (best-effort — never block the upload).
            try
            {
                if ((await policy.GetAsync(ct)).AutoNormalizeOnIngest)
                    await preservation.GeneratePreservationCopyAsync(r.Value!.Id, ct);
            }
            catch { /* preservation failure must not fail the scan */ }

            return Ok(r.Value);
        }
    }

    /// <summary>Generate (or fetch) the PDF/A preservation master for an attachment — ISO 19005 / 14721.</summary>
    [HttpPost("{id:long}/attachments/{attachmentId:long}/preserve")]
    [HasPermission("Documents.Archive")]
    public async Task<IActionResult> Preserve(long id, long attachmentId, CancellationToken ct)
    {
        var r = await preservation.GeneratePreservationCopyAsync(attachmentId, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>OAIS information packages (SIP/AIP/DIP) + Representation Information — ISO 14721.</summary>
    [HttpGet("{id:long}/packages")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Packages(long id, CancellationToken ct)
    {
        var r = await packages.GetPackagesAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Download the AIP as a ZIP (preservation files + manifest.json). Records a DIP.</summary>
    [HttpGet("{id:long}/packages/aip/export")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> ExportAip(long id, CancellationToken ct)
    {
        var r = await packages.ExportAipAsync(id, ct);
        if (!r.Succeeded) return NotFound(new { error = r.Error });
        return File(r.Value!.Content, "application/zip", r.Value.FileName);
    }

    /// <summary>ISO 23081 records metadata — agents, relationships, and business activities.</summary>
    [HttpGet("{id:long}/metadata")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Metadata(long id, CancellationToken ct)
    {
        var r = await metadata.GetForDocumentAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost("{id:long}/metadata/agents")]
    [HasPermission("Documents.Edit")]
    public async Task<IActionResult> AddAgent(long id, [FromBody] AddAgentRequest req, CancellationToken ct)
    {
        var r = await metadata.AddAgentAsync("Document", id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:long}/metadata/relationships")]
    [HasPermission("Documents.Edit")]
    public async Task<IActionResult> AddRelationship(long id, [FromBody] AddRelationshipRequest req, CancellationToken ct)
    {
        var r = await metadata.AddRelationshipAsync(req with { SourceType = "Document", SourceId = id }, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Download(long id, long attachmentId, CancellationToken ct)
    {
        var r = await service.DownloadAttachmentAsync(id, attachmentId, ct);
        if (!r.Succeeded) return NotFound(new { error = r.Error });
        var d = r.Value!;
        await audit.WriteAsync("Download", "Documents", id, d.FileName, ct: ct);   // motion log: تنزيل ملف
        return File(d.Content, d.ContentType, d.FileName);
    }

    [HttpDelete("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission("Documents.Edit")]
    public async Task<IActionResult> RemoveAttachment(long id, long attachmentId, CancellationToken ct)
    {
        var r = await service.RemoveAttachmentAsync(id, attachmentId, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    /// <summary>Merges all of a document's attachments into one PDF (for "print all attachments").</summary>
    [HttpGet("{id:long}/attachments/combined")]
    [HasPermission("Documents.Print")]
    public async Task<IActionResult> CombinedAttachments(long id, CancellationToken ct)
    {
        var doc = await service.GetAsync(id, ct);
        if (!doc.Succeeded) return NotFound(new { error = doc.Error });
        if (doc.Value!.Attachments.Count == 0) return BadRequest(new { error = "لا توجد مرفقات" });

        var parts = new List<(byte[], string)>();
        foreach (var a in doc.Value.Attachments)
        {
            var dl = await service.DownloadAttachmentAsync(id, a.Id, ct);
            if (!dl.Succeeded) continue;
            await using var s = dl.Value!.Content;
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, ct);
            parts.Add((ms.ToArray(), dl.Value.ContentType));
        }

        var pdf = AttachmentMerger.Combine(parts);
        return File(pdf, "application/pdf", $"document-{id}-attachments.pdf");
    }
}
