# Claude Code Implementation Prompt — Unified Workflow System Upgrade

> **Purpose:** Give this entire file to Claude Code as a prompt. It transforms the existing Document Management & Archiving system from isolated modules into a fully connected, event-driven workflow platform.
>
> **Important:** Work through each phase sequentially. Do NOT skip ahead. Each phase builds on the previous one. After completing each phase, run `dotnet build` and `dotnet test` to confirm nothing is broken before moving on.

---

## System Context

You are working on an ASP.NET Core Web API (.NET 10) project with this structure:

```
backend/
  Archiving.slnx
  src/
    Archiving.Domain/          # entities + enums
    Archiving.Application/     # DTOs, service interfaces, result/paging models
    Archiving.Infrastructure/  # EF Core DbContext + migrations, MySQL, JWT, storage, services
    Archiving.Api/             # controllers, DI, middleware, RBAC policies
frontend/                      # React SPA (RTL Arabic, Vite, TypeScript)
```

Stack: ASP.NET Core (.NET 10), EF Core 9 + Pomelo, MySQL 8 (utf8mb4), JWT + RBAC auth.

Existing modules already implemented and working:
- Documents (CRUD, versioning, attachments, OCR full-text search, clearance-gated)
- Incoming Mail (register, route/forward, lifecycle actions, timeline)
- Outgoing Mail (draft → submit → approve → send, auto-archive, official numbering)
- Workflow engine (admin-defined definitions/stages, instances, position-based worklist, act with stage advancement + SLA)
- Notifications (in-app feed, unread count)
- Physical Locations (Building → Room → Cabinet → Shelf → Box hierarchy, location picker)
- Retention & Disposition (two-step verification, legal hold, destruction certificate)
- Inter-department Correspondence (auto-generated memos, per-department delivery + acknowledge)
- Organization (institutions, org-unit tree, positions, occupants)
- ISO compliance (fixity, audit chain, PDF/A, OAIS packages, metadata, preservation policy)
- Favorites, Folders, Sharing, Notepad, Export/ZIP, Encryption at rest

**Do NOT modify or break any existing functionality.** All changes are additive — extending the system, not rewriting it.

---

## PHASE 1 — Domain Event Bus (MediatR INotification)

**Goal:** Create an in-process domain event system so any module can publish events and any other module can react — without direct service-to-service coupling.

### Step 1.1 — Install MediatR

Add the `MediatR` NuGet package to `Archiving.Application` and `Archiving.Infrastructure`. Register MediatR in DI in `Archiving.Api/Program.cs` (or wherever DI is configured).

### Step 1.2 — Base event infrastructure

In `Archiving.Domain`, create:

```
Archiving.Domain/
  Events/
    IDomainEvent.cs              # marker interface extending MediatR.INotification
    DomainEventBase.cs           # abstract base: Guid EventId, DateTime OccurredAt, string TriggeredByUserId
```

In `Archiving.Domain/Common/` (or wherever your base entity lives), add a `List<IDomainEvent> DomainEvents` collection to the base `Entity` class with `AddDomainEvent()` and `ClearDomainEvents()` methods.

### Step 1.3 — Event dispatcher middleware

In `Archiving.Infrastructure`, create a `DomainEventDispatcher` that hooks into EF Core's `SaveChangesAsync`. Before calling `base.SaveChangesAsync()`, collect all domain events from tracked entities, clear them from the entities, then after save succeeds, publish each event via `IMediator.Publish()`. This ensures events only fire after successful persistence.

### Step 1.4 — Define all domain events

Create these event classes in `Archiving.Domain/Events/`:

