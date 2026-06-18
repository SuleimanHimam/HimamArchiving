using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Physical;

public sealed record PhysicalLocationDto(
    long Id,
    long? ParentId,
    string Name,
    string Type,
    string? Code,
    string? RfidTag,
    bool IsActive);

public sealed record CreatePhysicalLocationRequest(
    long? ParentId,
    string Name,
    PhysicalLocationType Type,
    string? Code,
    string? RfidTag);

public sealed record PhysicalArchiveItemDto(
    long Id,
    long? DocumentId,
    long? IncomingMailId,
    long PhysicalLocationId,
    string LocationName,
    string? BoxNumber,
    string? FileNumber,
    DateTime ArchivedAt,
    string? Notes);

public sealed record CreatePhysicalArchiveItemRequest(
    long? DocumentId,
    long? IncomingMailId,
    long PhysicalLocationId,
    string? BoxNumber,
    string? FileNumber,
    string? Notes);
