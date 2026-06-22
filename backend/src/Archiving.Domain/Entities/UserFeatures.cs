using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>A personal folder for organizing a user's own documents.</summary>
public class Folder : BaseEntity
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
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
    public Document Document { get; set; } = null!;
}

/// <summary>A note/comment attached to a specific document (visible to everyone who can see it).</summary>
public class DocumentNote : BaseEntity
{
    public long DocumentId { get; set; }
    public long UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Document Document { get; set; } = null!;
}
