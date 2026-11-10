using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Delegation;

public sealed record CreateDelegationDto(
    long FromPositionId,
    long ToPositionId,
    DateTime StartDate,
    DateTime EndDate,
    DelegationScope Scope,
    long? WorkflowDefinitionId,
    string? Reason);

public sealed record DelegationDto(
    long Id,
    long FromPositionId,
    string? FromPositionTitle,
    long ToPositionId,
    string? ToPositionTitle,
    DateTime StartDate,
    DateTime EndDate,
    string Scope,
    long? WorkflowDefinitionId,
    bool IsActive,
    string? Reason);