```csharp
// Mail events
IncomingMailReceivedEvent          { int MailId, int InstitutionId, int? DocumentTypeId }
IncomingMailRoutedEvent            { int MailId, int FromPositionId, int ToPositionId }
OutgoingMailApprovedEvent          { int MailId, int? LinkedDocumentId }
OutgoingMailSentEvent              { int MailId }

// Document events
DocumentCreatedEvent              { int DocumentId, int DocumentTypeId, int CategoryId, int CreatedByUserId }
DocumentClassifiedEvent           { int DocumentId, int CategoryId, string ClassificationLevel }
DocumentStatusChangedEvent        { int DocumentId, string OldStatus, string NewStatus }
DocumentArchivedEvent             { int DocumentId, int? PhysicalLocationBoxId }
DocumentSharedEvent               { int DocumentId, int SharedWithUserId, string PermissionLevel }
DocumentMovedEvent                { int DocumentId, int? OldBoxId, int? NewBoxId }

// Attachment events
AttachmentUploadedEvent           { int AttachmentId, int DocumentId, string FileName, string ContentType }

// Workflow events
WorkflowInstanceCreatedEvent      { int InstanceId, int DefinitionId, int DocumentId }
WorkflowStageCompletedEvent       { int InstanceId, int StageId, string Action, int ActedByPositionId }
WorkflowStageAssignedEvent        { int InstanceId, int StageId, int AssignedToPositionId, DateTime? DueDate }
WorkflowApprovedEvent             { int InstanceId, int DocumentId }    // final stage approved
WorkflowRejectedEvent             { int InstanceId, int DocumentId, string Reason }
WorkflowSLABreachedEvent          { int InstanceId, int StageId, int AssignedToPositionId, DateTime DueDate }

// Disposition events
DispositionRequestCreatedEvent    { int RequestId, int DocumentId }
DispositionVerifiedEvent          { int RequestId }
DispositionApprovedEvent          { int RequestId, int DocumentId, int? PhysicalBoxId }
DispositionRejectedEvent          { int RequestId, string Reason }
RetentionExpiringEvent            { int DocumentId, int DaysRemaining }

// Correspondence events
CorrespondenceSentEvent           { int CorrespondenceId, int FromDepartmentId, int ToDepartmentId }
CorrespondenceAcknowledgedEvent   { int CorrespondenceId }
CorrespondenceOverdueEvent        { int CorrespondenceId, int ToDepartmentId, int DaysOverdue }

// Organization events
PositionOccupantChangedEvent      { int PositionId, int? OldUserId, int? NewUserId }
```

### Step 1.5 — Publish events from existing services

Go through each existing service and add `entity.AddDomainEvent(new XxxEvent(...))` at the appropriate points. Do NOT change any existing business logic — only add event publishing. Key places:

- `IncomingMailService` → after creating a new mail entry, add `IncomingMailReceivedEvent`
- `DocumentService` → after Create, add `DocumentCreatedEvent`; after status change, add `DocumentStatusChangedEvent`
- `AttachmentService` → after upload, add `AttachmentUploadedEvent`
- `WorkflowService` → after stage completion, add `WorkflowStageCompletedEvent`; after final approval, add `WorkflowApprovedEvent`
- `DispositionService` → after approval, add `DispositionApprovedEvent`
- `CorrespondenceService` → after sending, add `CorrespondenceSentEvent`

### Step 1.6 — Event log table

Create an `EventLog` entity and migration:

```
EventLog: Id, EventType (string), EventData (JSON), TriggeredByUserId, OccurredAt, ProcessedAt (nullable)
```

Create a generic `EventLogHandler<T> : INotificationHandler<T>` that serializes every domain event to this table. This gives you a complete event audit trail.

### Step 1.7 — Verify

Run `dotnet build` and `dotnet test`. Manually test: create an incoming mail via API → confirm an `IncomingMailReceivedEvent` row appears in `EventLog`. No existing behavior should change.

---

## PHASE 2 — Cross-Module Event Handlers (Connecting the Modules)

**Goal:** Write handlers that react to domain events and trigger actions in other modules — this is what makes the system a connected workflow.

Create these handlers in `Archiving.Infrastructure/EventHandlers/`:

### Step 2.1 — Incoming mail → auto-create document

```
Handler: IncomingMailReceivedHandler
Trigger: IncomingMailReceivedEvent
Action:
  1. Read the incoming mail entity
  2. Create a new Document with:
     - Title = mail subject
     - DocumentTypeId = mail's document type (or a default "Incoming Correspondence" type)
     - Status = "Draft"
     - Link back to the incoming mail (add a LinkedMailId field to Document if needed — create migration)
     - Copy relevant metadata (sender, date, reference number)
  3. Publish DocumentCreatedEvent
```

### Step 2.2 — Document created → start classification workflow

```
Handler: DocumentCreatedHandler
Trigger: DocumentCreatedEvent
Action:
  1. Look up the document type → find if there's a default WorkflowDefinition linked to that type
     (add a DefaultWorkflowDefinitionId nullable FK to DocumentType if not present — create migration)
  2. If a default workflow exists, auto-create a WorkflowInstance for this document
  3. Assign the first stage to the appropriate position
  4. Create a notification for the assigned position holder
  5. Start the retention clock: if the category has a retention policy, record the retention start date
```

### Step 2.3 — Document created → reserve physical location

```
Handler: DocumentPhysicalLocationHandler
Trigger: DocumentCreatedEvent
Action:
  1. Check if the document type requires physical archiving (add a RequiresPhysicalArchiving bool to DocumentType if missing)
  2. If yes, find the department's designated archive room/cabinet
  3. Find the first available box with capacity
  4. Reserve a slot (don't assign yet — mark as "Pending")
  5. Add the location to the document's physical location fields
```

### Step 2.4 — Workflow stage completed → generate correspondence

