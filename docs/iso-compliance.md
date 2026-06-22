# ISO Compliance Mapping

This document maps system features to the records-management and digital-preservation
standards they help satisfy. It is updated as each implementation unit lands.

> Scope note: implementing a feature that supports a clause is not the same as a formal
> certification audit. This map records **which features address which requirements**.

## Standards in scope
- **ISO 15489** — Records management
- **ISO 14721 (OAIS)** — Open Archival Information System reference model
- **ISO 23081** — Metadata for records
- **ISO 19005 (PDF/A)** — Long-term document format
- **ISO 16363** — Audit & certification of trustworthy digital repositories

---

## Implemented

### Unit 1 — Fixity / integrity verification (ISO 16363, ISO 15489)
| Capability | Where | Standard |
|---|---|---|
| Checksum generated at ingest (SHA-256) | `LocalFileStorage.SaveAsync` → `DocumentAttachment.Checksum` | 16363 (fixity at ingest) |
| Checksum algorithm recorded | `DocumentAttachment.ChecksumAlgorithm` (default `SHA-256`) | 16363 |
| Periodic re-verification | `FixityVerificationBackgroundService` (cadence `Fixity:IntervalMinutes`, default daily; batch `Fixity:BatchSize`) | 16363 (periodic fixity) |
| On-demand verification | `POST /api/fixity/verify/{attachmentId}`, `POST /api/fixity/sweep` | 16363 |
| Verification provenance (append-only) | `FixityCheck` entity (`Verified`/`Failed`/`Missing`/`NoBaseline`) + report `GET /api/fixity/checks` | 16363 (provenance), 15489 (audit) |
| Every check recorded in the audit trail | `FixityService` → `IAuditWriter` (`FixityVerified`/`FixityFailed`/…) | 15489, 16363 |

**Permissions:** all fixity endpoints require `Audit.View`.

### Unit 2 — Tamper-evident audit trail verification (ISO 15489, ISO 16363)
| Capability | Where | Standard |
|---|---|---|
| Hash-chained audit log (each entry seals the previous) | `AuditWriter` + `AuditHash` | 15489 (audit trail), 16363 (tamper-evidence) |
| Chain verification (re-link + re-hash, report first break) | `AuditChainVerifier`, `GET /api/audit/verify` | 15489, 16363 |
| Persistence-stable hashing | `AuditHash` formats the timestamp to microsecond precision (`Truncate`) so the hash is reproducible after a `datetime(6)` round-trip | 16363 |
| Baseline reseal (one-time, admin-gated, audited) | `POST /api/audit/reseal` (`Audit.Edit`) | — (operational) |

**Note on reseal:** the original hashing used `DateTime.ToString("O")`, which is not stable across
the MySQL `datetime(6)` round-trip, so historical entries could not be re-verified. The corrected
hashing is stable going forward; `reseal` re-establishes a clean baseline over existing rows. It is a
deliberate, audit-logged, admin-only operation — not a routine action.

### Unit 3 — PDF/A preservation copies (ISO 19005, ISO 14721)
| Capability | Where | Standard |
|---|---|---|
| Generate a PDF/A-2b preservation master from a scan | `PdfaNormalizer` (QuestPDF), `PreservationService` | 19005 |
| Keep the submitted original (SIP) **and** the preservation master (AIP) | `DocumentAttachment.Kind` (`Original`/`PreservationMaster`) + `SourceAttachmentId` | 14721 |
| Conformance level recorded | `DocumentAttachment.PdfAConformance` (e.g. `PDF/A-2B`) | 19005 |
| Conformance **validated** with veraPDF | `VeraPdfValidator` (`Preservation:VeraPdfPath`), `PreservationValidated` + `PreservationNote` | 19005 |
| Auto-normalize on scan ingest; manual trigger | scan endpoint hook + `POST /api/documents/{id}/attachments/{attachmentId}/preserve` (`Documents.Archive`) | 19005 |
| Preservation events in the audit trail | `PreservationCopyCreated` via `IAuditWriter` | 16363 |

