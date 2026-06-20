using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

// ---- DTOs ----
public sealed record NoteDto(long Id, string Title, string? Content, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record NoteRequest(string Title, string? Content);
public sealed record FolderDto(long Id, string Name, long? ParentId, int DocumentCount);
public sealed record FolderRequest(string Name, long? ParentId);
public sealed record ShareDto(long DocumentId, long SharedWithUserId, string UserName, bool CanEdit, DateTime CreatedAt);
public sealed record CreateShareRequest(long UserId, bool CanEdit);
public sealed record MoveFolderRequest(long? FolderId);

/// <summary>Per-user features: notepad, personal folders, favorites, and document sharing.</summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class UserFeaturesController(AppDbContext db, ICurrentUser currentUser, IAuditWriter audit) : ControllerBase
{
    private long Uid => currentUser.UserId ?? 0;

    // ---------------- Notepad ----------------
    [HttpGet("notes")]
    public async Task<IActionResult> Notes(CancellationToken ct) =>
        Ok(await db.UserNotes.Where(n => n.UserId == Uid).OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Select(n => new NoteDto(n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt)).ToListAsync(ct));

    [HttpPost("notes")]
    public async Task<IActionResult> CreateNote([FromBody] NoteRequest r, CancellationToken ct)
    {
        var n = new UserNote { UserId = Uid, Title = string.IsNullOrWhiteSpace(r.Title) ? "ملاحظة" : r.Title.Trim(), Content = r.Content };
        db.UserNotes.Add(n);
        await db.SaveChangesAsync(ct);
        return Ok(new NoteDto(n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt));
    }

    [HttpPut("notes/{id:long}")]
    public async Task<IActionResult> UpdateNote(long id, [FromBody] NoteRequest r, CancellationToken ct)
    {
        var n = await db.UserNotes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid, ct);
        if (n is null) return NotFound();
        n.Title = string.IsNullOrWhiteSpace(r.Title) ? n.Title : r.Title.Trim();
        n.Content = r.Content; n.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new NoteDto(n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt));
    }

    [HttpDelete("notes/{id:long}")]
    public async Task<IActionResult> DeleteNote(long id, CancellationToken ct)
    {
        var n = await db.UserNotes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid, ct);
        if (n is null) return NotFound();
        db.UserNotes.Remove(n); await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------- Personal folders ----------------
    [HttpGet("folders")]
    public async Task<IActionResult> Folders(CancellationToken ct) =>
        Ok(await db.Folders.Where(f => f.UserId == Uid).OrderBy(f => f.Name)
            .Select(f => new FolderDto(f.Id, f.Name, f.ParentId, db.Documents.Count(d => d.FolderId == f.Id))).ToListAsync(ct));

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] FolderRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "اسم المجلد مطلوب" });
        var f = new Folder { UserId = Uid, Name = r.Name.Trim(), ParentId = r.ParentId };
        db.Folders.Add(f); await db.SaveChangesAsync(ct);
        return Ok(new FolderDto(f.Id, f.Name, f.ParentId, 0));
    }

    [HttpPut("folders/{id:long}")]
    public async Task<IActionResult> UpdateFolder(long id, [FromBody] FolderRequest r, CancellationToken ct)
    {
        var f = await db.Folders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid, ct);
        if (f is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(r.Name)) f.Name = r.Name.Trim();
        if (r.ParentId != id) f.ParentId = r.ParentId;
        await db.SaveChangesAsync(ct);
        return Ok(new FolderDto(f.Id, f.Name, f.ParentId, await db.Documents.CountAsync(d => d.FolderId == f.Id, ct)));
    }

    [HttpDelete("folders/{id:long}")]
    public async Task<IActionResult> DeleteFolder(long id, CancellationToken ct)
    {
        var f = await db.Folders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid, ct);
        if (f is null) return NotFound();
        // Unfile documents and detach child folders, then delete.
        await db.Documents.Where(d => d.FolderId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.FolderId, (long?)null), ct);
        await db.Folders.Where(x => x.ParentId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentId, (long?)null), ct);
        db.Folders.Remove(f); await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("documents/{id:long}/folder")]
    public async Task<IActionResult> MoveToFolder(long id, [FromBody] MoveFolderRequest r, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();
        if (r.FolderId is { } fid && !await db.Folders.AnyAsync(f => f.Id == fid && f.UserId == Uid, ct))
            return BadRequest(new { error = "المجلد غير موجود" });
        doc.FolderId = r.FolderId;
        await db.SaveChangesAsync(ct);
        return Ok(new { doc.Id, doc.FolderId });
    }

    // ---------------- Favorites ----------------
    [HttpPost("documents/{id:long}/favorite")]
    public async Task<IActionResult> AddFavorite(long id, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return NotFound();
        if (!await db.DocumentFavorites.AnyAsync(f => f.DocumentId == id && f.UserId == Uid, ct))
        {
            db.DocumentFavorites.Add(new DocumentFavorite { UserId = Uid, DocumentId = id });
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { favorite = true });
    }

    [HttpDelete("documents/{id:long}/favorite")]
    public async Task<IActionResult> RemoveFavorite(long id, CancellationToken ct)
    {
        var f = await db.DocumentFavorites.FirstOrDefaultAsync(x => x.DocumentId == id && x.UserId == Uid, ct);
        if (f is not null) { db.DocumentFavorites.Remove(f); await db.SaveChangesAsync(ct); }
        return Ok(new { favorite = false });
    }

    // ---------------- Sharing ----------------
    [HttpGet("documents/{id:long}/shares")]
    public async Task<IActionResult> Shares(long id, CancellationToken ct) =>
        Ok(await db.DocumentShares.Where(s => s.DocumentId == id)
            .Select(s => new ShareDto(s.DocumentId, s.SharedWithUserId,
                db.Users.Where(u => u.Id == s.SharedWithUserId).Select(u => u.FullName).FirstOrDefault() ?? "—",
                s.CanEdit, s.CreatedAt)).ToListAsync(ct));

    [HttpPost("documents/{id:long}/shares")]
    public async Task<IActionResult> Share(long id, [FromBody] CreateShareRequest r, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return NotFound();
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct)) return BadRequest(new { error = "المستخدم غير موجود" });

        var existing = await db.DocumentShares.FirstOrDefaultAsync(s => s.DocumentId == id && s.SharedWithUserId == r.UserId, ct);
        if (existing is null)
            db.DocumentShares.Add(new DocumentShare { DocumentId = id, SharedWithUserId = r.UserId, SharedByUserId = Uid, CanEdit = r.CanEdit });
        else
            existing.CanEdit = r.CanEdit;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Shared", "Document", id, $"user={r.UserId}", ct: ct);
        return Ok(new { shared = true });
    }

    [HttpDelete("documents/{id:long}/shares/{userId:long}")]
    public async Task<IActionResult> Unshare(long id, long userId, CancellationToken ct)
    {
        var s = await db.DocumentShares.FirstOrDefaultAsync(x => x.DocumentId == id && x.SharedWithUserId == userId, ct);
        if (s is not null) { db.DocumentShares.Remove(s); await db.SaveChangesAsync(ct); }
        return NoContent();
    }
}