```
Handler: WorkflowStageCorrespondenceHandler
Trigger: WorkflowStageCompletedEvent
Action:
  1. Check if the completed stage has RequiresCorrespondence = true (add this bool to WorkflowStage if missing)
  2. If the next stage is assigned to a different department than the current one:
     - Auto-generate an inter-department correspondence memo
     - Link it to the document and workflow instance
```

### Step 2.5 — Workflow final approval → generate outgoing mail + archive

```
Handler: WorkflowFinalApprovalHandler
Trigger: WorkflowApprovedEvent
Action:
  1. Load the document
  2. If the document type requires an outgoing response:
     - Create an OutgoingMail draft linked to this document
     - Pre-fill from the incoming mail's sender info (if linked)
  3. Change document status to "Active" or "Archived"
  4. Confirm the physical location slot (change from "Pending" to "Assigned")
  5. Publish DocumentArchivedEvent
```

### Step 2.6 — Workflow rejection → notify + return

```
Handler: WorkflowRejectionHandler
Trigger: WorkflowRejectedEvent
Action:
  1. Create a notification for the document's creator
  2. If the document was linked to an incoming mail, create a return note on the mail
  3. Change document status back to "Draft" or "Returned"
```

### Step 2.7 — Disposition approved → release physical slot + notify shared users

```
Handler: DispositionCompletionHandler
Trigger: DispositionApprovedEvent
Action:
  1. If the document has a physical box, release the slot (decrement box usage)
  2. Notify all users who had this document shared with them
  3. Update correspondence records linked to this document
```

### Step 2.8 — Attachment uploaded → queue processing pipeline

```
Handler: AttachmentProcessingPipelineHandler
Trigger: AttachmentUploadedEvent
Action:
  1. Queue text extraction (OCR or PDF text) — already exists, hook it to this event
  2. Queue fixity hash computation — already exists, hook it
  3. Queue PDF/A normalization if it's a scan — already exists, hook it
  4. These should run as a chained sequence, not three independent jobs
```

### Step 2.9 — Correspondence overdue → escalate to supervisor

```
Handler: CorrespondenceEscalationHandler
Trigger: CorrespondenceOverdueEvent
Action:
  1. Look up the recipient department's head position
  2. Create a notification for the department head
  3. If still overdue after another N days, escalate to the institution admin
```

### Step 2.10 — Position occupant changed → reassign open tasks

```
Handler: PositionReassignmentHandler
Trigger: PositionOccupantChangedEvent
Action:
  1. Find all open workflow stage assignments for this position
  2. If a new occupant exists, create notifications for them
  3. If no new occupant, check for active delegation (Phase 5) or escalate
```

**Verify:** `dotnet build && dotnet test`. Then end-to-end test: create an incoming mail → confirm a document is auto-created → confirm a workflow starts (if configured) → approve the workflow → confirm outgoing mail is generated and document is archived.

---

## PHASE 3 — Document Lifecycle State Machine

**Goal:** Enforce valid state transitions at the domain level so no API call or event handler can put a document into an invalid state.

### Step 3.1 — Define the state machine

In `Archiving.Domain`, create:

```csharp
// Archiving.Domain/StateMachine/DocumentLifecycle.cs

public enum DocumentStatus
{
    Draft,
    UnderReview,
    Classified,
    Active,
    Archived,
    UnderDisposition,
    Destroyed,
    Transferred,
    Returned
}

public static class DocumentLifecycle
{
    // Define allowed transitions: (FromStatus, ToStatus) → required permission
    private static readonly Dictionary<(DocumentStatus, DocumentStatus), string> AllowedTransitions = new()
    {
        { (DocumentStatus.Draft, DocumentStatus.UnderReview), "Documents.Submit" },
        { (DocumentStatus.UnderReview, DocumentStatus.Classified), "Documents.Classify" },
        { (DocumentStatus.UnderReview, DocumentStatus.Returned), "Documents.Return" },
        { (DocumentStatus.Classified, DocumentStatus.Active), "Documents.Activate" },
        { (DocumentStatus.Active, DocumentStatus.Archived), "Documents.Archive" },
        { (DocumentStatus.Active, DocumentStatus.Transferred), "Documents.Transfer" },
        { (DocumentStatus.Archived, DocumentStatus.UnderDisposition), "Disposition.Create" },
        { (DocumentStatus.Archived, DocumentStatus.Active), "Documents.Reactivate" },
        { (DocumentStatus.UnderDisposition, DocumentStatus.Destroyed), "Disposition.Approve" },
        { (DocumentStatus.UnderDisposition, DocumentStatus.Archived), "Disposition.Reject" },
        { (DocumentStatus.Returned, DocumentStatus.Draft), "Documents.Edit" },
    };

    public static bool CanTransition(DocumentStatus from, DocumentStatus to) =>
        AllowedTransitions.ContainsKey((from, to));

    public static string? GetRequiredPermission(DocumentStatus from, DocumentStatus to) =>
        AllowedTransitions.TryGetValue((from, to), out var perm) ? perm : null;

    public static List<DocumentStatus> GetAllowedNextStatuses(DocumentStatus current) =>
        AllowedTransitions.Keys.Where(k => k.Item1 == current).Select(k => k.Item2).ToList();
}
```

