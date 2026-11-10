using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.InternalMail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>المراسلات — internal correspondence between staff/departments (addressing, read tracking,
/// threaded conversation). Thin controller: all rules live in <see cref="IInternalMailService"/>.</summary>
[ApiController]
[Route("api/internal-mail")]
[Authorize]
public sealed class InternalMailController(IInternalMailService service) : ControllerBase
{
    [HttpPost]
    [HasPermission("Correspondence.Create")]
    public async Task<IActionResult> Compose([FromBody] ComposeInternalRequest req, CancellationToken ct)
        => Wrap(await service.ComposeAsync(req, ct));

    [HttpPost("{id:long}/reply")]
    [HasPermission("Correspondence.Create")]
    public async Task<IActionResult> Reply(long id, [FromBody] ReplyInternalRequest req, CancellationToken ct)
        => Wrap(await service.ReplyAsync(id, req, ct));

    [HttpPost("{id:long}/forward")]
    [HasPermission("Correspondence.Forward")]
    public async Task<IActionResult> Forward(long id, [FromBody] ForwardInternalRequest req, CancellationToken ct)
        => Wrap(await service.ForwardAsync(id, req, ct));

    [HttpGet]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> List([FromQuery] InternalListFilter filter, CancellationToken ct)
        => Ok(await service.ListAsync(filter, ct));

    [HttpGet("search")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await service.SearchAsync(q, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Wrap(await service.GetByIdAsync(id, ct));

    [HttpGet("{id:long}/thread")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Thread(long id, CancellationToken ct)
        => Ok(await service.GetThreadAsync(id, ct));

    [HttpGet("{id:long}/read-status")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> ReadStatus(long id, CancellationToken ct)
        => Ok(await service.GetReadStatusAsync(id, ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
        => Bool(await service.MarkReadAsync(id, ct));

    [HttpPost("{id:long}/archive")]
    [HasPermission("Correspondence.Archive")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
        => Bool(await service.ArchiveAsync(id, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("Correspondence.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => Bool(await service.SoftDeleteAsync(id, ct));

    [HttpPost("{id:long}/restore")]
    [HasPermission("Correspondence.Delete")]
    public async Task<IActionResult> Restore(long id, CancellationToken ct)
        => Bool(await service.RestoreAsync(id, ct));

    [HttpPost("{id:long}/attachments")]
    [HasPermission("Correspondence.Edit")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> AddAttachment(long id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "الملف مطلوب" });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var r = await service.AddAttachmentAsync(id, file.FileName, file.ContentType, ms.ToArray(), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId, CancellationToken ct)
    {
        var r = await service.DownloadAttachmentAsync(id, attachmentId, ct);
        if (!r.Succeeded) return NotFound(new { error = r.Error });
        return File(r.Value!.Content, r.Value.ContentType, r.Value.FileName);
    }

    /// <summary>All of a message's attachments merged into one PDF — what "print the scanned original" uses.</summary>
    [HttpGet("{id:long}/attachments/combined")]
    [HasPermission("Correspondence.Print")]
    public async Task<IActionResult> CombinedAttachments(long id, CancellationToken ct)
    {
        var mail = await service.GetByIdAsync(id, ct);
        if (!mail.Succeeded) return NotFound(new { error = mail.Error });
        if (mail.Value!.Attachments.Count == 0) return BadRequest(new { error = "لا توجد مرفقات" });

        var parts = new List<(byte[], string)>();
        foreach (var a in mail.Value.Attachments)
        {
            var dl = await service.DownloadAttachmentAsync(id, a.Id, ct);
            if (!dl.Succeeded) continue;
            await using var s = dl.Value!.Content;
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, ct);
            parts.Add((ms.ToArray(), dl.Value.ContentType));
        }
        if (parts.Count == 0) return BadRequest(new { error = "تعذّر قراءة المرفقات" });

        return File(AttachmentMerger.Combine(parts), "application/pdf", $"internal-mail-{id}-attachments.pdf");
    }

    private IActionResult Wrap(Application.Common.Models.Result<InternalDetailDto> r)
        => r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    private IActionResult Bool(Application.Common.Models.Result<bool> r)
        => r.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = r.Error });
}
