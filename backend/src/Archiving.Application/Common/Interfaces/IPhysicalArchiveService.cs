using Archiving.Application.Common.Models;
using Archiving.Application.Features.Physical;

namespace Archiving.Application.Common.Interfaces;

public interface IPhysicalArchiveService
{
    Task<IReadOnlyList<PhysicalLocationDto>> ListLocationsAsync(CancellationToken ct = default);
    Task<Result<PhysicalLocationDto>> CreateLocationAsync(CreatePhysicalLocationRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PhysicalArchiveItemDto>> ListItemsAsync(long? locationId, CancellationToken ct = default);
    Task<Result<PhysicalArchiveItemDto>> CreateItemAsync(CreatePhysicalArchiveItemRequest request, CancellationToken ct = default);
}