### Step 3.2 — Enforce in the Document entity

Add a `TransitionTo(DocumentStatus newStatus)` method on the `Document` entity that:
1. Calls `DocumentLifecycle.CanTransition(this.Status, newStatus)`
2. Throws a `DomainException` if the transition is invalid
3. Publishes a `DocumentStatusChangedEvent` with old and new status

### Step 3.3 — Precondition checks

Before certain transitions, validate preconditions:
- `Active → Archived`: document MUST have a physical location assigned (if type requires it)
- `Archived → UnderDisposition`: retention period MUST have expired (or manual override with permission)
- `UnderDisposition → Destroyed`: disposition request MUST be approved by both verification and final approval steps

### Step 3.4 — API endpoint

Add `GET /api/documents/{id}/allowed-transitions` that returns the list of valid next statuses for the current document, filtered by the calling user's permissions. The frontend can use this to show only valid action buttons.

### Step 3.5 — Migration

If the existing `Status` field is a string, migrate it to use the new enum (store as string in DB for readability, but map to the enum in EF Core). Ensure all existing documents map to a valid status value.

---

## PHASE 4 — Unified Task Queue (Worklist)

**Goal:** Aggregate all pending actions across all modules into one "my tasks" endpoint.

### Step 4.1 — Create the unified task model

```csharp
// Archiving.Application/DTOs/UnifiedTaskDto.cs

public class UnifiedTaskDto
{
    public string TaskId { get; set; }        // e.g., "workflow-123", "mail-456", "disposition-789"
    public string TaskType { get; set; }       // "WorkflowStage", "IncomingMail", "DispositionReview",
                                               // "CorrespondenceResponse", "DocumentReview", "SharedDocument"
    public string Title { get; set; }          // human-readable: "مراجعة وثيقة — عقد توريد"
    public string? Description { get; set; }
    public string Priority { get; set; }       // "High", "Normal", "Low"
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public int? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string SourceModule { get; set; }   // "Workflow", "Mail", "Disposition", "Correspondence", "Documents"
    public List<string> AvailableActions { get; set; } // ["approve", "reject", "forward"]
    public string? AssignedVia { get; set; }   // "Direct", "Delegation", "Escalation"
}
```

### Step 4.2 — Unified task service

Create `IUnifiedTaskService` in Application and implement in Infrastructure:

```csharp
public interface IUnifiedTaskService
{
    Task<PagedResult<UnifiedTaskDto>> GetMyTasksAsync(
        int userId,
        UnifiedTaskFilter? filter,    // filter by TaskType, Priority, IsOverdue, SourceModule, date range
        string? sortBy,               // "dueDate", "createdAt", "priority"
        int page, int pageSize);

    Task<TaskCountsDto> GetMyTaskCountsAsync(int userId);  // counts per TaskType + overdue count
}
```

Implementation queries across:
1. **Workflow stages** assigned to the user's position(s) — already exists in `my-tasks`
2. **Incoming mail** pending action by the user's position
3. **Disposition requests** pending the user's review/approval
4. **Correspondence** delivered to the user's department awaiting acknowledgment
5. **Documents** shared with the user that are unread (add an `IsRead` bool to the sharing table)
6. **Documents** where the user is the creator and the document has status "Returned"

Union these into one list, sorted by due date (overdue first), then priority, then created date.

### Step 4.3 — API endpoints

```
GET  /api/tasks                    → paginated unified task list (filters in query string)
GET  /api/tasks/counts             → { total, overdue, byType: { workflow: 5, mail: 3, ... } }
GET  /api/tasks/{taskType}/{id}    → detail for a specific task (redirects to the right module)
```

### Step 4.4 — Add IsRead tracking for shared documents

Add migration: `DocumentShare` table gets `IsRead bool default false`, `ReadAt DateTime?`. When a user opens a shared document, mark it read.

---

## PHASE 5 — Delegation & Substitution

**Goal:** When a position holder is absent, their tasks automatically route to a delegate.

### Step 5.1 — Delegation entity