**Scope:** Unit 3 normalizes **image** sources (the scan ingest path). Converting *arbitrary existing
PDFs/Office files* to PDF/A (case B) is not yet implemented — those keep their original and are marked
"not normalized". A heavier converter (e.g. Ghostscript/Aspose) would be a later addition.

**Licensing note (action required):** PDF/A generation uses **QuestPDF** under its *Community License*,
which is free only for organizations under **US $1M** annual revenue. A government deployment likely
needs a **paid QuestPDF Professional/Enterprise license**. The runtime declares
`LicenseType.Community`; update it and obtain the licence as appropriate. veraPDF (validator) is
external and optional — install it and set `Preservation:VeraPdfPath` to enable conformance validation.

### Unit 4 — OAIS information packages (ISO 14721)
| Capability | Where | Standard |
|---|---|---|
| SIP / AIP per document (file list, checksums, metadata manifest) | `InformationPackage` + `PreservationPackageService` (upserted on read) | 14721 |
| Representation Information (format + PRONOM PUID + rendering note) | `RepresentationInfo` + `PronomMap` | 14721 |
| AIP export as a ZIP (preservation files + `manifest.json`) | `GET /api/documents/{id}/packages/aip/export` | 14721 |
| DIP recorded on dissemination/export | `InformationPackage` (type `Dissemination`) + audit `AipExported` | 14721 |
| Read API + UI panel | `GET /api/documents/{id}/packages`; "حزم الحفظ (OAIS)" panel on the document page | 14721 |
| Designated Community (who the archive serves + rendering) | `DesignatedCommunity` + `GET/PUT /api/preservation/designated-community`; Settings → الحفظ الرقمي | 14721 |

**Note:** packages are *materialized on read/export* (kept current), referencing the existing attachments —
no change to how files are stored. PRONOM ids are a best-effort static map (extend as formats grow).

### Unit 5 — Records metadata structure (ISO 23081)
| Capability | Where | Standard |
|---|---|---|
| Record↔**Agent** links with roles (Creator/Owner/Custodian/…) | `RecordAgent` + `RecordMetadataService` (auto-derived from CreatedBy / OwnerPosition / OwningOrgUnit) | 23081 (agent entity) |
| Typed **relationships** between records (IsVersionOf, RespondsTo, …) | `RecordRelationship` (auto from `ParentDocumentId`; manual add API) | 23081 (relationships) |
| **Business activity** link | workflow runs surfaced as the record's activities (`WorkflowInstance` where entity = document) | 23081 (business activity) |
| Read API + UI panel | `GET /api/documents/{id}/metadata`; "البيانات الوصفية (ISO 23081)" panel | 23081 |
| Manual additions | `POST …/metadata/agents`, `POST …/metadata/relationships` (`Documents.Edit`), audited | 23081 |

### Unit 6 — Preservation policy (ISO 16363)
| Capability | Where | Standard |
|---|---|---|
| Configurable preservation policy (target PDF/A, fixity algorithm + cadence, auto-normalize, allowed formats) | `PreservationPolicy` + `PreservationPolicyService` (defaults when unset) | 16363 (preservation policy) |
| Policy **drives** ingest: auto-normalize toggle + target conformance | scan hook + `PreservationService` read the policy | 16363 / 19005 |
| Policy **drives** the fixity sweep cadence | `FixityVerificationBackgroundService` (config override wins for dev) | 16363 |
| Read/update API + UI | `GET/PUT /api/preservation/policy`; Settings → الحفظ الرقمي | 16363 |

