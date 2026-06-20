using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>A personal folder for organizing a user's own documents.</summary>
public class Folder : BaseEntity
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
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

/// <summary>A per-user notepad note.</summary>
public class UserNote : BaseEntity
{
    public long UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
