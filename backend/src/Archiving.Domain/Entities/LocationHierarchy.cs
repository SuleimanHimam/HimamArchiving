using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

// Normalized physical-archive hierarchy (per the Physical Location Module spec):
// Building → Room → Cabinet (Treasury) → Shelf → Box → Document. Rooms also connect to
// each other (self-referencing many-to-many) via RoomConnection.

public class Building : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }       // unique
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}

public class Room : BaseEntity
{
    public long BuildingId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Building Building { get; set; } = null!;
    public ICollection<Cabinet> Cabinets { get; set; } = new List<Cabinet>();
}

/// <summary>A directed adjacency between two rooms. Connections are conceptually bidirectional;
/// we store ONE row per ordered direction and the service mirrors it (creates/deletes both
/// directions) so a connection always shows from either room.</summary>
public class RoomConnection : BaseEntity
{
    public long RoomId { get; set; }
    public long ConnectedRoomId { get; set; }
    public string? ConnectionType { get; set; }   // Door | Corridor | Internal Passage
    public string? Notes { get; set; }

    public Room Room { get; set; } = null!;
    public Room ConnectedRoom { get; set; } = null!;
}

public class Cabinet : BaseEntity   // الخزانة / Treasury
{
    public long RoomId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? CabinetCode { get; set; }
    public int ShelfCount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Room Room { get; set; } = null!;
    public ICollection<Shelf> Shelves { get; set; } = new List<Shelf>();
}

public class Shelf : BaseEntity   // الرف
{
    public long CabinetId { get; set; }
    public string ShelfNumber { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Cabinet Cabinet { get; set; } = null!;
    public ICollection<Box> Boxes { get; set; } = new List<Box>();
}

public class Box : BaseEntity   // الصندوق
{
    public long? ShelfId { get; set; }   // a box sits on a shelf …
    public long? RoomId { get; set; }    // … or directly in a room (3-level mode)
    public string BoxCode { get; set; } = string.Empty;   // unique
    public string? Barcode { get; set; }
    public int? Capacity { get; set; }
    public int CurrentCount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Shelf? Shelf { get; set; }
    public Room? Room { get; set; }
}
