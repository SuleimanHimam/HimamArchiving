# نشر النظام على Windows Server — Deploying on Windows Server

> **بالعربية:** دليل التنصيب خطوة بخطوة مع الربط بـ SQL Server في [README.ar.md](README.ar.md).

The package is one folder: the API, the built SPA and these scripts. It is **self-contained** —
the server needs no .NET runtime installed. One Windows Service serves the pages and the API on a
single port, so nothing has to be put in front of it.

---

## 1. Build the package (on a machine with the source)

```powershell
cd <repo>\deploy
.\build-package.ps1        # folder + zip
.\build-setup.ps1          # …and a single Archiving-Setup.exe carrying it
```

Produces `<repo>\dist\Archiving\` and `<repo>\dist\Archiving_<date>.zip`.

`build-setup.ps1` additionally compiles `<repo>\dist\Archiving-Setup.exe`: the whole package
embedded in a small WinForms front end, with the app's icon and an administrator manifest. Copy
that one file to the server and double-click it — it asks where to install, unpacks, registers the
`Archiving` Windows Service, and hands over to the same wizard.
`Archiving-Setup.exe /S "D:\Archiving"` does the same silently for unattended rollouts. It builds
with the C# compiler that ships inside Windows, so no Inno Setup, WiX or extra SDK is needed.

The service is registered **stopped**: a fresh install has no `appsettings.Production.json` yet, so
there is no database for it to reach. `install-service.ps1` — which the wizard runs, and which you
run yourself after `/S` — writes the settings, reconfigures that same service and starts it. What
setup guarantees is that the machine is never left with a folder of files and nothing in
`services.msc`.

Requires the .NET 10 SDK and Node 20+. It builds the SPA, publishes the API as
`win-x64 / self-contained`, copies the SPA into `wwwroot`, and drops the dev `appsettings` —
that file carries the development database password and signing key and must never ship.

## 2. Install on the server

Copy the folder (or unzip it) to the server — `C:\Archiving` is a good home.

**No command line:** double-click **`Install.cmd`**. It asks for administrator rights, then opens a
window with the SQL Server instance (a dropdown, filled from the instances found on the machine),
the database name, the authentication mode, the storage folder and the port — plus a **Test
connection** button that checks the server before anything is changed. It is a form over
`install-service.ps1`; everything below happens the same way.

**From a command line**, **as administrator**:

```powershell
cd C:\Archiving

# SQL Server on the same machine, Windows authentication:
.\install-service.ps1 -SqlServer '.\SQLEXPRESS' -StoragePath 'D:\ArchivingData\storage'

# A separate database server with a SQL login:
.\install-service.ps1 -SqlServer 'SQL01' -Database 'Archiving' `
                      -SqlUser 'archiving' -SqlPassword '…' `
                      -ServiceAccount 'DOMAIN\svc_archiving' `
                      -StoragePath 'D:\ArchivingData\storage'

# Staying on MySQL:
.\install-service.ps1 -Provider MySql -ConnectionString 'server=localhost;port=3306;database=archiving_db;uid=archiver;pwd=…;SslMode=Preferred;'
```

The script writes `appsettings.Production.json`, **generates a fresh JWT signing key and storage
encryption key** for this installation, creates the database if it is missing, grants the service
account access, registers the service (auto-start, restart on failure), opens the firewall port
and starts it. Running it again updates an existing install instead of failing.

Then open `http://<server>:7070/` and sign in as `admin` / `Admin@12345` —
**change that password immediately.**

### Accounts created on first start

A first start seeds the five departments the system ships with (الموارد البشرية · المالية ·
تقنية المعلومات · الشؤون الإدارية · قسم الأرشيف) and **a head for each**, because every scoping
rule in the system is written in terms of a department and the person who heads it.

| Account | Password |
|---|---|
| `admin` | `Admin@12345` |
| `hr.manager` · `finance.manager` · `it.manager` · `admin.affairs.manager` · `archive.manager` | `Manager@12345` |

The sign-in box takes either the short name (`admin`) or the full address
(`admin@archiving.local`); the short form is accepted as long as it names exactly one account.

All six are **the same on every install**, and the five heads hold the Manager role. Change every
password, or deactivate the heads you are not using, before the server is reachable by anyone else.
The `demo.*` accounts are development-only and are not created in a production install.

## 3. Afterwards

