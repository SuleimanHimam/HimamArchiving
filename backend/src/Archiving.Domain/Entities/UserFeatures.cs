using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>A personal folder for organizing a user's own documents.</summary>
public class Folder : BaseEntity
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    /// <summary>Folder tint, as one of the palette keys the client offers (blue, red, …).
    /// Null keeps the default blue, so folders created before this stay unchanged.</summary>
    public string? Color { get; set; }
    /// <summary>Optional symbol shown on the folder face, as a client-side icon key.</summary>
    public string? Icon { get; set; }
}

/// <summary>
/// A plain file kept in the archive without being a document.
///
/// <para>Not everything a department stores is a record. A working spreadsheet, a photo of a
/// whiteboard, an installer — these need a name, a folder and a safe place to live, and nothing
/// else. Forcing them through <see cref="Document"/> would mint a document number, demand a type
/// and a confidentiality level, and put them on a retention schedule that means nothing for them,
/// which is how registers fill up with rows nobody can dispose of.</para>
///
/// <para>The bytes are stored exactly the way an attachment's are — same encrypted object store,
/// same key handling. Only the metadata around them is lighter.</para>
/// </summary>
public class UserFile : SoftDeleteEntity
{
    public long UserId { get; set; }
    /// <summary>Display name including the extension, as the uploader had it.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Null means it sits at the root, outside every folder.</summary>
    public long? FolderId { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    /// <summary>Set once the content has been crypto-shredded; the row survives as a record of
    /// what was here, the same way a tombstoned document does.</summary>
    public bool ContentDestroyed { get; set; }
}

/// <summary>Per-user column layout (rename + show/hide + order) for one data table.
/// ConfigJson is an opaque JSON array of { key, label?, hidden? } owned by the frontend.</summary>
public class UserTablePref : BaseEntity
{
    public long UserId { get; set; }
    public string TableKey { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
}

/// <summary>A document a user has marked as favorite (quick access).</summary>
public class DocumentFavorite : BaseEntity
{
    public long UserId { get; set; }
    public long DocumentId { get; set; }
    public Document Document { get; set; } = null!;
}

/// <summary>A document explicitly shared with another user (view, or view+edit).</summary>
public class DocumentShare : BaseEntity
{
    public long DocumentId { get; set; }
    public long SharedWithUserId { get; set; }
    public long SharedByUserId { get; set; }
    public bool CanEdit { get; set; }
    public bool IsRead { get; set; }                 // has the recipient opened it (drives the unified worklist) — Phase 4
    public DateTime? ReadAt { get; set; }
    public Document Document { get; set; } = null!;
}

/// <summary>
/// A personal folder opened up to another user.
///
/// <para>The grant reaches everything beneath the folder — its files and its sub-folders and
/// their files — because that is what sharing a folder means; a grant that stopped at the first
/// level would be a list of permissions wearing a folder's name.</para>
///
/// <para>It does NOT reach the <see cref="Document"/>s filed into the folder. A folder is a
/// personal filing device; a document belongs to a department and carries its own confidentiality,
/// and letting a folder share hand it over would route around the rule that decides who may read
/// it. Documents are shared as documents — see <see cref="DocumentShare"/>.</para>
/// </summary>
public class FolderShare : BaseEntity
{
    public long FolderId { get; set; }
    public long SharedWithUserId { get; set; }
    public long SharedByUserId { get; set; }
    /// <summary>Editors rename, recolour, and put things into the folder. Emptying it stays with
    /// the owner: help with someone's filing is not authority to undo it.</summary>
    public bool CanEdit { get; set; }
    public Folder Folder { get; set; } = null!;
}

/// <summary>One plain file opened up to another user, independently of the folder it sits in —
/// the ordinary case of "here, look at this one thing" without opening the drawer around it.</summary>
public class UserFileShare : BaseEntity
{
    public long UserFileId { get; set; }
    public long SharedWithUserId { get; set; }
    public long SharedByUserId { get; set; }
    /// <summary>Editors rename it and move it; only the owner deletes it.</summary>
    public bool CanEdit { get; set; }
    public UserFile File { get; set; } = null!;
}

/// <summary>A note/comment attached to a specific document (visible to everyone who can see it).</summary>
public class DocumentNote : BaseEntity
{
    public long DocumentId { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Document Document { get; set; } = null!;
}