### Pre-existing capabilities that already support the standards
| Capability | Where | Standard |
|---|---|---|
| Unique record identifier | `Document.DocumentNumber` (unique) | 15489 |
| Registration date | `BaseEntity.CreatedAt` | 15489 |
| Classification scheme | `DocumentCategory` tree + `DocumentType` | 15489 |
| Descriptive metadata | `Document` fields, `DocumentAttachment` (MIME, size, ext) | 15489, 23081 |
| Retention & disposition | `RetentionPolicy`, `RetentionAlert`, `DisposalRequest` (request kept permanently) | 15489 |
| Access/permission controls | JWT + RBAC + `ConfidentialityLevel` clearance gating | 15489 |
| Document normalization for scans | scan endpoint converts images to PDF (PDFsharp) | 19005 (precursor; PDF/A is Unit 3) |

---

## Roadmap (planned, not yet implemented)

| Unit | Scope | Standard |
|---|---|---|
| 3b | **Case-B normalization** — convert arbitrary existing PDFs/Office files to PDF/A (needs Ghostscript/Aspose). | 19005 |

## Configuration
| Key | Default | Meaning |
|---|---|---|
| `Fixity:IntervalMinutes` | `1440` | How often the fixity sweep runs |
| `Fixity:BatchSize` | `50` | Attachments verified per sweep (least-recently-checked first) |
| `Preservation:VeraPdfPath` | _(unset)_ | Path to the veraPDF CLI; when set, PDF/A masters are validated |

> Fixity cadence, target PDF/A conformance and the auto-normalize toggle are also (and primarily) set via the
> **Preservation Policy** (Settings → الحفظ الرقمي / `PUT /api/preservation/policy`). `Fixity:IntervalMinutes`
> still overrides the cadence when present (useful for development).

## Tests
`backend/tests/Archiving.Tests` — audit-hash determinism, chain verification (intact / tampered /
broken-link / empty), and SHA-256 fixity digest (known vector). Run: `dotnet test`.

## Secure Destruction / Disposition (ISO 15489 §9.9–9.10, ISO 16363 provenance)

Controlled, auditable destruction of records that have met retention.

**Eligibility (ISO 15489 retention-driven disposition).** A record is destroyable only when its
retention has expired **AND** it is under no active **legal hold** **AND** no workflow is open on it
(`IDestructionEligibilityService`). Re-checked at request, approval, and execution.

**Legal hold.** `LegalHold` (document / folder / org-unit scope) makes in-scope records permanently
ineligible until released. No path can destroy a held record.

**Segregation of duties (two-person rule).** Requester ≠ approver ≠ executor — enforced in code.
Execution additionally requires **MFA step-up** (re-authentication) before the irreversible action.

**Secure digital destruction.** Each file is encrypted with its own data key (DEK) wrapped by the
master key and stored in the file header. **Crypto-shredding** destroys that wrapped DEK, rendering the
ciphertext permanently unrecoverable without touching any other file; the bytes are then removed. Legacy
plaintext/global-key files fall back to **secure overwrite** (random passes + truncate + unlink). All
representations (SIP original, AIP/PDF-A master, DIP/cache) are destroyed; a **fixity-void** provenance
entry is appended (`FixityResult.Missing`), and the original SHA-256 is retained in the certificate as
proof-of-prior-existence.

**Tombstone (keep metadata, destroy content).** The document row is never deleted: `IsTombstone`,
`DestroyedAtUtc`, `DestructionCertificateId`, status `Disposed`. Proves the record existed and was
lawfully destroyed.

**Certificate of Destruction.** Rendered to **PDF/A-2b** (QuestPDF) with request ref, item list +
checksums-before, method, approver, executor, UTC timestamps, and organization name.

**Audit.** Every action (place/release hold, create/submit/approve/reject/cancel/execute) is written to
the hash-chained audit log; the execution event is individually verifiable via `/api/audit/verify`.

**RBAC.** `Destruction.Create` (request), `Destruction.Approve`, `Destruction.Delete` (execute),
`LegalHold.View/Edit`. Seeded: request → Archive Officer; approve + holds → Manager; execute → Admin.
