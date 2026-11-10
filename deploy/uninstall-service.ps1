<#
.SYNOPSIS
  Removes the Archiving Windows Service. Leaves the database, the stored files and the settings
  alone — this uninstalls the service, it does not delete anyone's records.

.DESCRIPTION
  Run from inside the installation folder, as administrator. It removes what the install added to
  the machine — the service, the URL reservation, the firewall rule and the desktop shortcut — and
  then tells you exactly what is left and how to remove it, if removing the records is what you
  actually want. Deleting a document archive is not something a script should do on its own.

  With -RemoveAll it goes further and destroys the archive: the documents, the database and the
  installation folder. That path asks you to type the database name first, and no switch skips
  that — the whole point of the prompt is that it cannot be reached by an accidental double-click
  or a copied command line.

.EXAMPLE
  .\uninstall-service.ps1
  .\uninstall-service.ps1 -Yes
  .\uninstall-service.ps1 -RemoveAll        # destroys the archive; asks you to type the db name
#>
[CmdletBinding()]
param(
  [string] $ServiceName = 'Archiving',
  # Left at 0 the port is read from appsettings.Production.json — this install's actual port, which
  # is not necessarily the default. Assuming 8080 meant an install on any other port had its
  # firewall rule and URL reservation left behind, and a rule that was never there deleted instead.
  [int]    $Port = 0,
  [string] $ShortcutName = 'الديوان الإلكتروني',
  [switch] $Yes,
  # Also delete the documents, drop the database and delete this folder. Irreversible.
  [switch] $RemoveAll
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  throw "Run this script from an elevated PowerShell (Run as administrator)."
}

# ── read this install's own settings, so the summary names real paths ─────────
$settingsPath = Join-Path $PSScriptRoot 'appsettings.Production.json'
$storagePath = $null
$dbName = $null
$dbServer = $null
if (Test-Path $settingsPath) {
  $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
  if (-not $Port -and $settings.Urls -match ':(\d+)') { $Port = [int]$Matches[1] }
  $storagePath = $settings.Storage.RootPath
  $cs = $settings.ConnectionStrings.Default
  if ($cs -match '(?i)(?:database|initial catalog)\s*=\s*([^;]+)') { $dbName = $Matches[1].Trim() }
  if ($cs -match '(?i)(?:server|data source)\s*=\s*([^;]+)')       { $dbServer = $Matches[1].Trim() }
}
if (-not $Port) { $Port = 7070 }

Write-Host ""
Write-Host "  service  : $ServiceName"
Write-Host "  port     : $Port"
Write-Host "  folder   : $PSScriptRoot"
Write-Host ""
if ($RemoveAll) {
  Write-Host "  *** REMOVE EVERYTHING ***" -ForegroundColor Red
  Write-Host "  The service, and then the archive itself:" -ForegroundColor Red
  Write-Host "    documents : $(if ($storagePath) { $storagePath } else { '(not found in settings)' })" -ForegroundColor Red
  Write-Host "    database  : $(if ($dbName) { "[$dbName] on $dbServer" } else { '(not found in settings)' })" -ForegroundColor Red
  Write-Host "    folder    : $PSScriptRoot" -ForegroundColor Red
  Write-Host ""
  Write-Host "  This cannot be undone. Take a backup first if there is any doubt." -ForegroundColor Red
} else {
  Write-Host "  This removes the service, the firewall rule, the URL reservation and the" -ForegroundColor Yellow
  Write-Host "  desktop shortcut. The database, the documents and the settings are KEPT." -ForegroundColor Yellow
}
Write-Host ""

if ($RemoveAll) {
  # Typing the database name is the confirmation, and -Yes does not skip it. A destructive path
  # that any switch can wave through is one that eventually runs by accident.
  if (-not $dbName) { throw "Cannot use -RemoveAll: no database name in appsettings.Production.json — remove things by hand." }
  Write-Host "Type the database name to confirm destruction: " -NoNewline -ForegroundColor Red
  $typed = Read-Host
  if ($typed -ne $dbName) { Write-Host "'$typed' does not match '$dbName' — cancelled, nothing was touched."; exit 0 }
} elseif (-not $Yes) {
  $answer = Read-Host "Continue? [y/N]"
  if ($answer -notmatch '^(y|yes)$') { Write-Host "cancelled."; return }
}

# ── service ───────────────────────────────────────────────────────────────────
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
  if ($svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; $svc.WaitForStatus('Stopped', '00:00:60') }
  sc.exe delete $ServiceName | Out-Null
  Write-Host "service '$ServiceName' removed."
} else {
  Write-Host "service '$ServiceName' is not installed."
}

