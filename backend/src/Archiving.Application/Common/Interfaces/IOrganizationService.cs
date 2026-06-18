using Archiving.Application.Common.Models;
using Archiving.Application.Features.Organization;

namespace Archiving.Application.Common.Interfaces;

public interface IOrganizationService
{
    Task<IReadOnlyList<InstitutionDto>> ListInstitutionsAsync(CancellationToken ct = default);
    Task<Result<InstitutionDto>> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<OrgUnitDto>> ListOrgUnitsAsync(long? institutionId, CancellationToken ct = default);
    Task<Result<OrgUnitDto>> CreateOrgUnitAsync(CreateOrgUnitRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PositionDto>> ListPositionsAsync(long? orgUnitId, CancellationToken ct = default);
    Task<Result<PositionDto>> CreatePositionAsync(CreatePositionRequest request, CancellationToken ct = default);
    Task<Result<PositionDto>> AssignOccupantAsync(long positionId, AssignOccupantRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<UserLookupDto>> ListUsersAsync(CancellationToken ct = default);
}
