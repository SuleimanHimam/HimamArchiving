# نظام إدارة الوثائق والأرشفة وسير العمل · Document Management, Archiving & Workflow System

Enterprise platform for institutional documents, electronic/physical archiving, and administrative workflow.
RTL/Arabic-first. Built to the spec in `Archiving_System_Documentation.pdf` (the source of truth).

## Stack (mandated)
- **Backend:** ASP.NET Core Web API (.NET 10), layered architecture, EF Core 9 + Pomelo
- **Database:** MySQL 8 (`utf8mb4`)
- **Frontend:** React + TypeScript (Vite), Motion — *Official Diwan* design system
- **Auth:** JWT + RBAC + MFA · **Storage:** S3/MinIO (local disk for MVP)
- **Excluded by spec:** internal user-to-user chat/messaging

## Layout
```
backend/
  Archiving.slnx
  src/
    Archiving.Domain/          # entities + enums (the data model)
    Archiving.Application/      # DTOs, service interfaces, result/paging models
    Archiving.Infrastructure/   # EF Core DbContext + migrations, MySQL, JWT, storage, services
    Archiving.Api/             # controllers, DI, middleware, RBAC policies
frontend/                      # React SPA — Diwan design system, RTL
docs/
  data-model.md                # reference: the implemented data model
  local-dev.md                 # local MySQL/credentials/run notes
```

## Status
- ✅ Solution scaffolded, builds clean (backend + frontend)
- ✅ Full Phase-1 domain model + EF Core migrations (applied to MySQL 8.4 — see `docs/local-dev.md`)
- ✅ Auth (JWT + RBAC + permission policies) and RBAC/admin seeding
- ✅ Module APIs implemented & smoke-tested end-to-end:
  - **Documents** — types/categories, CRUD, versioning fields, attachment upload/download (local disk storage), clearance-gated
  - **Incoming Mail** — register, route/forward, lifecycle actions, timeline
  - **Outgoing Mail** — draft → submit → approve → send (auto-archive), official numbering
  - **Workflow engine** — admin-defined definitions/stages, instances, position-based worklist (`my-tasks`), act (approve/reject/forward/return/hold/close) with stage advancement + SLA due dates
  - **Notifications** — in-app feed, unread count, mark read (auto-created on workflow task assignment)
  - **Physical archive** — location tree + digital→paper links
  - **Lifecycle** — retention policies, expiring-document report, disposal request approve/execute flow
  - **Organization** — institutions, org-unit tree, positions, occupant assignment, user lookup
  - **Preservation / integrity (ISO)** — fixity verification (`/api/fixity/*`) and tamper-evident audit-chain verification (`/api/audit/verify`); see below
- ✅ Frontend: login, dashboard, Incoming Mail screens, **Documents** screens (list/create/detail + attachment upload/download)
- ✅ Automated tests: `backend/tests/Archiving.Tests` (`dotnet test`)
- ⏭ Next: ISO units 3–6 (PDF/A normalization, OAIS information packages, ISO 23081 metadata, preservation policy); OCR/full-text; S3/MinIO storage swap

## ISO compliance (in progress)
Work to align with ISO 15489 / 14721 (OAIS) / 23081 / 19005 (PDF/A) / 16363 is tracked in
[`docs/iso-compliance.md`](docs/iso-compliance.md). Implemented so far:
- **Fixity (ISO 16363):** SHA-256 at ingest + algorithm recorded; periodic re-verification background
  job + on-demand `POST /api/fixity/verify/{attachmentId}`; append-only `FixityCheck` provenance.
- **Tamper-evident audit (ISO 15489/16363):** hash-chained audit log with end-to-end verification
  `GET /api/audit/verify` (persistence-stable hashing; one-time admin `POST /api/audit/reseal` baseline).
- **PDF/A preservation (ISO 19005/14721):** scans are normalized to a **PDF/A-2b** preservation master
  (QuestPDF) while keeping the submitted original (SIP/AIP); conformance recorded and optionally
  validated with veraPDF. Auto on scan; manual `POST /api/documents/{id}/attachments/{attachmentId}/preserve`.
  > **Licence:** QuestPDF runs under the Community License (free < $1M revenue). A government deployment
  > likely needs a paid QuestPDF licence — see `docs/iso-compliance.md`.
- **OAIS information packages (ISO 14721):** per-document **SIP/AIP/DIP** with a manifest (files +
  checksums + metadata) and **Representation Information** (PRONOM); `GET /api/documents/{id}/packages`,
  AIP **ZIP export** `…/packages/aip/export`, and a **Designated Community** record
  (`/api/preservation/designated-community`). UI: "حزم الحفظ" panel on the document + Settings → الحفظ الرقمي.
- **Records metadata (ISO 23081):** record↔**agent** links with roles, typed **relationships**, and
  **business activities** (workflows) — `GET /api/documents/{id}/metadata`; UI: "البيانات الوصفية" panel.
- **Preservation policy (ISO 16363):** configurable target PDF/A, fixity algorithm/cadence, auto-normalize
  toggle and allowed formats that **drive** ingest + the fixity sweep — `GET/PUT /api/preservation/policy`;
  UI: Settings → الحفظ الرقمي.

Fixity cadence is configurable via `Fixity:IntervalMinutes` (default 1440) and `Fixity:BatchSize` (default 50).

## Configuration (local setup)
Secrets are **not** committed. `appsettings.json` ships with placeholders; real local values live in a
gitignored override. After cloning:
```bash
cp backend/src/Archiving.Api/appsettings.Development.json.example \
   backend/src/Archiving.Api/appsettings.Development.json
# then edit it: DB password, a long random Jwt:Key, and machine-specific paths
```

## Run
```bash
# backend (API skeleton)
dotnet build backend/Archiving.slnx
dotnet run --project backend/src/Archiving.Api

# frontend
cd frontend && npm install && npm run dev
```

## Prerequisites not yet installed on this machine
- **MySQL 8** (required before generating/applying migrations)
- (optional) MinIO/Docker for object storage; local disk works for MVP