```csharp
// Archiving.Domain/Entities/PositionDelegation.cs

public class PositionDelegation : Entity
{
    public int Id { get; set; }
    public int FromPositionId { get; set; }      // the absent person's position
    public int ToPositionId { get; set; }         // the delegate's position
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DelegationScope Scope { get; set; }    // All, WorkflowOnly, MailOnly, SpecificWorkflowType
    public int? WorkflowDefinitionId { get; set; } // if Scope = SpecificWorkflowType
    public bool IsActive { get; set; }
    public int CreatedByUserId { get; set; }
    public string? Reason { get; set; }           // "Annual leave", "Sick leave", etc.

    // Navigation
    public Position FromPosition { get; set; }
    public Position ToPosition { get; set; }
}

public enum DelegationScope { All, WorkflowOnly, MailOnly, SpecificWorkflowType }
```

### Step 5.2 — Migration + DbContext

Add `DbSet<PositionDelegation>`, configure with Fluent API, create migration.

### Step 5.3 — Delegation service

```csharp
public interface IDelegationService
{
    Task<PositionDelegation> CreateDelegationAsync(CreateDelegationDto dto, int createdByUserId);
    Task RevokeDelegationAsync(int delegationId, int revokedByUserId);
    Task<List<PositionDelegation>> GetActiveDelegationsForPositionAsync(int positionId);
    Task<List<int>> GetEffectivePositionIdsForUserAsync(int userId);
    // Returns: user's own position IDs + any positions currently delegated to them
}
```

### Step 5.4 — Integrate delegation into task queries

Modify the unified task service (Phase 4) and the existing workflow `my-tasks` endpoint:
- When querying tasks for a user, also include tasks for positions delegated to them
- Mark delegated tasks with `AssignedVia = "Delegation"` so the UI can show a badge

### Step 5.5 — API endpoints

```
POST   /api/delegations                        → create a delegation
DELETE /api/delegations/{id}                    → revoke
GET    /api/delegations/from-position/{posId}   → list delegations from a position
GET    /api/delegations/to-position/{posId}     → list delegations to a position
GET    /api/delegations/my                      → delegations relevant to current user
```

### Step 5.6 — Auto-deactivation background job

Create a `DelegationExpiryJob` (IHostedService or similar to existing background jobs) that runs daily and deactivates delegations past their `EndDate`.

---

## PHASE 6 — Escalation Engine

**Goal:** Automatically escalate overdue tasks up the organizational hierarchy.

### Step 6.1 — Escalation rule entity

```csharp
public class EscalationRule : Entity
{
    public int Id { get; set; }
    public int? WorkflowDefinitionId { get; set; }  // null = applies globally
    public int? WorkflowStageId { get; set; }        // null = applies to all stages in the definition
    public int EscalationLevel { get; set; }          // 1, 2, 3 (sequential)
    public int OverdueMinutes { get; set; }           // trigger after this many minutes past SLA
    public EscalationAction Action { get; set; }
    public int? EscalateToPositionId { get; set; }    // explicit target, or null = use org hierarchy
    public bool NotifyOriginator { get; set; }
    public bool IsActive { get; set; }
}

public enum EscalationAction
{
    NotifyOnly,          // send notification to the escalation target
    ReassignTask,        // move the task to the escalation target
    NotifyAndReassign    // both
}
```

### Step 6.2 — Escalation log entity

```csharp
public class EscalationLog : Entity
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public int WorkflowStageId { get; set; }
    public int EscalationRuleId { get; set; }
    public int EscalationLevel { get; set; }
    public int EscalatedToPositionId { get; set; }
    public EscalationAction ActionTaken { get; set; }
    public DateTime EscalatedAt { get; set; }
}
```

### Step 6.3 — Escalation background job

Create `EscalationJob` (IHostedService, runs every 15 minutes):

```
1. Query all open workflow stage assignments WHERE DueDate < DateTime.UtcNow
2. For each overdue assignment:
   a. Calculate how many minutes overdue
   b. Find matching EscalationRules ordered by EscalationLevel
   c. Check EscalationLog to see which levels have already fired
   d. For the next unfired level where OverdueMinutes threshold is met:
      - If EscalateToPositionId is set, use that
      - If null, walk up the org tree from the assigned position to find the supervisor
      - Execute the EscalationAction (notify / reassign / both)
      - Log to EscalationLog
      - Publish WorkflowSLABreachedEvent
```

### Step 6.4 — Extend to correspondence and disposition

Apply the same escalation pattern to:
- Overdue correspondence responses (use a separate set of rules or a generic `EscalationTarget` enum)
- Overdue disposition requests

### Step 6.5 — API endpoints

```
GET    /api/escalation-rules                          → list rules
POST   /api/escalation-rules                          → create
PUT    /api/escalation-rules/{id}                     → update
DELETE /api/escalation-rules/{id}                     → delete
GET    /api/escalation-log                            → view escalation history (filtered by date, workflow, etc.)
GET    /api/escalation-log/by-department/{deptId}     → department escalation report
```