# ── what the install opened on the machine ────────────────────────────────────
netsh http delete urlacl url="http://+:$Port/" 2>&1 | Out-Null
Get-NetFirewallRule -DisplayName "Archiving (TCP $Port)" -ErrorAction SilentlyContinue |
  Remove-NetFirewallRule -ErrorAction SilentlyContinue
Write-Host "firewall rule and URL reservation for port $Port removed."

# The shortcut the install put on the Public desktop, and one the signed-in user may have made
# for themselves with Create-Desktop-Shortcut.cmd.
foreach ($dir in @([Environment]::GetFolderPath('CommonDesktopDirectory'),
                   [Environment]::GetFolderPath('Desktop'))) {
  $lnk = Join-Path $dir "$ShortcutName.lnk"
  if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host "shortcut removed: $lnk" }
}

# ── -RemoveAll: the archive itself ────────────────────────────────────────────
if ($RemoveAll) {
  Write-Host ""

  # 1. The documents.
  if ($storagePath -and (Test-Path $storagePath)) {
    $count = @(Get-ChildItem $storagePath -Recurse -File -ErrorAction SilentlyContinue).Count
    Remove-Item $storagePath -Recurse -Force
    Write-Host "documents deleted: $storagePath ($count files)"
  } elseif ($storagePath) {
    Write-Host "documents: $storagePath does not exist — nothing to delete."
  }

  # 2. The database. SINGLE_USER first: a connection still open from anywhere blocks the drop, and
  # after removing the service the likeliest one is an administrator's own SSMS window.
  if ($dbName) {
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($sqlcmd) {
      $sql = "IF DB_ID(N'$dbName') IS NOT NULL BEGIN ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$dbName]; END"
      $args = @('-S', $dbServer, '-C', '-b', '-Q', $sql)
      # Reuse whatever the connection string authenticates with, so a SQL-login install works too.
      if ($cs -match '(?i)user\s*id\s*=\s*([^;]+)' ) {
        $u = $Matches[1].Trim()
        $p = if ($cs -match '(?i)password\s*=\s*([^;]+)') { $Matches[1].Trim() } else { '' }
        $args += @('-U', $u, '-P', $p)
      } else { $args += '-E' }
      & sqlcmd @args | Out-Null
      if ($LASTEXITCODE -eq 0) { Write-Host "database dropped: [$dbName] on $dbServer" }
      else { Write-Warning "could not drop [$dbName] — drop it in SSMS." }
    } else {
      Write-Warning "sqlcmd not found — drop [$dbName] in SSMS yourself."
    }
  }

  # 3. This folder. It cannot delete itself while the script is running out of it, so hand the job
  # to a detached cmd that waits for this process to exit first.
  $cleanup = Join-Path $env:TEMP "archiving_cleanup_$(Get-Date -Format 'HHmmss').cmd"
  @"
@echo off
rem Waits for the uninstaller to exit, then removes the installation folder.
timeout /t 5 /nobreak >nul
rd /s /q "$PSScriptRoot"
del "%~f0"
"@ | Set-Content $cleanup -Encoding ascii
  Start-Process 'cmd.exe' -ArgumentList '/c', "`"$cleanup`"" -WindowStyle Hidden
  Write-Host "folder will be deleted moments after this window closes: $PSScriptRoot"

  Write-Host ""
  Write-Host "Everything removed. Staff PCs keep their own desktop shortcuts." -ForegroundColor Yellow
  exit 0
}

# ── what is left, and how to remove it if that is what you want ───────────────
Write-Host ""
Write-Host "Kept — remove these yourself only if you mean to destroy the archive:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. The documents:  $(if ($storagePath) { $storagePath } else { '(see Storage:RootPath in appsettings.Production.json)' })"
Write-Host "  2. The database:   $(if ($dbName) { "[$dbName] on $dbServer" } else { '(see the connection string)' })"
if ($dbName) {
  Write-Host "        DROP DATABASE [$dbName];   -- in SSMS, after taking a backup"
}
Write-Host "  3. This folder:    $PSScriptRoot"
Write-Host "        including appsettings.Production.json, which holds the key the stored"
Write-Host "        documents are encrypted with. Without it a restored backup cannot be opened."
Write-Host ""
Write-Host "Staff PCs keep their own desktop shortcuts — delete those on each machine." -ForegroundColor Yellow

# netsh and robocopy report non-zero for things that are not failures here — a URL reservation that
# was already there, files that were copied. PowerShell hands the last native exit code back to the
# caller, so Install.cmd would announce a failed install after a perfectly good one. Say plainly
# how it went instead of letting the last tool that ran decide.
exit 0
