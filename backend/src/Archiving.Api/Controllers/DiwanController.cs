using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Diwan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>الديوان — the external correspondence register (وارد/صادر with outside bodies).
/// Thin controller: all rules live in <see cref="IDiwanService"/>.</summary>
[ApiController]
[Route("api/diwan")]
[Authorize]
public sealed class DiwanController(IDiwanService service, ILetterheadService letterhead) : ControllerBase
{
    /// <summary>
    /// The outgoing letter rendered on its slot-based letterhead, as PDF.
    ///
    /// This is the only way an outgoing letter reaches paper: the same renderer serves the
    /// on-screen preview, so there is no second layout that could drift away from it. Incoming
    /// letters are not rendered here — they print from their scanned original, because putting our
    /// letterhead on someone else's letter would misrepresent it.
    /// </summary>
    [HttpGet("{id:long}/letter.pdf")]
    [HasPermission("Correspondence.Print")]
    public async Task<IActionResult> LetterPdf(long id, CancellationToken ct)
    {
        var result = await letterhead.RenderDiwanLetterAsync(id, ct);
        return result.Succeeded
            ? File(result.Value!, "application/pdf", $"diwan-{id}.pdf")
            : BadRequest(new { error = result.Error });
    }

    [HttpPost]
    [HasPermission("Correspondence.Create")]
    public async Task<IActionResult> Create([FromBody] SaveDiwanRequest req, CancellationToken ct)
        => Wrap(await service.CreateAsync(req, ct));

    [HttpGet]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> List([FromQuery] DiwanListFilter filter, CancellationToken ct)
        => Ok(await service.ListAsync(filter, ct));

    /// <summary>تصنيف البريد lookup. <c>scope</c> 0 External · 1 Internal narrows it to one
    /// register's types; omit it for the whole catalogue (settings screen, print designer).</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(
        [FromQuery] int? scope, [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await service.ListCategoriesAsync(scope, includeInactive, ct));

    [HttpPost("categories")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveMailCategoryRequest req, CancellationToken ct)
    {
        var r = await service.CreateCategoryAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("categories/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] SaveMailCategoryRequest req, CancellationToken ct)
    {
        var r = await service.UpdateCategoryAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("categories/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct)
    {
        var r = await service.DeleteCategoryAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpGet("search")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await service.SearchAsync(q, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Wrap(await service.GetByIdAsync(id, ct));

    // File a reply to a register entry — linked back to it as المرجع.
    [HttpPost("{id:long}/reply")]
    [HasPermission("Correspondence.Create")]
    public async Task<IActionResult> Reply(long id, [FromBody] ReplyDiwanRequest body, CancellationToken ct)
        => Wrap(await service.ReplyAsync(id, body, ct));

    // The full chain: the original, its replies, and entries citing it as مرجع.
    [HttpGet("{id:long}/thread")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Thread(long id, CancellationToken ct)
        => Ok(await service.GetThreadAsync(id, ct));

    // Convert a register entry into an internal message (مراسلة داخلية).
    [HttpPost("{id:long}/to-internal")]
    [HasPermission("Correspondence.Create")]
    public async Task<IActionResult> ConvertToInternal(long id, [FromBody] ConvertToInternalRequest body, CancellationToken ct)
    {
        var r = await service.ConvertToInternalAsync(id, body, ct);
        return r.Succeeded ? Ok(new { internalMailId = r.Value }) : BadRequest(new { error = r.Error });
    }

    /// <summary>من يطّلع على البريد — who can open this letter. Readable by anyone the register
    /// already shows the letter to; the service decides whether they may also change it.</summary>
    [HttpGet("{id:long}/access")]
    [HasPermission("Correspondence.View")]
    public async Task<IActionResult> Access(long id, CancellationToken ct)
    {
        var r = await service.GetAccessAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Names who may open the letter. Gated on Correspondence.Edit and, inside the
    /// service, on heading a department the letter actually reached.</summary>
    [HttpPut("{id:long}/access")]
    [HasPermission("Correspondence.Edit")]
    public async Task<IActionResult> SetAccess(long id, [FromBody] SetDiwanAccessRequest body, CancellationToken ct)
        => Bool(await service.SetAccessAsync(id, body, ct));

    // حالة البريد — change the register entry's status.
    [HttpPost("{id:long}/status")]
    [HasPermission("Correspondence.Edit")]
    public async Task<IActionResult> ChangeStatus(long id, [FromBody] ChangeDiwanStatusRequest body, CancellationToken ct)
        => Bool(await service.ChangeStatusAsync(id, body, ct));

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

    /// <summary>All of a letter's attachments merged into one PDF — what "print the scanned original" uses.</summary>
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

        return File(AttachmentMerger.Combine(parts), "application/pdf", $"diwan-{id}-attachments.pdf");
    }

    private IActionResult Wrap(Application.Common.Models.Result<DiwanDetailDto> r)
        => r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    private IActionResult Bool(Application.Common.Models.Result<bool> r)
        => r.Succeeded ? Ok(new { ok = true }) : BadRequest(new { error = r.Error });
}
