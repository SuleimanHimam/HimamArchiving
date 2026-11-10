using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

// ---- DTOs ----
public sealed record FolderDto(long Id, string Name, long? ParentId, int DocumentCount,
    string? Color = null, string? Icon = null,
    // Shared is false on your own folders and true on one someone opened up to you; the browser
    // then says whose it is and withholds the actions you were not given.
    bool Shared = false,
    bool CanEdit = true,
    string? OwnerName = null);
public sealed record FolderRequest(string Name, long? ParentId, string? Color = null, string? Icon = null);
public sealed record ShareDto(long DocumentId, long SharedWithUserId, string UserName, bool CanEdit, DateTime CreatedAt);
public sealed record CreateShareRequest(long UserId, bool CanEdit);
/// <summary>Grant the same level to every active user at once.</summary>
public sealed record ShareEveryoneRequest(bool CanEdit);
public sealed record MoveFolderRequest(long? FolderId);
public sealed record DocNoteDto(long Id, long UserId, string AuthorName, string Content, DateTime CreatedAt);
public sealed record DocNoteRequest(string Content);
public sealed record TableColumnsRequest(string? ConfigJson);
/// <summary>A file kept on its own, with no document wrapped around it.</summary>
public sealed record UserFileDto(long Id, string Name, long? FolderId, string FileExtension,
    string ContentType, long SizeBytes, DateTime CreatedAt, DateTime? UpdatedAt, bool ContentDestroyed,
    bool Shared = false, bool CanEdit = true, string? OwnerName = null);
/// <summary>One grant on a folder or a file, as its owner's share list shows it.</summary>
public sealed record ItemShareDto(long UserId, string UserName, bool CanEdit, DateTime CreatedAt);
public sealed record UserFileRequest(string Name, long? FolderId);
/// <summary>The properties card for a plain file. Thinner than a document's, because a file
/// has no number, no type and no retention — the fields it lacks are the point of it.</summary>
public sealed record UserFilePropertiesDto(
    long Id, string Name, string? FolderName, string? FolderColor, string? FolderIcon,
    string FileExtension, string ContentType,
    long SizeBytes, string? Checksum, DateTime CreatedAt, string? CreatedByName,
    DateTime? UpdatedAt, string? UpdatedByName, bool ContentDestroyed);

