using Archiving.Domain.Entities;

namespace Archiving.Application.Features.Metadata;

public sealed record RecordAgentDto(long Id, string AgentKind, long AgentId, string AgentName, string Role);

public sealed record RecordRelationshipDto(
    long Id, string SourceType, long SourceId, string TargetType, long TargetId, string Type, string? TargetTitle);

/// <summary>The business activity a record took part in (mapped to a workflow run).</summary>
public sealed record RecordActivityDto(long WorkflowInstanceId, string DefinitionName, string Status, DateTime StartedAt);

/// <summary>The ISO 23081 metadata graph for a record: its agents, relationships, and activities.</summary>
public sealed record RecordMetadataDto(
    long DocumentId,
    string DocumentNumber,
    string Title,
    IReadOnlyList<RecordAgentDto> Agents,
    IReadOnlyList<RecordRelationshipDto> Relationships,
    IReadOnlyList<RecordActivityDto> Activities);

public sealed record AddAgentRequest(AgentKind AgentKind, long AgentId, AgentRole Role);

public sealed record AddRelationshipRequest(
    string SourceType, long SourceId, string TargetType, long TargetId, RecordRelationshipType Type);
