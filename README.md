# Marasil · Enterprise DMS & Internal Correspondence Platform

**Marasil** is an integrated platform for institutional document management, electronic & physical archiving, internal correspondence, and administrative workflow. It is **Arabic-first (RTL)** with English support, and ships with light **and** dark themes. It unifies electronic documents, the physical archive, correspondence (incoming/outgoing/internal), meetings & decisions, a configurable workflow engine, and a tamper-evident audit log — on one shared architecture.

> Baseline **v1.1** — multi-organization ready · 2,000,000-document design ceiling.

> 🇸🇦 Arabic README: [`README.ar.md`](README.ar.md) · Developer setup with SQL Server (Arabic, step by step): [`docs/local-setup-sqlserver.ar.md`](docs/local-setup-sqlserver.ar.md)

---

## Contents
- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Project Layout](#project-layout)
- [Modules & Features](#modules--features)
- [Roles & Permissions](#roles--permissions)
- [Running Locally](#running-locally)
- [ISO & Preservation](#iso--preservation)
- [Key Design Decisions](#key-design-decisions)

---

## Overview

Every item (a letter, a document, a decision) follows the same lifecycle — **Capture → Register → Classify → Route → Approve → Execute → Archive** — with governance (permissions, security classification, audit, retention) applied at every stage.

Core principles:
- **Internal correspondence is connected:** incoming mail is **never created manually**; it is auto-generated when an outgoing letter is dispatched to an employee, department, or the archive.
- **Two independent classification axes:** a security classification (who may access) and a records classification / file plan (how long it is kept).
- **Tamper-evident audit:** a hash-chained log whose integrity is verifiable end-to-end.
- **API-first** with an events/webhooks framework for integration.
- **Arabic-first + RTL** with a dark mode and customizable appearance.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API — **.NET 10** |
| Data access | **EF Core 10** (SQL Server and MySQL providers) |
| Database | **SQL Server** 2016+ (Express included) or **MySQL 8.4** (`utf8mb4`), chosen by `Database:Provider` |
| In-process events | **MediatR** (domain events) |
| Auth | **JWT** + **RBAC** (per-module permissions) + MFA-ready (TOTP) |
| Storage | `IFileStorage` abstraction (local disk for MVP · S3/MinIO for production) |
| Frontend | **React + TypeScript + Vite** |
| Styling | **Tailwind CSS v4** + **shadcn/ui** + Marasil design tokens |
| i18n | **i18next** (Arabic/English, RTL/LTR) |
| Animation | **motion/react** |
| Reports / capture | QuestPDF (PDF) · scanning via a local **Scan Agent** |

---

## Architecture

A **layered** (Clean Architecture) backend:

```
Archiving.Domain          ← entities + enums (the data model) + domain events
Archiving.Application      ← DTOs, service interfaces, result/paging models
Archiving.Infrastructure   ← EF Core DbContext + migrations (MySQL), JWT, storage, services, background jobs
Archiving.Migrations.SqlServer ← SQL Server migrations
Archiving.Api             ← controllers, DI, middleware, RBAC policies
```

- **Domain events** are dispatched after save via MediatR (e.g. notifications/logs).
- **Audit** is written through a central `AuditWriter` (single choke-point).
- **Background (hosted) services:** escalation, retention/disposition, delegation expiry, metrics snapshots, webhook delivery, fixity verification, and text-extraction/OCR.

---

## Project Layout

```
backend/
  Archiving.slnx
  src/
    Archiving.Domain/
    Archiving.Application/
    Archiving.Infrastructure/   (Persistence/Migrations — MySQL migrations)
    Archiving.Migrations.SqlServer/ (SQL Server migrations)
    Archiving.Api/Controllers/  (38+ controllers)
  tests/Archiving.Tests/        (158 tests)
frontend/
  src/
    pages/        (dashboard, login, correspondence, documents, governance,
                   external, records, disposition, destruction, archive,
                   locations, monitoring, reports, workflow, settings)
    components/   (AppLayout, shadcn ui primitives, panels, forms)
    lib/          (API clients, auth, theme, appearance, branding)
    styles/       (tailwind.css · diwan.css · marasil.css — design tokens)
docs/
tools/            (the local scan-agent)
```

---

## Modules & Features

### 1) Auth / Users / RBAC
- Admin-provisioned users (no self-registration), JWT, MFA-ready.
- Fine-grained per-module/per-action permissions (`Resource.Action`) via a `[HasPermission]` attribute.

### 2) Organization
- Multi-level hierarchy (departments / sections / units); **positions as first-class entities** so routing follows the role, not a named person.

### 3) Correspondence — the unified **المراسلات** section
- **Incoming / Outgoing / Internal** tabs in one register.
- **Outgoing routing** to: an external party, an employee, a department, or the archive.
- **Auto-generated incoming:** when an outgoing letter is approved and sent to an internal recipient, it automatically appears as their incoming mail, linked back to the original letter.
- **Type:** a text message or a document (file upload **or scan**), with attachments copied to the recipient.
- Auto letter numbering, confidentiality & priority, status lifecycle, an activity timeline, and actions (forward to employee/department, approve, hold, close, archive).

### 4) Document Management
- **Dynamic document types + custom fields** (admin-managed, no code changes).
- Multiple attachments, **scanning**, download/preview, full-text indexing (OCR), versioning, check-in/out, and physical-location tracking.

### 5) External Directory
- A directory of external organizations and contacts (usable as senders/recipients in correspondence).

### 6) Governance — Meetings & Decisions
- **Committees/boards → meetings → minutes (agenda items) → administrative decisions** (often permanent records), with auto-numbered decisions and publishing.

### 7) Records / Classification
- A hierarchical file plan + record series that own retention rules, and a two-axis classification (security + records).

### 8) Retention & Disposition / Destruction
- A **Verify → Final Approval → Execute** workflow for disposition (destroy/renew/transfer), with a **Certificate of Destruction** and a step-up security check, plus a cross-cutting **Legal Hold**.

### 9) Physical Archive & Locations
- A **Building → Room → Cabinet → Shelf → Box** hierarchy (card UI) and printable box labels; a cascading picker files documents into a box.

### 10) Workflow Engine
- Configurable approvals, routing by person/position, **delegation**, automatic **escalation**, per-step SLA, and step-level audit.

### 11) Tasks — a unified module for assignable tasks & follow-ups.

### 12) Search / OCR — quick & advanced search, full-text inside PDF/Word + OCR for scanned documents (Arabic + English), MySQL `FULLTEXT` index.

### 13) Dashboards / Reports
- Executive & operational KPIs, charts, a retention-expiry **calendar**, and exportable reports (PDF/Excel/CSV).

### 14) Audit (Monitoring Log)
- A tamper-evident, verifiable hash chain. **Only** create / edit / delete / download / print are recorded. Deleting a log entry is a **chain-safe soft-hide** that preserves integrity and records who performed it.

### 15) Notifications — in-app and email (a channel-abstracted model that is extensible).

### 16) Integration & Preservation
- **API-first** + events/webhooks, integration hooks, numbering sequences.
- **Preservation:** fixity checks, PDF/A conformance, ISO 16363/15489 alignment.

### 17) Appearance / Branding
- Customizable sidebar colors & font, system background/foreground, dark mode, and institution branding/logo (shown on the login page and top bar).

---

## Roles & Permissions

Default roles: **System Administrator**, Organization Administrator, Department Manager, Section Supervisor, **Employee**, and a read-only **Auditor**. Each role has a configurable permission set per module/action.

---

## Running Locally

The full walkthrough, with troubleshooting, is in [`docs/local-setup-sqlserver.ar.md`](docs/local-setup-sqlserver.ar.md) (Arabic). Summary:

### Prerequisites
- **Windows** 10/11 or Windows Server (stored files are encrypted with a DPAPI-protected keyring)
- **.NET 10 SDK**
- **Node.js 22 LTS** (minimum 20.19)
- **SQL Server** 2016+ (Express is fine) — or **MySQL 8.4**

### 1) Database and settings
```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "IF DB_ID(N'Archiving') IS NULL CREATE DATABASE [Archiving];"

cd backend\src\Archiving.Api
copy appsettings.Development.SqlServer.json.example appsettings.Development.json
# edit ConnectionStrings:Default and Jwt:Key  (the file is gitignored — never commit it)
```
For MySQL, start from `appsettings.Development.json.example` instead.

### 2) Backend
```powershell
cd backend
dotnet run --project src/Archiving.Api --launch-profile http   # http://localhost:5105
```
On first start the API applies all migrations and seeds reference data itself; no `dotnet ef database update` is needed.

### 3) Frontend
```powershell
cd frontend
npm install
npm run dev                                     # http://localhost:5173
```
The SPA calls `http://localhost:5105/api` by default; override with `VITE_API_URL` in `.env.local` (copy `.env.example`).

### 4) First login
The UI opens in **Arabic** by default. A default admin is seeded on first run:
- **User name:** `admin` (or `admin@archiving.local`)
- **Password:** `Admin@12345`

### Tests
```bash
dotnet test backend/Archiving.slnx
```

---

## ISO & Preservation

Work to align with ISO 15489 / 14721 (OAIS) / 23081 / 19005 (PDF/A) / 16363 is tracked in [`docs/iso-compliance.md`](docs/iso-compliance.md). Implemented:
- **Fixity (ISO 16363):** SHA-256 at ingest + periodic re-verification background job; on-demand `POST /api/fixity/verify/{attachmentId}`; append-only provenance.
- **Tamper-evident audit (ISO 15489/16363):** hash-chained log with `GET /api/audit/verify` (and an admin baseline `POST /api/audit/reseal`).
- **PDF/A preservation (ISO 19005/14721):** scans normalized to a **PDF/A-2b** master while keeping the original (SIP/AIP); optional veraPDF validation. *(QuestPDF runs under the Community License — a government deployment likely needs a paid licence.)*
- **OAIS packages (ISO 14721):** per-document SIP/AIP/DIP with a manifest + Representation Information; AIP ZIP export.
- **Records metadata (ISO 23081):** record↔agent links, typed relationships, business activities.
- **Encryption at rest:** optional AES-256 of stored files (`Storage:EncryptionKey`), with checksums kept over the plaintext so fixity/OCR/preservation keep working.

---

## Key Design Decisions

- **Incoming is never created manually** — it is generated from internal outgoing letters (both ends and attachments stay linked).
- **Two independent classification axes:** security (access) and records (retention).
- **Tamper-evident audit** via a hash chain; deletion is a chain-safe soft-hide.
- **Routing by position, not person** — work follows the seat, so it survives staff turnover.
- **Arabic / RTL first** with a consistent dark mode and per-browser appearance customization.
- **API-first** so future systems and channels integrate without a rebuild.

---

<div align="center">

**Marasil** · Enterprise DMS & Internal Correspondence Platform · Baseline v1.1

</div>