/// <summary>Per-user features: notepad, personal folders, favorites, and document sharing.</summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class UserFeaturesController(AppDbContext db, ICurrentUser currentUser, IAuditWriter audit) : ControllerBase
{
    private long Uid => currentUser.UserId ?? 0;

    // Can the caller see this document (clearance, or it was shared with them)?
    private async Task<bool> CanSeeDocAsync(long id, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return false;
        return (int)doc.Confidentiality <= (int)currentUser.Clearance
            || await db.DocumentShares.AnyAsync(s => s.DocumentId == id && s.SharedWithUserId == Uid, ct);
    }

    // ---------------- Who may reach a folder or a file ----------------

    /// <summary>
    /// The folders the caller may open, and the subset they may write into.
    ///
    /// <para>Resolved in one pass because almost every call below needs it: a folder is reachable
    /// if the caller owns it, or if any folder above it was shared with them. Walking upward per
    /// row would issue a query per ancestor; the tree is small, so it is loaded once and walked
    /// downward from each grant instead.</para>
    /// </summary>
    private sealed record FolderRights(
        HashSet<long> Read, HashSet<long> Write, IReadOnlyDictionary<long, long> OwnerOf);

    private async Task<FolderRights> FolderRightsAsync(CancellationToken ct)
    {
        var all = await db.Folders.Select(f => new { f.Id, f.ParentId, f.UserId }).ToListAsync(ct);
        var children = all.Where(f => f.ParentId != null)
            .GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var read = all.Where(f => f.UserId == Uid).Select(f => f.Id).ToHashSet();
        var write = read.ToHashSet();

        var grants = await db.FolderShares
            .Where(s => s.SharedWithUserId == Uid)
            .Select(s => new { s.FolderId, s.CanEdit })
            .ToListAsync(ct);

        // A grant reaches every folder under it. `seen` also guards against a parent chain that
        // loops back on itself, which would otherwise spin here forever.
        var seen = new HashSet<long>();
        foreach (var g in grants.OrderByDescending(g => g.CanEdit))   // the stronger grant wins
        {
            var queue = new Queue<long>([g.FolderId]);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id)) continue;
                read.Add(id);
                if (g.CanEdit) write.Add(id);
                if (children.TryGetValue(id, out var kids)) foreach (var k in kids) queue.Enqueue(k);
            }
        }

        return new FolderRights(read, write, all.ToDictionary(f => f.Id, f => f.UserId));
    }

    /// <summary>One file's row and what the caller may do with it: their own, one shared with them
    /// directly, or one sitting in a folder they were given.</summary>
    private async Task<(UserFile? Row, bool Read, bool Edit)> FileAccessAsync(long id, CancellationToken ct)
    {
        var f = await db.UserFiles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return (null, false, false);
        if (f.UserId == Uid) return (f, true, true);

        var direct = await db.UserFileShares
            .Where(s => s.UserFileId == id && s.SharedWithUserId == Uid)
            .Select(s => (bool?)s.CanEdit).FirstOrDefaultAsync(ct);
        if (direct is { } canEdit) return (f, true, canEdit);

        if (f.FolderId is { } fid)
        {
            var rights = await FolderRightsAsync(ct);
            if (rights.Read.Contains(fid)) return (f, true, rights.Write.Contains(fid));
        }
        return (f, false, false);
    }

    /// <summary>Names for the owners of borrowed rows, so a listing can say whose they are.</summary>
    private async Task<Dictionary<long, string>> OwnerNamesAsync(IEnumerable<long> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(u => u != Uid).Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ---------------- Plain files (kept without becoming documents) ----------------

    /// <summary>
    /// Files in one folder, or at the root when <paramref name="folderId"/> is omitted.
    ///
    /// <para>The root is also where files shared with the caller directly surface. They live in
    /// their owner's folders, which the caller cannot browse, so the root is the only place they
    /// are reachable at all — and each is labelled with whose it is.</para>
    /// </summary>
    [HttpGet("files")]
    public async Task<IActionResult> Files([FromQuery] long? folderId, CancellationToken ct)
    {
        var rights = await FolderRightsAsync(ct);
        List<UserFile> rows;

        if (folderId is { } fid)
        {
            if (!rights.Read.Contains(fid)) return Forbid();
            rows = await db.UserFiles.Where(f => f.FolderId == fid).OrderBy(f => f.Name).ToListAsync(ct);
        }
        else
        {
            rows = await db.UserFiles
                .Where(f => (f.UserId == Uid && f.FolderId == null)
                    || db.UserFileShares.Any(s => s.UserFileId == f.Id && s.SharedWithUserId == Uid))
                .OrderBy(f => f.Name).ToListAsync(ct);
        }

        var direct = await db.UserFileShares.Where(s => s.SharedWithUserId == Uid)
            .ToDictionaryAsync(s => s.UserFileId, s => s.CanEdit, ct);
        var names = await OwnerNamesAsync(rows.Select(f => f.UserId), ct);

        return Ok(rows.Select(f => FileDto(f, rights, direct, names)).ToList());
    }

    /// <summary>One row, plus what this caller may do with it. Kept in one place so the listing and
    /// the browser never disagree about whether a button should be there.</summary>
    private UserFileDto FileDto(UserFile f, FolderRights rights,
        IReadOnlyDictionary<long, bool> direct, IReadOnlyDictionary<long, string> names)
    {
        var mine = f.UserId == Uid;
        var canEdit = mine
            || (direct.TryGetValue(f.Id, out var e) && e)
            || (f.FolderId is { } fid && rights.Write.Contains(fid));
        return new UserFileDto(f.Id, f.Name, f.FolderId, f.FileExtension, f.ContentType,
            f.SizeBytes, f.CreatedAt, f.UpdatedAt, f.ContentDestroyed,
            !mine, canEdit, mine ? null : names.GetValueOrDefault(f.UserId));
    }

    /// <summary>Upload one file. It becomes a file and nothing else — no number, no type,
    /// no retention schedule.</summary>
    [HttpPost("files")]
    [RequestSizeLimit(1_073_741_824)]
    public async Task<IActionResult> UploadFile(
        IFormFile file, [FromQuery] long? folderId,
        [FromServices] IFileStorage storage, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "لم يُرفع أي ملف" });
        if (folderId is { } fid && !(await FolderRightsAsync(ct)).Write.Contains(fid))
            return BadRequest(new { error = "المجلد غير موجود أو لا تملك الإضافة إليه" });

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync($"files/{Uid}", file.FileName, stream, ct);

        var row = new UserFile
        {
            UserId = Uid,
            Name = StorageKeys.SafeDisplayName(file.FileName, "ملف"),
            FolderId = folderId,
            FileExtension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            Checksum = stored.Checksum,
        };
        db.UserFiles.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileUploaded", "UserFile", row.Id, row.Name, ct: ct);

        return Ok(new UserFileDto(row.Id, row.Name, row.FolderId, row.FileExtension, row.ContentType,
            row.SizeBytes, row.CreatedAt, row.UpdatedAt, row.ContentDestroyed));
    }

    /// <summary>What is known about one file: where it sits, how big it is, who put it
    /// there and who last touched it.</summary>
    [HttpGet("files/{id:long}/properties")]
    public async Task<IActionResult> FileProperties(long id, CancellationToken ct)
    {
        var (f, mayRead, _) = await FileAccessAsync(id, ct);
        if (f is null || !mayRead) return NotFound(new { error = "الملف غير موجود" });

        // Both names in one round-trip; the uploader and the last editor are usually
        // the same person, and asking twice for that is a wasted query.
        var ids = new[] { f.CreatedBy, f.UpdatedBy }.Where(u => u is not null).Select(u => u!.Value).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<long, string>()
            : await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(long? uid) => uid is { } v && names.TryGetValue(v, out var n) ? n : null;

        // Tint and symbol travel with the name, so the card shows the folder rather than spelling it.
        var folder = f.FolderId is { } fid
            ? await db.Folders.Where(x => x.Id == fid)
                .Select(x => new { x.Name, x.Color, x.Icon }).FirstOrDefaultAsync(ct)
            : null;

        return Ok(new UserFilePropertiesDto(
            f.Id, f.Name, folder?.Name, folder?.Color, folder?.Icon,
            f.FileExtension, f.ContentType, f.SizeBytes, f.Checksum,
            f.CreatedAt, NameOf(f.CreatedBy), f.UpdatedAt, NameOf(f.UpdatedBy), f.ContentDestroyed));
    }

    /// <summary>Rename a file, move it between folders, or both.</summary>
    [HttpPut("files/{id:long}")]
    public async Task<IActionResult> UpdateFile(long id, [FromBody] UserFileRequest r, CancellationToken ct)
    {
        var (row, mayRead, mayEdit) = await FileAccessAsync(id, ct);
        if (row is null || !mayRead || row.IsDeleted) return NotFound(new { error = "الملف غير موجود" });
        if (!mayEdit) return Forbid();
        // Moving it out of reach of the person who lent it — or into a drawer you were only shown —
        // is not an edit, so the destination is checked as a destination.
        if (r.FolderId is { } fid && !(await FolderRightsAsync(ct)).Write.Contains(fid))
            return BadRequest(new { error = "المجلد غير موجود أو لا تملك الإضافة إليه" });

        var name = StorageKeys.SafeDisplayName(r.Name, row.Name);
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "الاسم مطلوب" });

        row.Name = name;
        row.FolderId = r.FolderId;
        // The extension follows the name: renaming report.pdf to report.txt has to stop the
        // card claiming PDF, and the browser sorts and filters on this column.
        row.FileExtension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileUpdated", "UserFile", row.Id, row.Name, ct: ct);
        return NoContent();
    }

    /// <summary>Stream the file back. Inline, so the browser previews what it can.</summary>
    [HttpGet("files/{id:long}/content")]
    public async Task<IActionResult> FileContent(long id, [FromServices] IFileStorage storage, CancellationToken ct)
    {
        var (row, mayRead, _) = await FileAccessAsync(id, ct);
        if (row is null || !mayRead || row.ContentDestroyed) return NotFound(new { error = "الملف غير موجود" });

        var stream = await storage.OpenAsync(row.StorageKey, ct);
        if (stream is null) return NotFound(new { error = "محتوى الملف غير متاح" });

        var safe = Uri.EscapeDataString(row.Name);
        Response.Headers.ContentDisposition = "inline; filename*=UTF-8" + "''" + safe;
        return File(stream, row.ContentType);
    }

    /// <summary>Soft delete — the file moves to the trash and can be put back.</summary>
    [HttpDelete("files/{id:long}")]
    public async Task<IActionResult> DeleteFile(long id, CancellationToken ct)
    {
        var row = await db.UserFiles.FirstOrDefaultAsync(f => f.Id == id && f.UserId == Uid, ct);
        if (row is null) return NotFound(new { error = "الملف غير موجود" });
        db.UserFiles.Remove(row);   // the audit interceptor turns this into a soft delete
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileDeleted", "UserFile", row.Id, row.Name, ct: ct);
        return NoContent();
    }

    [HttpGet("files/trash")]
    public async Task<IActionResult> FilesTrash(CancellationToken ct) =>
        Ok(await db.UserFiles.IgnoreQueryFilters()
            .Where(f => f.UserId == Uid && f.IsDeleted && !f.ContentDestroyed)
            .OrderByDescending(f => f.DeletedAt)
            .Select(f => new UserFileDto(f.Id, f.Name, f.FolderId, f.FileExtension, f.ContentType,
                f.SizeBytes, f.CreatedAt, f.DeletedAt, f.ContentDestroyed))
            .ToListAsync(ct));

    [HttpPost("files/{id:long}/restore")]
    public async Task<IActionResult> RestoreFile(long id, CancellationToken ct)
    {
        var row = await db.UserFiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == Uid, ct);
        if (row is null) return NotFound(new { error = "الملف غير موجود" });
        if (row.ContentDestroyed) return BadRequest(new { error = "لا يمكن استرجاع ملف أُتلف محتواه" });

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedBy = null;
        // A file restored into a folder that was deleted meanwhile would vanish from every
        // listing, so it comes back to the root instead of nowhere.
        if (row.FolderId is { } fid && !await db.Folders.AnyAsync(x => x.Id == fid && x.UserId == Uid, ct))
            row.FolderId = null;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileRestored", "UserFile", row.Id, row.Name, ct: ct);
        return NoContent();
    }

    /// <summary>Permanent: the content is crypto-shredded and the row is kept as a record of it.</summary>
    [HttpDelete("files/{id:long}/purge")]
    public async Task<IActionResult> PurgeFile(long id, [FromServices] IFileStorage storage, CancellationToken ct)
    {
        var row = await db.UserFiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == Uid, ct);
        if (row is null) return NotFound(new { error = "الملف غير موجود" });

        if (!row.ContentDestroyed)
        {
            try { await storage.CryptoShredAsync(row.StorageKey, ct); }
            catch { /* an object already gone stays gone; the row is still marked */ }
            row.ContentDestroyed = true;
        }
        row.IsDeleted = true;
        row.DeletedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FilePurged", "UserFile", row.Id, row.Name, ct: ct);
        return NoContent();
    }

    // ---------------- Document notes (comments on a document) ----------------
    [HttpGet("documents/{id:long}/notes")]
    [HasPermission("Notes.View")]
    public async Task<IActionResult> DocNotes(long id, CancellationToken ct)
    {
        if (!await CanSeeDocAsync(id, ct)) return Forbid();
        return Ok(await db.DocumentNotes.Where(n => n.DocumentId == id).OrderByDescending(n => n.CreatedAt)
            .Select(n => new DocNoteDto(n.Id, n.UserId,
                db.Users.Where(u => u.Id == n.UserId).Select(u => u.FullName).FirstOrDefault() ?? "—",
                n.Content, n.CreatedAt)).ToListAsync(ct));
    }

    [HttpPost("documents/{id:long}/notes")]
    [HasPermission("Notes.Create")]
    public async Task<IActionResult> AddDocNote(long id, [FromBody] DocNoteRequest r, CancellationToken ct)
    {
        if (!await CanSeeDocAsync(id, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(r.Content)) return BadRequest(new { error = "الملاحظة فارغة" });
        var n = new DocumentNote { DocumentId = id, UserId = Uid, Content = r.Content.Trim() };
        db.DocumentNotes.Add(n);
        await db.SaveChangesAsync(ct);
        var author = await db.Users.Where(u => u.Id == Uid).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "—";
        return Ok(new DocNoteDto(n.Id, n.UserId, author, n.Content, n.CreatedAt));
    }

    [HttpDelete("documents/{id:long}/notes/{noteId:long}")]
    [HasPermission("Notes.Create")]
    public async Task<IActionResult> DeleteDocNote(long id, long noteId, CancellationToken ct)
    {
        var n = await db.DocumentNotes.FirstOrDefaultAsync(x => x.Id == noteId && x.DocumentId == id, ct);
        if (n is null) return NotFound();
        if (n.UserId != Uid) return Forbid(); // authors delete their own notes
        db.DocumentNotes.Remove(n); await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------- Personal folders ----------------
    /// <summary>My folders, plus the ones opened up to me and everything under them.</summary>
    [HttpGet("folders")]
    public async Task<IActionResult> Folders(CancellationToken ct)
    {
        var rights = await FolderRightsAsync(ct);
        var rows = await db.Folders.Where(f => rights.Read.Contains(f.Id)).OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.ParentId, f.UserId, f.Color, f.Icon,
                Count = db.Documents.Count(d => d.FolderId == f.Id) })
            .ToListAsync(ct);
        var names = await OwnerNamesAsync(rows.Select(f => f.UserId), ct);

        return Ok(rows.Select(f =>
        {
            var mine = f.UserId == Uid;
            // A borrowed sub-folder hangs off a parent the caller cannot see, which would leave it
            // orphaned in the tree. Re-rooted so it is reachable, the way a shared file is.
            var parent = f.ParentId is { } p && rights.Read.Contains(p) ? p : (long?)null;
            return new FolderDto(f.Id, f.Name, parent, f.Count, f.Color, f.Icon,
                !mine, rights.Write.Contains(f.Id), mine ? null : names.GetValueOrDefault(f.UserId));
        }).ToList());
    }

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] FolderRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "اسم المجلد مطلوب" });
        if (r.ParentId is { } pid && !(await FolderRightsAsync(ct)).Write.Contains(pid))
            return BadRequest(new { error = "المجلد الأعلى غير موجود أو لا تملك الإضافة إليه" });
        var f = new Folder { UserId = Uid, Name = r.Name.Trim(), ParentId = r.ParentId, Color = r.Color, Icon = r.Icon };
        db.Folders.Add(f); await db.SaveChangesAsync(ct);
        return Ok(new FolderDto(f.Id, f.Name, f.ParentId, 0, f.Color, f.Icon));
    }

    [HttpPut("folders/{id:long}")]
    public async Task<IActionResult> UpdateFolder(long id, [FromBody] FolderRequest r, CancellationToken ct)
    {
        var rights = await FolderRightsAsync(ct);
        var f = await db.Folders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null || !rights.Read.Contains(id)) return NotFound();
        if (!rights.Write.Contains(id)) return Forbid();
        if (r.ParentId is { } pid && pid != f.ParentId && !rights.Write.Contains(pid))
            return BadRequest(new { error = "المجلد الأعلى غير موجود أو لا تملك الإضافة إليه" });
        if (!string.IsNullOrWhiteSpace(r.Name)) f.Name = r.Name.Trim();
        if (r.ParentId != id) f.ParentId = r.ParentId;
        // Sent explicitly on every save, so a cleared choice reverts to the default.
        f.Color = r.Color;
        f.Icon = r.Icon;
        await db.SaveChangesAsync(ct);
        return Ok(new FolderDto(f.Id, f.Name, f.ParentId, await db.Documents.CountAsync(d => d.FolderId == f.Id, ct),
            f.Color, f.Icon));
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
        if (r.FolderId is { } fid && !(await FolderRightsAsync(ct)).Write.Contains(fid))
            return BadRequest(new { error = "المجلد غير موجود أو لا تملك الإضافة إليه" });
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

    // ---------------- Sharing folders and files ----------------
    //
    // Only the owner grants and revokes. Someone given edit rights can work inside the folder, not
    // hand it on: a chain of re-shares is how a personal drawer quietly becomes public, and the
    // person who filed the thing is the one who should be able to say who sees it.

    [HttpGet("folders/{id:long}/shares")]
    public async Task<IActionResult> FolderShares(long id, CancellationToken ct)
    {
        if (!await db.Folders.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        return Ok(await db.FolderShares.Where(s => s.FolderId == id)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new ItemShareDto(s.SharedWithUserId,
                db.Users.Where(u => u.Id == s.SharedWithUserId).Select(u => u.FullName).FirstOrDefault() ?? "—",
                s.CanEdit, s.CreatedAt))
            .ToListAsync(ct));
    }

    [HttpPost("folders/{id:long}/shares")]
    public async Task<IActionResult> ShareFolder(long id, [FromBody] CreateShareRequest r, CancellationToken ct)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.UserId == Uid, ct);
        if (folder is null) return NotFound();
        if (r.UserId == Uid) return BadRequest(new { error = "المجلد لك أصلاً" });
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId && u.IsActive, ct))
            return BadRequest(new { error = "المستخدم غير موجود" });

        var existing = await db.FolderShares.FirstOrDefaultAsync(s => s.FolderId == id && s.SharedWithUserId == r.UserId, ct);
        if (existing is null)
            db.FolderShares.Add(new FolderShare
            {
                FolderId = id, SharedWithUserId = r.UserId, SharedByUserId = Uid, CanEdit = r.CanEdit,
            });
        else existing.CanEdit = r.CanEdit;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FolderShared", "Folder", id, $"user={r.UserId} edit={r.CanEdit}", ct: ct);
        return Ok(new { shared = true });
    }

    [HttpDelete("folders/{id:long}/shares/{userId:long}")]
    public async Task<IActionResult> UnshareFolder(long id, long userId, CancellationToken ct)
    {
        if (!await db.Folders.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        var s = await db.FolderShares.FirstOrDefaultAsync(x => x.FolderId == id && x.SharedWithUserId == userId, ct);
        if (s is not null)
        {
            db.FolderShares.Remove(s);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("FolderUnshared", "Folder", id, $"user={userId}", ct: ct);
        }
        return NoContent();
    }

    [HttpGet("files/{id:long}/shares")]
    public async Task<IActionResult> FileShares(long id, CancellationToken ct)
    {
        if (!await db.UserFiles.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        return Ok(await db.UserFileShares.Where(s => s.UserFileId == id)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new ItemShareDto(s.SharedWithUserId,
                db.Users.Where(u => u.Id == s.SharedWithUserId).Select(u => u.FullName).FirstOrDefault() ?? "—",
                s.CanEdit, s.CreatedAt))
            .ToListAsync(ct));
    }

    [HttpPost("files/{id:long}/shares")]
    public async Task<IActionResult> ShareFile(long id, [FromBody] CreateShareRequest r, CancellationToken ct)
    {
        var file = await db.UserFiles.FirstOrDefaultAsync(f => f.Id == id && f.UserId == Uid, ct);
        if (file is null) return NotFound();
        if (r.UserId == Uid) return BadRequest(new { error = "الملف لك أصلاً" });
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId && u.IsActive, ct))
            return BadRequest(new { error = "المستخدم غير موجود" });

        var existing = await db.UserFileShares.FirstOrDefaultAsync(s => s.UserFileId == id && s.SharedWithUserId == r.UserId, ct);
        if (existing is null)
            db.UserFileShares.Add(new UserFileShare
            {
                UserFileId = id, SharedWithUserId = r.UserId, SharedByUserId = Uid, CanEdit = r.CanEdit,
            });
        else existing.CanEdit = r.CanEdit;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileShared", "UserFile", id, $"user={r.UserId} edit={r.CanEdit}", ct: ct);
        return Ok(new { shared = true });
    }

    [HttpDelete("files/{id:long}/shares/{userId:long}")]
    public async Task<IActionResult> UnshareFile(long id, long userId, CancellationToken ct)
    {
        if (!await db.UserFiles.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        var s = await db.UserFileShares.FirstOrDefaultAsync(x => x.UserFileId == id && x.SharedWithUserId == userId, ct);
        if (s is not null)
        {
            db.UserFileShares.Remove(s);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("FileUnshared", "UserFile", id, $"user={userId}", ct: ct);
        }
        return NoContent();
    }

    // ---------------- Sharing with everyone ----------------
    //
    // Rows, not a flag. A "shared with all" boolean would be one column and a dozen read paths to
    // teach about it — the register list, the scope filters, the archive reader, the export — and
    // any one of them missed would be a document that looks shared and is not, or the reverse.
    // One row per person keeps every existing check correct without touching it.

    /// <summary>Everyone this grant would reach: active accounts other than the caller's.</summary>
    private Task<List<long>> EveryoneElseAsync(CancellationToken ct) =>
        db.Users.Where(u => u.IsActive && u.Id != Uid).Select(u => u.Id).ToListAsync(ct);

    [HttpPost("folders/{id:long}/shares/everyone")]
    public async Task<IActionResult> ShareFolderWithEveryone(long id, [FromBody] ShareEveryoneRequest r, CancellationToken ct)
    {
        if (!await db.Folders.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();

        var everyone = await EveryoneElseAsync(ct);
        var already = await db.FolderShares.Where(s => s.FolderId == id).ToListAsync(ct);
        foreach (var s in already) s.CanEdit = r.CanEdit;
        db.FolderShares.AddRange(everyone.Except(already.Select(s => s.SharedWithUserId))
            .Select(uid => new FolderShare
            {
                FolderId = id, SharedWithUserId = uid, SharedByUserId = Uid, CanEdit = r.CanEdit,
            }));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FolderSharedWithAll", "Folder", id, $"count={everyone.Count} edit={r.CanEdit}", ct: ct);
        return Ok(new { shared = everyone.Count });
    }

    [HttpDelete("folders/{id:long}/shares/everyone")]
    public async Task<IActionResult> UnshareFolderFromEveryone(long id, CancellationToken ct)
    {
        if (!await db.Folders.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        var removed = await db.FolderShares.Where(s => s.FolderId == id).ExecuteDeleteAsync(ct);
        await audit.WriteAsync("FolderUnsharedFromAll", "Folder", id, $"count={removed}", ct: ct);
        return NoContent();
    }

    [HttpPost("files/{id:long}/shares/everyone")]
    public async Task<IActionResult> ShareFileWithEveryone(long id, [FromBody] ShareEveryoneRequest r, CancellationToken ct)
    {
        if (!await db.UserFiles.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();

        var everyone = await EveryoneElseAsync(ct);
        var already = await db.UserFileShares.Where(s => s.UserFileId == id).ToListAsync(ct);
        foreach (var s in already) s.CanEdit = r.CanEdit;
        db.UserFileShares.AddRange(everyone.Except(already.Select(s => s.SharedWithUserId))
            .Select(uid => new UserFileShare
            {
                UserFileId = id, SharedWithUserId = uid, SharedByUserId = Uid, CanEdit = r.CanEdit,
            }));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("FileSharedWithAll", "UserFile", id, $"count={everyone.Count} edit={r.CanEdit}", ct: ct);
        return Ok(new { shared = everyone.Count });
    }

    [HttpDelete("files/{id:long}/shares/everyone")]
    public async Task<IActionResult> UnshareFileFromEveryone(long id, CancellationToken ct)
    {
        if (!await db.UserFiles.AnyAsync(f => f.Id == id && f.UserId == Uid, ct)) return NotFound();
        var removed = await db.UserFileShares.Where(s => s.UserFileId == id).ExecuteDeleteAsync(ct);
        await audit.WriteAsync("FileUnsharedFromAll", "UserFile", id, $"count={removed}", ct: ct);
        return NoContent();
    }

    /// <summary>
    /// Open one document to the whole institution.
    ///
    /// <para>Heavier than it looks, and deliberately audited: a document share is checked before
    /// clearance and before the department scope, so this hands a سرّي document to every account
    /// that exists. The screen asks for a second click before it calls this.</para>
    /// </summary>
    [HttpPost("documents/{id:long}/shares/everyone")]
    public async Task<IActionResult> ShareDocumentWithEveryone(long id, [FromBody] ShareEveryoneRequest r, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return NotFound();
        if (!await CanSeeDocAsync(id, ct)) return Forbid();

        var everyone = await EveryoneElseAsync(ct);
        var already = await db.DocumentShares.Where(s => s.DocumentId == id).ToListAsync(ct);
        foreach (var s in already) s.CanEdit = r.CanEdit;
        db.DocumentShares.AddRange(everyone.Except(already.Select(s => s.SharedWithUserId))
            .Select(uid => new DocumentShare
            {
                DocumentId = id, SharedWithUserId = uid, SharedByUserId = Uid, CanEdit = r.CanEdit,
            }));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("SharedWithAll", "Document", id, $"count={everyone.Count} edit={r.CanEdit}", ct: ct);
        return Ok(new { shared = everyone.Count });
    }

    [HttpDelete("documents/{id:long}/shares/everyone")]
    public async Task<IActionResult> UnshareDocumentFromEveryone(long id, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return NotFound();
        if (!await CanSeeDocAsync(id, ct)) return Forbid();
        var removed = await db.DocumentShares.Where(s => s.DocumentId == id).ExecuteDeleteAsync(ct);
        await audit.WriteAsync("UnsharedFromAll", "Document", id, $"count={removed}", ct: ct);
        return NoContent();
    }

    // ---------------- Per-user table column layout ----------------
    [HttpGet("table-columns")]
    public async Task<IActionResult> TableColumns(CancellationToken ct)
    {
        var rows = await db.UserTablePrefs.Where(p => p.UserId == Uid)
            .Select(p => new { p.TableKey, p.ConfigJson }).ToListAsync(ct);
        return Ok(rows.ToDictionary(r => r.TableKey, r => r.ConfigJson));
    }

    [HttpPut("table-columns/{tableKey}")]
    [HasPermission("TableColumns.Edit")]
    public async Task<IActionResult> SaveTableColumns(string tableKey, [FromBody] TableColumnsRequest r, CancellationToken ct)
    {
        var p = await db.UserTablePrefs.FirstOrDefaultAsync(x => x.UserId == Uid && x.TableKey == tableKey, ct);
        if (p is null) { p = new UserTablePref { UserId = Uid, TableKey = tableKey }; db.UserTablePrefs.Add(p); }
        p.ConfigJson = r.ConfigJson ?? "";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
