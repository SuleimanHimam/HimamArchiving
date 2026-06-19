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
    IPreservationPolicyService policy) : ControllerBase
{
    // ---- Configuration: types & categories ----
    [HttpGet("types")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Types(CancellationToken ct) => Ok(await service.ListTypesAsync(ct));

    [HttpPost("types")]
    [HasPermission("Classification.Edit")]
    public async Task<IActionResult> CreateType([FromBody] CreateDocumentTypeRequest req, CancellationToken ct)
    {
        var r = await service.CreateTypeAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
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

    // ---- Documents ----
    [HttpGet]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] DocumentStatus? status,
        [FromQuery] long? documentTypeId,
        [FromQuery] long? owningOrgUnitId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(new DocumentQuery(search, status, documentTypeId, owningOrgUnitId, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await service.GetAsync(id, ct);
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
