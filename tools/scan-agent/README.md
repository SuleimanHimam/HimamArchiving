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

## The window and the tray icon

The agent lives in the notification area. Its icon is **blue while listening and grey when stopped**,
and the tooltip shows the port. Click it to open the window; right-click for a short menu (open,
run-at-login, mock mode, quit).

The window shows:

- whether the agent is listening, and on which URL (the link opens `/status` in a browser)
- the scanners and printers it can see, with a **تحديث الأجهزة** button to re-enumerate
- **تشغيل الوكيل تلقائياً عند بدء تشغيل Windows** — the run-at-login switch (see below)
- **وضع المحاكاة** — return a sample page instead of using a scanner, toggleable at runtime
- **اختبار المسح** — performs a real scan through the agent's own HTTP endpoint (the same call the
  web app makes) and offers to open the resulting PDF
- a live log of every scan and print request

Closing the window **hides it to the tray; it does not quit the agent** — otherwise scanning would
stop from the browser's point of view while the user believed they had merely closed a window.
Quitting is an explicit choice in the tray menu.

## Installing it as a program

What people download is a single .exe, and left alone it stays one: run from Downloads, absent from
the Start menu, and gone the day someone empties that folder. Installing fixes all three.

The first run offers it. You can also press **«تثبيت البرنامج على هذا الجهاز»** in the window, or run
`archiving-scan-agent.exe --install` (add `--silent` for unattended rollout; `install.bat` does this).

It is a per-user install — `%LOCALAPPDATA%` and `HKCU`, no elevation, for the same reason the login
entry is per-user. It:

- copies the program to `%LOCALAPPDATA%\Programs\ArchivingScanAgent`
- writes `agent.ico` there and points the shortcuts at it — the agent draws its icon at runtime
  rather than embedding one, so without this the shell would show the generic executable icon
- adds a Start-menu and a desktop shortcut
- registers under `HKCU\...\Uninstall\ArchivingScanAgent`, so it appears in **Settings ▸ Apps** with
  a working Uninstall button
- turns on run-at-login, pointing at the **installed** copy

Uninstalling (`--uninstall`, `uninstall.bat`, or Settings ▸ Apps) reverses all of it. A running agent
is closed first, and the folder is removed by a detached shell — a program cannot delete itself while
it is running.

Re-running `--install` over an existing install is how you update it.

**A build from source is not a single file.** It is a stub .exe beside its runtime DLLs, so the
installer copies the whole output folder when it finds a `.runtimeconfig.json` next to the exe, and
just the one file otherwise. Copying only the .exe would produce an install that reports success and
then fails to start.

## Run at Windows startup

