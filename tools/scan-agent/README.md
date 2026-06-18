# Archiving — Local Scan Agent

The system is hosted in the cloud, but scanners are attached to each user's **own PC**. Browsers can't
talk to a USB/TWAIN scanner directly, so this small agent runs on the user's machine and exposes
scanning over loopback. The web app (even when served over HTTPS) calls `http://127.0.0.1:8765`, which
browsers permit for loopback addresses.

```
Browser (cloud SPA)  ──fetch──►  http://127.0.0.1:8765  ──WIA──►  local scanner
        ▲                                   │
        └────────── scanned PDF/JPEG ◄───────┘
   then uploaded to the cloud:  POST /api/documents/{id}/scan   (flagged IsScanned)
```

## What it does
- `GET  /status` → `{ "status":"ok", "scanners":[ "..." ], "mock": bool }`
- `POST /scan` with JSON `{ "format":"pdf"|"jpeg", "scanner":"<optional name>" }` → returns the scanned
  page as a binary `application/pdf` or `image/jpeg` body.

Scanning uses **Windows Image Acquisition (WIA)** — the standard, driver-agnostic Windows scanning API
(no vendor SDK or license required). The agent is a single self-contained executable with no runtime deps
beyond .NET.

## Build
```bash
dotnet build tools/scan-agent            # or: dotnet publish -c Release -r win-x64 --self-contained
```

## Run
```bash
# Real scanner (must be installed with a WIA driver on this PC):
archiving-scan-agent

# No scanner — returns a sample page so you can test the whole pipeline end-to-end:
archiving-scan-agent --mock
```
It listens on `http://127.0.0.1:8765`. Leave it running in the system tray / as a startup item on each
user's PC. (Package with `dotnet publish` and add a shortcut to the Startup folder, or install as a
Windows service.)

## Pointing the web app at the agent
The SPA defaults to `http://127.0.0.1:8765`. Override per-deployment with a Vite env var if you change the
port:
```
# frontend/.env.local
VITE_SCAN_AGENT_URL=http://127.0.0.1:8765
```

In the document detail page, **مسح ضوئي (Scan)** calls the agent; if the agent isn't detected, it falls
back to a file picker so the user can upload a file their scanner software produced (still flagged as
scanned). Scanner-only document types reject ordinary file uploads and require this scan path.

## Notes / limitations
- Real WIA acquisition needs a physical scanner with a Windows driver; it can't be exercised on a headless
  server. `--mock` verifies everything except the driver call.
- Single-page scan per request. Multi-page/ADF batching and an explicit scanner picker UI are natural
  next steps.
- For production, sign the executable and consider scoping CORS to your exact cloud origin (the agent
  currently echoes the request `Origin`).
