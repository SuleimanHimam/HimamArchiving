# Data Model — reference

> This document describes the Phase-1 entities implemented in `backend/src/Archiving.Domain`.
> **Migrations have been generated and applied** (`InitialCreate` + `FixNotificationRecipientFk`)
> to the local MySQL 8.4 instance (see `docs/local-dev.md`). This file is kept as a reference to the model.

**Stack:** ASP.NET Core Web API (.NET 10) · MySQL (Pomelo + EF Core 9) · React SPA · JWT + RBAC + MFA · S3/MinIO storage (local disk for MVP).
**Conventions:** PKs are `BIGINT` (`long`). All tables stamp `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy`. High-value records (`User`, `Document`, `IncomingMail`, `OutgoingMail`) are **soft-deleted** (`IsDeleted/DeletedAt/DeletedBy`) so nothing is lost. Timestamps are UTC. Arabic text uses `utf8mb4`.

> **Scope note:** the internal user-to-user chat/messaging module is intentionally **excluded**, per spec.

---

## 1. Identity, RBAC & Position-based access

| Entity | Key fields | Notes |
|---|---|---|
| **User** | FullName, Email (unique), Phone, JobTitle, OrgUnitId, PasswordHash, **Clearance** (ConfidentialityLevel), IsActive, MfaEnabled/MfaMethod/MfaSecret, DirectoryLogin | Captures mandated minimum. `Clearance` gates access to classified items. MFA + AD fields built in. |
| **Role** | Name (unique), Description, IsSystem | Seeded roles can't be deleted. |
| **Permission** | Resource, Action (enum), Code (e.g. `Documents.Delete`) | One row per (resource, action). |
| **RolePermission** | RoleId + PermissionId | M:N. |
| **UserRole** | UserId + RoleId | M:N. |
| **Position** | Title, Code, OrgUnitId, **Rank**, **CurrentOccupantUserId** | A *seat*. Transactions bind here, not to a person. |
| **PositionAssignment** | PositionId, UserId, StartDate, EndDate, IsCurrent | History of occupants → **open work auto-transfers** when the seat changes hands. |
| **RefreshToken** | UserId, Token, ExpiresAt, RevokedAt | JWT refresh rotation. |

**Position-based access** (key government requirement) is modeled via `Position.CurrentOccupantUserId` + `PositionAssignment` history; all assignable things reference `PositionId`.

## 2. Organizational structure

| Entity | Key fields | Notes |
|---|---|---|
| **Institution** | Name, NameEn, Code, LogoStorageKey, contact | Logo lives in object storage, not the DB. |
| **OrgUnit** | InstitutionId, **ParentId** (self-ref), Name, Type (enum), ManagerPositionId, SortOrder | Flexible tree: Institution → Directorate → Department → Unit → Committee → Team. |

## 3. Document management

| Entity | Key fields | Notes |
|---|---|---|
| **DocumentCategory** | ParentId (self-ref), Name, Code | Optional classification tree. |
| **DocumentType** | Name, Code, CategoryId, DefaultConfidentiality, RetentionMonths, DefaultWorkflowDefinitionId, RequiresApproval, **AllowedUploadSources** | Configurable per-type settings; upload-source restriction (scanner-only) supported. |
| **Document** | DocumentNumber (unique), Title, Description, DocumentTypeId, CategoryId, OwningOrgUnitId, OwnerPositionId, Confidentiality, Status, **Keywords**, RetentionMonths, DocumentDate, ExpiryDate, Version/ParentDocumentId/IsLatestVersion | Versioned, soft-deleted, full-text on Title/Description/Keywords. |
| **DocumentAttachment** | DocumentId, FileName, ContentType, FileExtension, SizeBytes, **StorageKey**, Checksum, IsScanned, OcrText | Binaries in object storage. OcrText filled in Phase 2. Formats: PDF/DOCX/XLSX/JPG/PNG/ZIP. |

## 4. Official mail (incoming & outgoing)

| Entity | Key fields | Notes |
|---|---|---|
| **IncomingMail** | TransactionNumber (auto), SenderEntity, SenderReference, Subject, IssueDate, ReceivedDate, Confidentiality, Priority, Keywords, Status, **AssignedToPositionId/OrgUnitId/UserId**, **ParentMailId**, WorkflowInstanceId | Routed to seat/unit; reference/reply chain via ParentMailId. |
| **OutgoingMail** | LetterNumber (auto), RecipientEntity, Subject, Body, LetterTemplateId, SignatoryPositionId, Status, SentDate, **InReplyToIncomingMailId**, ApprovedBy/At | Auto-archived on dispatch. |
| **MailAttachment** | MailId, Direction (enum), file fields, StorageKey | Shared by both directions. |
| **LetterTemplate** | InstitutionId, Name, HeaderHtml, FooterHtml, BodyTemplate, NumberingPattern | Letterhead/footer + official numbering for printing. |

## 5. Workflow engine (the core)

| Entity | Key fields | Notes |
|---|---|---|
| **WorkflowDefinition** | Name, TriggerModule, DocumentTypeId, Version, IsActive | Admin-defined dynamic route. |
| **WorkflowStage** | DefinitionId, Name, Order, **AssigneeType** + Assignee*Id, ResponseHours, EscalateAfterHours, **EscalateToPositionId**, AllowedActions (flags), TransitionCondition, IsFinal | Per-stage seat, SLA, actions, escalation target (default: direct manager). |
| **WorkflowInstance** | DefinitionId, EntityType+EntityId, **CurrentStageId**, **CurrentAssigneePositionId** (location), Status, StartedAt, DueAt, CompletedAt | A live run bound to a document/mail. |
| **WorkflowTask** | InstanceId, StageId, AssignedToPositionId/UserId, Status, DueAt, ActionTaken, Note, CompletedAt, IsEscalated | Open tasks = worklist; completed tasks = immutable routing history. |