Tick the checkbox in the window. It writes:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    ArchivingScanAgent = "<path>\archiving-scan-agent.exe" --background
```

HKCU rather than a service or an HKLM entry, deliberately: the agent brokers a scanner attached to one
person's desk and listens only on their loopback interface, so it is per-user by nature — and HKCU
needs no administrator rights, which means an ordinary employee can actually use the checkbox.
`--background` starts it straight to the tray so signing in doesn't throw a window in your face.

The path is always the **installed** copy when there is one, never whichever file happens to be
running. Ticking the box while running the download would otherwise register a path inside Downloads,
and the agent would silently stop starting the morning someone cleared that folder.

If the agent is moved to a different folder, the stale entry is detected and repaired on next launch.

`install-startup.bat` writes the same registry value without installing anything, and
`uninstall-startup.bat` removes it — use them to control autostart alone; use `install.bat` /
`uninstall.bat` (above) for the program itself. Older installs put a shortcut in the Startup folder
instead; the scripts and the checkbox delete that shortcut, since having both launches it twice.

## What it serves

- `GET /status[?scanner=…]` →
  ```json
  { "status":"ok", "scanners":[…], "printers":[…], "mock":false,
    "capabilities": { "flatbed":true, "feeder":true, "duplex":false },
    "dpiOptions":[150,200,300,600], "colorModes":["blackwhite","grayscale","color"],
    "paperSizes":["a4","a5","letter","legal"], "maxPages":200 }
  ```
  `capabilities` is read from the device, so the web app can offer a duplex switch only to a
  scanner that actually has one instead of a fixed menu half the drivers reject.

- `POST /scan` with JSON — **every field optional**:

  | field | values | default |
  |---|---|---|
  | `format` | `pdf` · `jpeg` | `pdf` |
  | `scanner` | device name | first scanner |
  | `dpi` | 50–1200 | `200` |
  | `color` | `blackwhite` · `grayscale` · `color` | `color` |
  | `paper` | `a4` · `a5` · `letter` · `legal` · `none` | `a4` |
  | `source` | `flatbed` · `feeder` | `flatbed` |
  | `duplex` | bool (feeder only) | `false` |
  | `maxPages` | 1–200 | 200 |
  | `preview` | bool | `false` |

  → the scan as a binary `application/pdf` (**multi-page**) or `image/jpeg` body, with the page
  count in the **`X-Scan-Pages`** response header. A caller posting only `{"format":"pdf"}` gets
  exactly the original single-page flatbed behaviour.

  `preview: true` shows the agent's review window first — drop a blank page, rotate a sheet fed
  upside down, re-scan one page, or add more — and only the approved set is returned. The request
  is held open meanwhile; cancelling answers **499**. Ignored in `--console` mode, which has no UI
  to show and must not hang the caller.

- `POST /print?printer=…&ext=pdf` with the file bytes as the body → `{ "status":"printed", pages }`

Scanning uses **Windows Image Acquisition (WIA)** — the standard, driver-agnostic Windows scanning API
(no vendor SDK or license required). Property support varies wildly between drivers, so dpi, colour
mode, paper size and duplex are all written best-effort: a scanner that refuses 600 dpi still
returns a scan at whatever it does support rather than failing the request.

### Multi-page and the feeder

With `source: "feeder"` the agent keeps pulling sheets until the ADF reports empty
(`WIA_ERROR_PAPER_EMPTY`), the feed-ready bit clears, or `maxPages` is reached, and assembles every
page into one PDF. `duplex: true` makes the driver return both sides, so a 3-sheet duplex batch
arrives as a 6-page PDF. `format: "jpeg"` cannot hold multiple pages — it returns the first, and
`X-Scan-Pages` tells the caller what it did not get.

## Build & run

```bash
dotnet build tools/scan-agent      # or: dotnet publish -c Release -r win-x64 --self-contained
```

```
archiving-scan-agent               tray icon + window
archiving-scan-agent --background  start straight to the tray (what the login entry runs)
archiving-scan-agent --console     no UI; logs to the terminal (the pre-UI behaviour, for scripts)
archiving-scan-agent --mock        no scanner needed; returns a sample page. Combines with the above.
```

Only one agent runs per signed-in *session* — the single-instance mutex is `Local\`-scoped, so each
Remote Desktop user gets their own. A second launch inside the same session says so and exits.

```
archiving-scan-agent --app-url=https://archive.example.com   set the address used for pairing links
archiving-scan-agent --port=8770                             pin the listener instead of searching 8765..8784
```

## Remote Desktop / Terminal Server

When people reach the system by signing in to a shared Windows server over RDP, the browser runs
*inside* that server, so `127.0.0.1` is the server — not the PC on their desk. That changes two
things, and the agent handles both.

**Several agents, one loopback.** Loopback is machine-wide, not per-session: every RDP session on the
host shares one 127.0.0.1 and one TCP port space. So the agent takes the first free port in
**8765–8784** rather than insisting on 8765, and the web app finds it by probing that range instead
of assuming an address. The range is also the ceiling on simultaneous sessions.

**Pairing.** Because they all share loopback, one session's page can reach another session's agent,
and neither can tell who is who by address. So on a shared host the agent requires a per-user token
before it will list, scan, or print — a request without it gets `401`, and `/status` answers
`authorized: false` with empty device lists so the web app can say *pair this browser* rather than
*no scanner found*.

The token gets into the right browser by the agent opening the app from inside the user's own
session: the browser Windows launches there runs under that user's profile, so what it stores in
`localStorage` is readable only by that user's pages. Press **«ربط المتصفح»** in the agent window, or
let it happen automatically on first run when the address is already configured. The link is
`{app}/scan-agent/connect#token=…`; the token rides in the *fragment*, which browsers never send to
the server, so pairing cannot leave the secret in an access log. **«نسخ الرمز»** plus the paste box
in scanner settings is the fallback for a browser that will not launch from the agent.

