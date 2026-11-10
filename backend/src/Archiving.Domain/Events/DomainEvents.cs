namespace Archiving.Domain.Events;

// All cross-module domain events. Ids are `long` to match the codebase's PK type (BaseEntity.Id).
// Each is a thin, immutable record carrying just enough for handlers to load what they need.

// ---- Document events ----
public sealed record DocumentCreatedEvent(long DocumentId, long? DocumentTypeId, long? CategoryId, long? CreatedByUserId) : DomainEventBase;
public sealed record DocumentClassifiedEvent(long DocumentId, long? CategoryId, string? ClassificationLevel) : DomainEventBase;
public sealed record DocumentStatusChangedEvent(long DocumentId, string OldStatus, string NewStatus) : DomainEventBase;
public sealed record DocumentArchivedEvent(long DocumentId, long? PhysicalLocationBoxId) : DomainEventBase;
public sealed record DocumentSharedEvent(long DocumentId, long SharedWithUserId, string PermissionLevel) : DomainEventBase;
public sealed record DocumentMovedEvent(long DocumentId, long? OldBoxId, long? NewBoxId) : DomainEventBase;

// ---- Attachment events ----
public sealed record AttachmentUploadedEvent(long AttachmentId, long DocumentId, string FileName, string? ContentType) : DomainEventBase;

// ---- Workflow events ----
public sealed record WorkflowInstanceCreatedEvent(long InstanceId, long DefinitionId, long? DocumentId) : DomainEventBase;
public sealed record WorkflowStageCompletedEvent(long InstanceId, long StageId, string Action, long ActedByPositionId) : DomainEventBase;
public sealed record WorkflowStageAssignedEvent(long InstanceId, long StageId, long AssignedToPositionId, DateTime? DueDate) : DomainEventBase;
public sealed record WorkflowApprovedEvent(long InstanceId, long? DocumentId) : DomainEventBase;        // final stage approved
public sealed record WorkflowRejectedEvent(long InstanceId, long? DocumentId, string? Reason) : DomainEventBase;
public sealed record WorkflowSLABreachedEvent(long InstanceId, long StageId, long AssignedToPositionId, DateTime DueDate) : DomainEventBase;

// ---- Disposition events ----
public sealed record DispositionRequestCreatedEvent(long RequestId, long DocumentId) : DomainEventBase;
public sealed record DispositionVerifiedEvent(long RequestId) : DomainEventBase;
public sealed record DispositionApprovedEvent(long RequestId, long DocumentId, long? PhysicalBoxId) : DomainEventBase;
public sealed record DispositionRejectedEvent(long RequestId, string? Reason) : DomainEventBase;
public sealed record RetentionExpiringEvent(long DocumentId, int DaysRemaining) : DomainEventBase;

// ---- Organization events ----
public sealed record PositionOccupantChangedEvent(long PositionId, long? OldUserId, long? NewUserId) : DomainEventBase;