---

## PHASE 7 — Conditional & Parallel Workflow Routing

**Goal:** Extend the workflow engine to support parallel stages and conditional branching.

### Step 7.1 — Extend WorkflowStage entity

Add these fields to the existing `WorkflowStage` entity (migration):

```csharp
public StageType Type { get; set; }                    // Sequential, Parallel, Conditional
public string? ConditionExpression { get; set; }        // JSON: e.g., {"field":"Amount","operator":"greaterThan","value":100000}
public int? ParallelGroupId { get; set; }               // stages in the same group run in parallel
public bool RequiresAllParallelComplete { get; set; }   // true = AND join; false = OR join (any one completes)
public DynamicAssignmentType AssignmentType { get; set; } // Static, OrgHierarchy, DocumentOwnerDepartment
public string? AssignmentExpression { get; set; }         // JSON config for dynamic assignment
```

```csharp
public enum StageType { Sequential, Parallel, Conditional }
public enum DynamicAssignmentType
{
    Static,                    // assigned to a fixed position (existing behavior)
    OrgHierarchy,             // assign to the Nth level supervisor of the originator
    DocumentOwnerDepartment,  // assign to a specific role within the document creator's department
    DepartmentHead            // assign to the head of a specified or the originator's department
}
```

### Step 7.2 — Parallel stage processing

Modify the workflow advancement logic:

```
When a parallel stage group is reached:
  1. Create assignments for ALL stages in the group simultaneously
  2. Track completion of each parallel stage independently
  3. If RequiresAllParallelComplete = true:
     - Wait until ALL parallel stages in the group are completed
     - Then advance to the next sequential stage
  4. If RequiresAllParallelComplete = false:
     - Advance as soon as ANY one parallel stage completes
     - Cancel/skip the remaining parallel stages
```

### Step 7.3 — Conditional routing

Modify the stage advancement logic:

```
When the current stage completes and the next stage is Conditional:
  1. Evaluate the ConditionExpression against the document's data
  2. Support these operators: equals, notEquals, greaterThan, lessThan, contains, isEmpty
  3. Support these fields: any document metadata field, custom fields, the action taken on the previous stage
  4. If condition is TRUE, proceed to this stage
  5. If condition is FALSE, skip to the next stage after this one
```

### Step 7.4 — Dynamic assignment

When a stage's `AssignmentType` is not `Static`:

```
OrgHierarchy:
  - Look up the document creator's position
  - Walk up the org-unit tree N levels (configured in AssignmentExpression)
  - Assign to the head position at that level

DocumentOwnerDepartment:
  - Find the document creator's org-unit
  - Assign to the position matching the role specified in AssignmentExpression

DepartmentHead:
  - Assign to the head of a department (the creator's or a specified one)
```

### Step 7.5 — API updates

Update the workflow definition/stage CRUD endpoints to accept the new fields. Add validation:
- Parallel stages must have a `ParallelGroupId`
- Conditional stages must have a `ConditionExpression`
- Dynamic assignments must have valid `AssignmentExpression`

---

## PHASE 8 — Unified Timeline per Document

**Goal:** A single chronological view of everything that happened to a document.

### Step 8.1 — Timeline DTO

```csharp
public class TimelineEntryDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EntryType { get; set; }        // "StatusChange", "WorkflowAction", "Correspondence",
                                                  // "Share", "LocationMove", "FixityCheck", "Version",
                                                  // "Comment", "Attachment", "Escalation"
    public string Title { get; set; }             // human-readable Arabic summary
    public string? Description { get; set; }
    public string? ActorName { get; set; }        // who did it
    public string? ActorPositionTitle { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }  // type-specific extra data
}
```

### Step 8.2 — Timeline service

```csharp
public interface IDocumentTimelineService
{
    Task<List<TimelineEntryDto>> GetTimelineAsync(int documentId, TimelineFilter? filter);
}
```

Implementation queries from ALL these sources and merges chronologically:
1. `AuditLog` entries for this document
2. `WorkflowStageAction` records (from workflow instances linked to this document)
3. `Correspondence` records linked to this document
4. `DocumentShare` records
5. `Document` version history / status change log
6. `FixityCheck` records for this document's attachments
7. `EscalationLog` records (from Phase 6)
8. Physical location change records

Each source maps to a `TimelineEntryDto` with the appropriate `EntryType`.

### Step 8.3 — API endpoint

```
GET /api/documents/{id}/timeline?types=StatusChange,WorkflowAction&from=2025-01-01&to=2025-12-31
```

Returns the merged, sorted timeline. `types` filter is optional (comma-separated).

---

## PHASE 9 — Template & Numbering Engine

**Goal:** Configurable auto-numbering patterns for all entity types.

