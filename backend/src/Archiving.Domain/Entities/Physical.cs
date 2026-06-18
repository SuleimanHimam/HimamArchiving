using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Self-referencing physical location tree: Building → Room → Cabinet → Shelf → Box.</summary>
public class PhysicalLocation : BaseEntity
{
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PhysicalLocationType Type { get; set; }
    public string? Code { get; set; }
    public string? RfidTag { get; set; }                 // Phase 2 — RFID tracking
    public bool IsActive { get; set; } = true;

    public PhysicalLocation? Parent { get; set; }
    public ICollection<PhysicalLocation> Children { get; set; } = new List<PhysicalLocation>();
}

/// <summary>Maps a digital record (document or incoming mail) to its paper location.</summary>
public class PhysicalArchiveItem : BaseEntity
{
    public long? DocumentId { get; set; }
    public long? IncomingMailId { get; set; }
    public long PhysicalLocationId { get; set; }
    public string? BoxNumber { get; set; }
    public string? FileNumber { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    public long? ArchivedByUserId { get; set; }
    public string? Notes { get; set; }

    public Document? Document { get; set; }
    public IncomingMail? IncomingMail { get; set; }
    public PhysicalLocation PhysicalLocation { get; set; } = null!;
}
