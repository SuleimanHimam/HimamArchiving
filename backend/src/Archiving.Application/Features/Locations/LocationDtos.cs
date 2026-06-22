namespace Archiving.Application.Features.Locations;

// ---- Building ----
public sealed record BuildingDto(long Id, string NameAr, string? NameEn, string? Code, string? Address, string? Notes, bool IsActive, int RoomCount);
public sealed record BuildingRequest(string NameAr, string? NameEn, string? Code, string? Address, string? Notes, bool IsActive = true);

// ---- Room ----
public sealed record RoomDto(long Id, long BuildingId, string BuildingName, string NameAr, string? NameEn, string? RoomNumber, string? Floor, string? Notes, bool IsActive, int CabinetCount);
public sealed record RoomRequest(long BuildingId, string NameAr, string? NameEn, string? RoomNumber, string? Floor, string? Notes, bool IsActive = true);

// ---- Room connections ----
public sealed record RoomConnectionDto(long Id, long RoomId, long ConnectedRoomId, string ConnectedRoomName, string? ConnectionType, string? Notes);
public sealed record RoomConnectionRequest(long ConnectedRoomId, string? ConnectionType, string? Notes);

// ---- Cabinet ----
public sealed record CabinetDto(long Id, long RoomId, string RoomName, string NameAr, string? NameEn, string? CabinetCode, int ShelfCount, string? Notes, bool IsActive, int ShelvesActual);
public sealed record CabinetRequest(long RoomId, string NameAr, string? NameEn, string? CabinetCode, int ShelfCount, string? Notes, bool IsActive = true);

// ---- Shelf ----
public sealed record ShelfDto(long Id, long CabinetId, string CabinetName, string ShelfNumber, int? Capacity, string? Notes, bool IsActive, int BoxCount);
public sealed record ShelfRequest(long CabinetId, string ShelfNumber, int? Capacity, string? Notes, bool IsActive = true);

// ---- Box ----
public sealed record BoxDto(long Id, long? ShelfId, long? RoomId, string BoxCode, string? Barcode, int? Capacity, int CurrentCount, bool IsFull, string? Notes, bool IsActive);
public sealed record BoxRequest(long? ShelfId, long? RoomId, string BoxCode, string? Barcode, int? Capacity, string? Notes, bool IsActive = true);

// ---- Tree + breadcrumb ----
public sealed record LocationTreeNode(long Id, string Type, string Name, string? Code, IReadOnlyList<LocationTreeNode> Children);
public sealed record BreadcrumbDto(long BoxId, string Path, string LocationCode, IReadOnlyList<string> Parts);

/// <summary>The chain of parent ids above a box, used to pre-fill the cascading location picker.</summary>
public sealed record LocationAncestryDto(long BoxId, long? ShelfId, long? CabinetId, long? RoomId, long? BuildingId);
