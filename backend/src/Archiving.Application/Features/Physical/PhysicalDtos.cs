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

public sealed record UpdatePhysicalLocationRequest(
    long? ParentId,
    string Name,
    PhysicalLocationType Type,
    string? Code,
    string? RfidTag,
    bool IsActive);

/// <param name="Source">"Legacy" for a PhysicalArchiveItem row, "Box" for a document filed straight
/// into the normalized Building→…→Box hierarchy (Document.BoxId). "Box" rows have no archive-item row
/// behind them, so <paramref name="Id"/> is 0 and they are edited by re-filing the document.</param>
public sealed record PhysicalArchiveItemDto(
    long Id,
    long? DocumentId,
    long PhysicalLocationId,
    string LocationName,
    string? BoxNumber,
    string? FileNumber,
    DateTime ArchivedAt,
    string? Notes,
    string? DocumentNumber = null,
    string? DocumentTitle = null,
    string Source = "Legacy");

public sealed record CreatePhysicalArchiveItemRequest(
    long? DocumentId,
    long PhysicalLocationId,
    string? BoxNumber,
    string? FileNumber,
    string? Notes);

public sealed record UpdatePhysicalArchiveItemRequest(
    long PhysicalLocationId,
    string? BoxNumber,
    string? FileNumber,
    string? Notes);