| | |
|---|---|
| Restart | `Restart-Service Archiving` |
| Logs | `Get-EventLog -LogName Application -Source Archiving -Newest 20` |
| Update | unzip the new package somewhere, double-click **`Update.cmd`** inside it (or `.\update-service.ps1`) |
| Remove | double-click **`Uninstall.cmd`** in the installation folder — leaves the database, the files and the settings |

The schema is applied by the service itself on start, so an update that adds tables needs no
separate migration step.

### The scan agent

Browsers cannot reach a USB scanner, so scanning runs through a small agent on the user's own PC
(`tools\archiving-scan-agent.exe`, one self-contained .exe). It ships inside the package, and the
installer points `Downloads:ScanAgentPath` at it — the system then offers it at
`/api/downloads/scan-agent`, so staff get it from the system itself rather than from a link
someone mailed around. `build-package.ps1 -SkipScanAgent` leaves it out (it is ~60 MB) for a
deployment where nobody scans.

### On the desktop, as a program

The install puts **«الديوان الإلكتروني»** on the server's Public desktop. For staff PCs, copy
`Create-Desktop-Shortcut.cmd`, `create-shortcut.ps1` and `app-icon.ico` over and double-click the
first one; it asks for the server address and needs no administrator rights (`-AllUsers` does, and
writes to the Public desktop instead).

The shortcut opens the system in **its own window — no address bar, no tabs, its own taskbar
icon**, via the browser's application mode (`--app=`). That is the same window a PWA install gives,
and it is here because the in-app «تثبيت» button *cannot* appear on a LAN address served over plain
HTTP: browsers only offer installation on a secure origin. Once a TLS certificate is in front, the
in-app button becomes the better route — it adds Start-menu entries and the manifest's jump list.
With no Chromium browser on the machine the shortcut falls back to opening the default browser.

### Removing

`Uninstall.cmd`, from the **installation** folder. It removes what the install added to the
machine — the service, the URL reservation and firewall rule for this install's actual port, and
the desktop shortcut — and stops there.

The database, the documents and `appsettings.Production.json` are kept. Deleting a document archive
is not a decision a script should make, so instead it prints where all three are, by real path and
real database name read from your own settings, and the `DROP DATABASE` line if that is what you
want. Note that the folder holds the key the stored documents are encrypted with: delete it and a
restored backup cannot be opened.

Re-running `Install.cmd` afterwards brings the system back with its data intact.

**To delete the records too**, there is a separately named file: `Remove-Everything.cmd`
(`-RemoveAll` on the script). It deletes the documents, drops the database and deletes the
installation folder. Before touching anything it prints the three real paths and asks you to **type
the database name**; anything else cancels, and no switch skips that prompt — a destructive path
that a flag can wave through is one that eventually runs by accident. The service is removed first
(it would otherwise hold the files and the database open), the database is dropped after
`SINGLE_USER` so an open SSMS window cannot block it, and the folder is deleted by a detached
command a few seconds later, since a script cannot delete the folder it is running from.

### Updating

Run `Update.cmd` **from inside the new package**, not from the installation. It asks the service
where it is installed, backs up `appsettings.Production.json`, stops the service, copies the new
build over the old one and starts it again — then waits for the site to answer on whatever port
this install uses.

`appsettings.Production.json` is excluded from the copy: it holds the connection string, the
signing key and the key the stored documents are encrypted with, and a package must never bring its
own. Nothing is deleted from the installation folder either, so a storage folder left inside it
survives. `-FullBackup` copies the whole installation aside first; `-Yes` skips the prompt.

Take a database backup before updating — migrations run on the next start.

---

## Database

Both engines are supported from the same build. `Database:Provider` picks one (`SqlServer` or
`MySql`); leave it out and the connection string decides, so an existing MySQL install keeps
working with no config change.

Each engine has its own migration history — the schemas are generated from the same model but are
not interchangeable SQL — so **a database cannot be moved between MySQL and SQL Server by copying
a backup.** Moving an existing MySQL deployment to SQL Server means migrating the data
(SSMA for MySQL, or an export/import), not restoring a dump.

### Backups

Both are driven from إعدادات → النسخ الاحتياطي, and both work on either engine:

- **MySQL** shells out to `mysqldump` / `mysql` and streams a `.sql`.
- **SQL Server** runs `BACKUP DATABASE` and streams the resulting `.bak`. The engine writes that
  file itself, with the *SQL Server service account's* identity, on the *database server's*
  filesystem — so the folder in `Backup:SqlServerDirectory` (or the instance default) must be
  writable by SQL Server and readable by the API. On a single-box install that is automatic; with
  a separate database server, point both at a share they can each reach.

