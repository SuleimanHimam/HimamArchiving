using Archiving.Application.Common.Models;
using Archiving.Application.Features.Locations;

namespace Archiving.Application.Common.Interfaces;

/// <summary>CRUD + tree/breadcrumb/code for the normalized physical-location hierarchy
/// (Building → Room → Cabinet → Shelf → Box) and room-to-room connections.</summary>
public interface ILocationService
{
    // Buildings
    Task<IReadOnlyList<BuildingDto>> ListBuildingsAsync(CancellationToken ct = default);
    Task<Result<BuildingDto>> CreateBuildingAsync(BuildingRequest r, CancellationToken ct = default);
    Task<Result<BuildingDto>> UpdateBuildingAsync(long id, BuildingRequest r, CancellationToken ct = default);
    Task<Result<bool>> DeleteBuildingAsync(long id, CancellationToken ct = default);

    // Rooms
    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(long? buildingId, CancellationToken ct = default);
    Task<Result<RoomDto>> CreateRoomAsync(RoomRequest r, CancellationToken ct = default);
    Task<Result<RoomDto>> UpdateRoomAsync(long id, RoomRequest r, CancellationToken ct = default);
    Task<Result<bool>> DeleteRoomAsync(long id, CancellationToken ct = default);

    // Room connections
    Task<IReadOnlyList<RoomConnectionDto>> ListConnectionsAsync(long roomId, CancellationToken ct = default);
    Task<Result<RoomConnectionDto>> AddConnectionAsync(long roomId, RoomConnectionRequest r, CancellationToken ct = default);
    Task<Result<bool>> RemoveConnectionAsync(long roomId, long connectionId, CancellationToken ct = default);

    // Cabinets
    Task<IReadOnlyList<CabinetDto>> ListCabinetsAsync(long? roomId, CancellationToken ct = default);
    Task<Result<CabinetDto>> CreateCabinetAsync(CabinetRequest r, CancellationToken ct = default);
    Task<Result<CabinetDto>> UpdateCabinetAsync(long id, CabinetRequest r, CancellationToken ct = default);
    Task<Result<bool>> DeleteCabinetAsync(long id, CancellationToken ct = default);

    // Shelves
    Task<IReadOnlyList<ShelfDto>> ListShelvesAsync(long? cabinetId, CancellationToken ct = default);
    Task<Result<ShelfDto>> CreateShelfAsync(ShelfRequest r, CancellationToken ct = default);
    Task<Result<ShelfDto>> UpdateShelfAsync(long id, ShelfRequest r, CancellationToken ct = default);
    Task<Result<bool>> DeleteShelfAsync(long id, CancellationToken ct = default);

    // Boxes
    Task<IReadOnlyList<BoxDto>> ListBoxesAsync(long? shelfId, long? roomId, CancellationToken ct = default);
    Task<Result<BoxDto>> CreateBoxAsync(BoxRequest r, CancellationToken ct = default);
    Task<Result<BoxDto>> UpdateBoxAsync(long id, BoxRequest r, CancellationToken ct = default);
    Task<Result<bool>> DeleteBoxAsync(long id, CancellationToken ct = default);

    // Tree + breadcrumb + label code
    Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(CancellationToken ct = default);
    Task<Result<BreadcrumbDto>> GetBreadcrumbAsync(long boxId, CancellationToken ct = default);
    Task<Result<LocationAncestryDto>> GetBoxAncestryAsync(long boxId, CancellationToken ct = default);

    /// <summary>One-shot, best-effort migration of the legacy single-table PhysicalLocation tree +
    /// PhysicalArchiveItem links into the normalized model. Idempotent (matches by name/type).</summary>
    Task<Result<string>> MigrateLegacyAsync(CancellationToken ct = default);
}