### Step 9.1 — NumberingTemplate entity

```csharp
public class NumberingTemplate : Entity
{
    public int Id { get; set; }
    public string EntityType { get; set; }            // "Document", "IncomingMail", "OutgoingMail",
                                                       // "WorkflowInstance", "Correspondence", "DispositionRequest"
    public int? InstitutionId { get; set; }            // null = global default
    public int? OrgUnitId { get; set; }                // null = institution-wide
    public int? DocumentTypeId { get; set; }           // null = all types
    public string Pattern { get; set; }                // e.g., "{DEPT}-{TYPE}-{YYYY}-{SEQ:5}"
    public ResetFrequency ResetFrequency { get; set; } // Never, Yearly, Monthly
    public int CurrentSequence { get; set; }
    public int? CurrentYear { get; set; }
    public int? CurrentMonth { get; set; }
    public bool IsActive { get; set; }
}

public enum ResetFrequency { Never, Yearly, Monthly }
```

### Step 9.2 — Numbering service

```csharp
public interface INumberingService
{
    Task<string> GenerateNumberAsync(string entityType, int? institutionId = null,
        int? orgUnitId = null, int? documentTypeId = null,
        Dictionary<string, string>? extraTokens = null);
}
```

Pattern tokens:
- `{YYYY}` — current year
- `{MM}` — current month
- `{DEPT}` — org-unit abbreviation
- `{TYPE}` — document type code
- `{INST}` — institution abbreviation
- `{SEQ}` or `{SEQ:N}` — sequence number (N = zero-padded width)

The service finds the most specific matching template (OrgUnit+Type > OrgUnit > Institution > Global), handles sequence increment with concurrency safety (use a DB-level lock or `UPDATE ... SET CurrentSequence = CurrentSequence + 1` pattern), and resets the sequence based on `ResetFrequency`.

### Step 9.3 — Integrate with existing modules

Replace existing hardcoded numbering logic (like `CORR-YYYY-SEQ`) with calls to `INumberingService`. Apply to:
- Document registration numbers (in `DocumentCreatedHandler`)
- Incoming mail registration numbers
- Outgoing mail official numbers
- Correspondence reference numbers
- Disposition request IDs

### Step 9.4 — API endpoints

```
GET    /api/numbering-templates                 → list all templates
POST   /api/numbering-templates                 → create
PUT    /api/numbering-templates/{id}            → update
DELETE /api/numbering-templates/{id}            → delete
POST   /api/numbering-templates/{id}/reset      → manually reset sequence
GET    /api/numbering-templates/preview          → preview what a pattern would generate
```

---

## PHASE 10 — Reporting & KPI Engine

**Goal:** Structured reporting endpoints + daily metric snapshots for trend analysis.

### Step 10.1 — DailyMetricSnapshot entity

```csharp
public class DailyMetricSnapshot : Entity
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int? InstitutionId { get; set; }
    public int? OrgUnitId { get; set; }
    public string MetricType { get; set; }     // "WorkflowAvgCompletionMinutes", "SLABreachCount",
                                                // "DocumentsByStatus", "DispositionBacklog",
                                                // "CorrespondenceAvgResponseMinutes", "StorageUsageBytes"
    public decimal Value { get; set; }
    public string? Breakdown { get; set; }      // JSON for sub-metrics
}
```

### Step 10.2 — Metrics snapshot background job

`MetricsSnapshotJob` runs daily at midnight:

```
For each institution and org-unit, calculate and store:
1. Workflow average completion time (per workflow definition)
2. SLA breach count (today)
3. Documents by status (count per status)
4. Active disposition requests (backlog count)
5. Correspondence average response time
6. Storage utilization (total bytes per department)
7. New documents created (today count)
8. Tasks completed (today count)
```

### Step 10.3 — Reporting API endpoints

```
GET /api/reports/workflow-performance
    ?from=2025-01-01&to=2025-12-31&definitionId=5&orgUnitId=3
    → { avgCompletionMinutes, slaBreachRate, completedCount, byStage: [...] }

GET /api/reports/department-activity
    ?from=2025-01-01&to=2025-12-31&orgUnitId=3
    → { documentsCreated, workflowsCompleted, correspondenceSent, avgResponseTime }

GET /api/reports/disposition-status
    → { pending, verified, approved, rejected, backlogByDepartment: [...] }

GET /api/reports/storage-utilization
    → { totalBytes, byDepartment: [...], physicalSlots: { total, used, available } }

GET /api/reports/trends
    ?metric=SLABreachCount&from=2025-01-01&to=2025-12-31&granularity=monthly
    → { dataPoints: [{ date, value }, ...] }

GET /api/reports/dashboard-summary
    → aggregated KPIs for the current user's scope (institution/department level)
```