Set the address once at rollout so nobody has to type it per session:

```
archiving-scan-agent.exe --install --silent --app-url=https://archive.example.com
```

The token lives in `%APPDATA%\ArchivingScanAgent\settings.json` — per-user, so each session gets its
own, and a roaming profile carries the pairing to whichever server the user lands on. **«إصدار رمز
جديد»** revokes every browser paired with the old one.

**On an ordinary single-user PC none of this appears.** The agent gets 8765, reports
`requiresToken: false`, demands no token, and the pairing panel stays hidden — exactly the behaviour
earlier builds had.

### What RDP does and does not redirect

This is the part no amount of agent code can change, and it is worth knowing before deploying:

| Device | In an RDP session |
|---|---|
| **Printers** | Redirected by default. The user's local printers appear in the session (often named `… (redirected 2)`) and the agent enumerates them normally. |
| **Scanners** | **Not redirected.** Plain RDP has no TWAIN/WIA redirection, so the agent sees only scanners attached to the *server*. |

To scan from a device on the user's own desk over RDP you need one of:

- **RemoteFX USB redirection** — RDS plus a group-policy setting, which passes the USB scanner
  through so WIA sees it in the session. The supported route, and what most sites use.
- **A network scanner** reachable from the server, installed there with a WIA driver.
- **Scan to a redirected drive or share** from the manufacturer's own software on the local PC, then
  upload the file. The web app already falls back to a file picker when no scanner is found, and the
  document is still flagged as scanned.

## Pointing the web app at the agent
The SPA probes `http://127.0.0.1:8765` through `:8784` and uses the agent that accepts its token
(or, on a single-user PC, the first that answers). It caches the port that worked, so the usual cost
is one request, not twenty.

Setting `VITE_SCAN_AGENT_URL` pins one address and turns discovery off entirely — which also disables
multi-session support, so use it only for a deployment that runs the agent somewhere unusual and
knows exactly where:
```
# frontend/.env.local
VITE_SCAN_AGENT_URL=http://127.0.0.1:8765
```

In the document detail page, **مسح ضوئي (Scan)** calls the agent; if the agent isn't detected, it falls
back to a file picker so the user can upload a file their scanner software produced (still flagged as
scanned). Scanner-only document types reject ordinary file uploads and require this scan path.

## Notes / limitations
- Real WIA acquisition needs a physical scanner with a Windows driver; it can't be exercised on a headless
  server. `--mock` verifies everything except the driver call — it renders numbered sample pages that
  honour the requested paper size, colour mode, page count and duplex, so multi-page batching is
  testable without hardware.
- Feeder/duplex behaviour depends entirely on the driver. The agent asks for what was requested and
  falls back silently when a property is refused, so a device may return fewer pages, a different
  resolution, or single-sided output without reporting an error.
- Blank-page removal and deskew are not implemented; the preview window is the manual equivalent.
- At most 20 concurrent sessions per host — the width of the 8765..8784 port range. Past that the
  agent reports the host is full rather than starting without a port.
- Scanners are not redirected by plain RDP; see the Remote Desktop section for the options.
- For production, sign the executable and consider scoping CORS to your exact cloud origin (the agent
  currently echoes the request `Origin`).
