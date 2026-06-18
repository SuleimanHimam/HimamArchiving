namespace Archiving.Domain.Common;

/// <summary>Marker for entities that carry creation/modification audit stamps.</summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    long? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    long? UpdatedBy { get; set; }
}

/// <summary>Marker for entities that are never hard-deleted (kept for the permanent archive / audit).</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    long? DeletedBy { get; set; }
}

/// <summary>Base for all persisted entities. PKs are <see cref="long"/> for high-volume archives.</summary>
public abstract class BaseEntity : IAuditableEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}

/// <summary>Base for entities that participate in soft-delete (documents, mail, etc.).</summary>
public abstract class SoftDeleteEntity : BaseEntity, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }
}