`BACKUP … WITH COPY_ONLY` is used, so downloading a copy never disturbs an existing backup chain.
Restore takes the database to `SINGLE_USER` briefly, which disconnects the API; it reconnects on
its own.

**Back up the storage folder too.** Documents live on disk, encrypted with
`Storage:EncryptionKey`; the database holds only their metadata. A database backup alone restores
an index of files you cannot open, and the key alone restores nothing.

## Session security

A signed-in screen left unattended signs itself out. After **15 minutes** with no real input
(mouse, keys, touch, scroll — background polling and re-renders deliberately do not count) a
warning appears and counts down **60 seconds** before the session ends: client state cleared, the
refresh token revoked through `POST /api/auth/logout`, and the login screen saying why. Open tabs
are kept in step through a `BroadcastChannel` — work in one tab keeps the others alive, and an
expiry closes all of them.

Both durations are build-time frontend config (see `frontend/.env.example`); rebuild the package
after changing them:

| Variable | Default | Meaning |
|---|---|---|
| `VITE_IDLE_TIMEOUT_MS` | `900000` (15 min) | quiet time before the warning shows |
| `VITE_IDLE_WARNING_MS` | `60000` (60 s) | countdown before the session ends |
| `VITE_CONFIRM_ON_CLOSE` | `standalone` | ask before closing: `standalone` (desktop-program window only) · `always` · `never` |

Closing the desktop-shortcut window ends the session — the token is per-window — so that window
asks for confirmation first. The wording is the browser's own ("Leave site?"); a page cannot supply
custom text for that prompt, and it also appears on a manual reload, because a page cannot tell a
reload from a close. Ordinary browser tabs are deliberately left alone: a prompt on every tab close
of a system people keep open all day is one they learn to dismiss without reading.

The token is held in **sessionStorage**, not localStorage, so genuinely closing the app — or the
OS killing its process, which Android does routinely to installed PWAs and home-screen shortcuts —
lands on the login screen instead of silently resuming. Ordinary reloads and in-app navigation are
unaffected. This is not airtight on its own: Chrome deliberately restores sessionStorage into tabs
it resurrects through its own crash-recovery. What bounds that case is `Jwt:AccessTokenMinutes`
(60) — kept short enough that a long absence invalidates the token, and far above the idle window
so it never interrupts someone who is working.

## Security checklist before going live

- [ ] Change the `admin@archiving.local` password **and the five department heads'** — they all
      ship with the same known default (see *Accounts created on first start*).
- [ ] Keep `appsettings.Production.json` out of source control — it holds both keys and the
      database credentials. Back it up somewhere the storage folder's backup does not live.
- [ ] Put a real TLS certificate in front (IIS or a reverse proxy) if the server is reachable
      beyond the LAN, and drop `TrustServerCertificate=true` once the SQL Server instance has a
      trusted certificate.
- [ ] Give the service account only what it needs: `db_owner` on this database, write access to
      the storage folder, nothing else.

## Behind IIS (optional)

The service is a complete web server; IIS is only worth adding for TLS termination, host headers or
a policy that all web traffic goes through IIS. IIS cannot host this application directly, and it
**cannot share the service's port** — pointing a site at a port the service already holds fails with
*"The process cannot access the file because it is being used by another process"*, because Kestrel
holds a `http.sys` URL reservation on it that stopping the service does not release.

**No command line:** double-click **`Setup-IIS.cmd`**. It checks IIS, URL Rewrite and ARR (offering
the download pages for whichever is missing), enables the ARR proxy, moves the service to a
loopback-only internal port while releasing its reservation on the public one, creates the site, app
pool and `web.config`, opens the firewall, and then verifies the whole chain answers. **تراجع** undoes
all of it and puts the service back on the public port.

It also raises IIS's request limit, which is the classic way an archive breaks behind IIS: the
default is 30 MB, and everything looks fine until someone uploads a larger document and gets an
opaque `404.13`.

By hand it is the same four things — install **URL Rewrite** and **ARR**, enable *Server Proxy
Settings → Enable proxy*, move the service (`.\install-service.ps1 -Port 5080`), and add a site
whose `web.config` rewrites `(.*)` to `http://127.0.0.1:5080/{R:1}` with a raised
`maxAllowedContentLength`.

Note the app does not read `X-Forwarded-*` headers, so terminating TLS at IIS leaves it unaware the
request arrived over HTTPS. The SPA uses relative paths, so this is usually invisible.
