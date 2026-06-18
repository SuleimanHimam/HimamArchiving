using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Organization;

public sealed record InstitutionDto(long Id, string Name, string? NameEn, string? Code, bool IsActive);

public sealed record CreateInstitutionRequest(string Name, string? NameEn, string? Code);

public sealed record OrgUnitDto(
    long Id,
    long InstitutionId,
    long? ParentId,
    string Name,
    string? NameEn,
    string? Code,
    string Type,
    int SortOrder,
    bool IsActive);

public sealed record CreateOrgUnitRequest(
    long InstitutionId,
    long? ParentId,
    string Name,
    string? NameEn,
    string? Code,
    OrgUnitType Type,
    int SortOrder);

public sealed record PositionDto(
    long Id,
    string Title,
    string? Code,
    long OrgUnitId,
    string OrgUnitName,
    int Rank,
    long? CurrentOccupantUserId,
    string? CurrentOccupantName,
    bool IsActive);

public sealed record CreatePositionRequest(string Title, string? Code, long OrgUnitId, int Rank);

public sealed record AssignOccupantRequest(long UserId);

public sealed record UserLookupDto(long Id, string FullName, string Email);