---

## PHASE 11 — Integration Hooks (Webhooks)

**Goal:** Allow external systems to subscribe to events via HTTP callbacks.

### Step 11.1 — IntegrationHook entity

```csharp
public class IntegrationHook : Entity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string EventType { get; set; }         // matches domain event class names
    public string CallbackUrl { get; set; }
    public string? HttpMethod { get; set; }        // default POST
    public string? HeadersJson { get; set; }       // custom headers (e.g., API keys)
    public string? SecretKey { get; set; }          // for HMAC signature verification
    public bool IsActive { get; set; }
    public int MaxRetries { get; set; }             // default 3
    public int RetryDelaySeconds { get; set; }      // default 60
    public int? InstitutionId { get; set; }
    public int CreatedByUserId { get; set; }
}
```

### Step 11.2 — Webhook delivery entity

```csharp
public class WebhookDelivery : Entity
{
    public int Id { get; set; }
    public int IntegrationHookId { get; set; }
    public string EventType { get; set; }
    public string PayloadJson { get; set; }
    public int AttemptNumber { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DeliveryStatus Status { get; set; }     // Pending, Delivered, Failed, Retrying
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum DeliveryStatus { Pending, Delivered, Failed, Retrying }
```

### Step 11.3 — Webhook dispatcher

Create a generic `WebhookDispatcherHandler<T> : INotificationHandler<T> where T : IDomainEvent`:

```
1. On any domain event, query IntegrationHook for matching EventType where IsActive = true
2. For each match, serialize the event payload as JSON
3. Create a WebhookDelivery record with Status = Pending
4. Queue the delivery (use a Channel<T> or background queue)
```

Create `WebhookDeliveryJob` background worker:

```
1. Process pending deliveries from the queue
2. POST the payload to CallbackUrl with configured headers
3. Include an HMAC-SHA256 signature header if SecretKey is configured
4. If successful (2xx), mark as Delivered
5. If failed, increment attempt, schedule retry with exponential backoff
6. After MaxRetries, mark as Failed
```

### Step 11.4 — API endpoints

```
GET    /api/integration-hooks                        → list hooks
POST   /api/integration-hooks                        → create
PUT    /api/integration-hooks/{id}                   → update
DELETE /api/integration-hooks/{id}                   → delete
POST   /api/integration-hooks/{id}/test              → send a test payload
GET    /api/integration-hooks/{id}/deliveries        → delivery history
POST   /api/integration-hooks/{id}/deliveries/{did}/retry → manual retry
```

---

## Final Verification Checklist

After all phases are complete, verify these end-to-end flows:

1. **Full incoming mail lifecycle:**
   Create incoming mail → document auto-created → workflow auto-started → approve all stages → outgoing mail generated → document archived → retention clock started

2. **Disposition flow:**
   Wait for retention expiry (or simulate) → disposition request auto-created → verification step → final approval → physical slot released → destruction certificate → shared users notified → correspondence generated

3. **Escalation:**
   Create a workflow with a short SLA → let it expire → confirm escalation notification sent → confirm escalation log entry

4. **Delegation:**
   Set up a delegation → confirm delegate sees tasks in their worklist → confirm tasks marked as "Delegation"

5. **Parallel workflow:**
   Create a workflow definition with parallel stages → start an instance → confirm both parallel stages are assigned → approve both → confirm advancement to next stage

6. **Unified worklist:**
   Create tasks across multiple modules → call `GET /api/tasks` → confirm all task types appear

7. **Timeline:**
   Process a document through its full lifecycle → call `GET /api/documents/{id}/timeline` → confirm all events are chronologically merged

8. **Numbering:**
   Configure a custom numbering template → create entities → confirm generated numbers match the pattern → cross a year boundary → confirm yearly reset works

9. **Webhooks:**
   Register a webhook for DocumentCreatedEvent → create a document → confirm the webhook delivery was made with correct payload

10. **All existing tests still pass:** `dotnet test`

---

## Coding Standards (follow throughout)

- **Layered architecture:** Domain has no dependencies. Application depends on Domain. Infrastructure depends on Application + Domain. Api depends on all.
- **No business logic in controllers.** Controllers call services, return results.
- **All new entities** get proper EF Core Fluent API configuration — no data annotations on domain entities.
- **Arabic comments** on public service methods for consistency with existing code (if the codebase uses Arabic comments; otherwise use English).
- **All string columns** that store Arabic text: `utf8mb4` collation is already configured at DB level.
- **Every new endpoint** gets proper `[Authorize]` with the appropriate RBAC policy.
- **Every new migration** is tested: `dotnet ef database update` must succeed cleanly.
- **No breaking changes** to existing API contracts — new fields on existing DTOs should be nullable or have defaults.