Escalation: when `DueAt` passes, a background job notifies (in-app/email/SMS) and escalates to `EscalateToPositionId` or the unit's manager position.

## 6. Lifecycle (retention & disposal)

| Entity | Key fields | Notes |
|---|---|---|
| **RetentionPolicy** | DocumentTypeId, RetentionMonths, DisposalAction, RequiresApproval | Per-type policy. |
| **RetentionAlert** | DocumentId, **Stage** (30/15/7/Expired), DueDate, NotifiedAt | Pre-expiry warnings. |
| **DisposalRequest** | DocumentId, Action, Status, RequestedBy/At, ApprovedBy/At, ExecutedAt | Disposal approval flow; **row kept permanently** as historical archive. |

## 7. Physical archiving

| Entity | Key fields | Notes |
|---|---|---|
| **PhysicalLocation** | ParentId (self-ref), Name, Type (Building/Room/Cabinet/Shelf/Box), Code, **RfidTag** | RFID field reserved for Phase 2. |
| **PhysicalArchiveItem** | DocumentId?/IncomingMailId?, PhysicalLocationId, BoxNumber, FileNumber, ArchivedAt/By | Links a digital record to its paper location. |

## 8. Notifications & audit

| Entity | Key fields | Notes |
|---|---|---|
| **Notification** | RecipientUserId, Title, Body, Type, Channels (flags), EntityType/Id, IsRead, IsEscalation | In-app record + multi-channel intent. |
| **NotificationDelivery** | NotificationId, Channel, Succeeded, SentAt, Error | Per-channel delivery for email/SMS/push + retry. |
| **AuditLog** | UserId, Action, EntityType/Id/Title, OldValues/NewValues (JSON), IpAddress, UserAgent, **PreviousHash + Hash** | Append-only, **hash-chained for tamper-evidence**. |

---

## Open questions before I generate migrations

1. **Confidentiality levels** — I used 4 (Public, Internal, Confidential, HighlyConfidential) matching the spec. OK?
2. **`Document.Keywords`** — stored as a single full-text-indexed column for MVP (simple) rather than a separate Tag table. Acceptable, or do you want normalized tags?
3. **PK type** — `BIGINT` everywhere for archive scale. Any objection to non-sequential exposure (we can switch to opaque public IDs later if needed)?
4. **Soft-delete scope** — applied to User/Document/IncomingMail/OutgoingMail. Want it on more entities?
5. **MySQL target version** — assuming **MySQL 8.0+** (needed for proper `utf8mb4`, full-text, window functions). Confirm.

**Reply "approve" (or with changes) and I'll generate the initial EF Core migration** — once a MySQL 8 instance is available (not currently installed on this machine).

## Destruction / Disposition entities (Phase: Secure Destruction)

- **LegalHold** — `Reason`, `Scope` (Document|Folder|OrgUnit|Query), scope target id, `PlacedBy/At`,
  `ReleasedBy/At?` (null = active). Active holds block destruction.
- **DestructionRequest** — `Status` (Draft→PendingApproval→Approved→Executing→Completed; +Rejected/
  Cancelled/Failed), `Reason`, `RetentionBasisId?`, requester/approver/executor ids + UTC timestamps,
  `ScheduledForUtc?`, `CertificateId?`, `WorkflowInstanceId?`.
- **DestructionItem** — links a request to a document; `Method` (CryptoShred|SecureOverwrite|
  DeleteFixityVoid|Shredding|Incineration|Pulping|Degaussing), `ChecksumBefore`, `Outcome`.
- **DestructionCertificate** — `CertificateNumber`, `PdfStorageKey`, `IssuedAtUtc`.
- **Document** gains `IsTombstone`, `DestroyedAtUtc`, `DestructionCertificateId`.
- **DocumentAttachment** gains `ContentDestroyed`, `ContentDestroyedAt`.

Storage: files use per-file wrapped data keys (`AENC2` header) to enable crypto-shredding.

## Physical Location Module (normalized hierarchy)

Normalized entities for the paper-archive hierarchy (alongside the legacy single-table model until migrated):

- **Building** → **Room** → **Cabinet** (الخزانة) → **Shelf** → **Box** → Document.
- **Room** belongs to one Building; **RoomConnection** is a self-referencing adjacency
  (Door/Corridor/Internal Passage) stored one row per direction and **mirrored** by the service.
- **Cabinet** ∈ Room, **Shelf** ∈ Cabinet, **Box** ∈ Shelf *or* directly a Room (3-level mode).
- Each level: `NameAr`/number + optional code, `IsActive` (no hard delete while children exist).
- **Box** has `BoxCode` (unique), `Barcode`, `Capacity`, `CurrentCount` (auto inc/dec as documents are
  filed/moved/unfiled; `IsFull` when `CurrentCount ≥ Capacity`). **Document** gains `BoxId`.
- `GET /api/locations/tree` and `/api/locations/{box}/breadcrumb` (path + `GenerateLocationCode`,
  e.g. `B1-R103-C2-S4-BX12`).
- `POST /api/locations/migrate-legacy` — best-effort, idempotent migration of the old
  `PhysicalLocation` + `PhysicalArchiveItem` data into the new model (creates default intermediate
  levels where the legacy chain skipped one, links documents to their new box).
- Rules: delete blocked while children/documents exist; no self-connection; no duplicate room pair.
