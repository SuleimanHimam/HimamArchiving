<#
.SYNOPSIS
  Updates an existing installation in place: stops the service, copies the new build over it,
  starts it again. Settings, documents and the database are left alone.

.DESCRIPTION
  Run this from inside the NEW package folder, as administrator. It finds where the service is
  installed by asking the service itself, so there is nothing to type in the normal case.

  What it never touches:
    · appsettings.Production.json — the connection string and this install's two secrets
    · the storage folder and the database — an update changes code, not records

  New database migrations are applied by the service itself when it starts, so there is no
  separate migration step; take a database backup first all the same.

.EXAMPLE
  .\update-service.ps1
  .\update-service.ps1 -TargetPath 'D:\Archiving' -Yes
#>
[CmdletBinding()]
param(
  # Where the system is installed. Detected from the registered service when omitted.
  [string] $TargetPath,
  [string] $ServiceName = 'Archiving',
  # Copy the whole current installation aside before overwriting it (~150 MB), so a bad update
  # can be undone by copying the folder back.
  [switch] $FullBackup,
  # Skip the confirmation prompt (for unattended runs).
  [switch] $Yes
)

$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  throw "Run this script from an elevated PowerShell (Run as administrator)."
}
if (-not (Test-Path (Join-Path $source 'Archiving.Api.exe'))) {
  throw "Archiving.Api.exe not found in $source — run this from inside the NEW package folder."
}

# ── where is it installed ─────────────────────────────────────────────────────
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $TargetPath) {
  if (-not $svc) {
    throw "Service '$ServiceName' is not installed. This is an update — run install-service.ps1 (or Install.cmd) for a first install."
  }
  # The service's own binPath is the only authority on where the running copy lives; a folder
  # someone remembers is not.
  $binPath = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").PathName
  $exePath = ($binPath -replace '^"([^"]+)".*$', '$1').Trim('"')
  $TargetPath = Split-Path -Parent $exePath
}
$TargetPath = (Resolve-Path $TargetPath).Path

if (-not (Test-Path (Join-Path $TargetPath 'Archiving.Api.exe'))) {
  throw "No installation found at $TargetPath (Archiving.Api.exe is missing)."
}
if ($TargetPath -eq (Resolve-Path $source).Path) {
  throw "The new package and the installation are the same folder ($TargetPath) — nothing to update."
}

$settingsPath = Join-Path $TargetPath 'appsettings.Production.json'
if (-not (Test-Path $settingsPath)) {
  throw "appsettings.Production.json is missing from $TargetPath — that folder is not a completed installation."
}

# The port this install actually runs on, for the health check at the end.
$port = 7070
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if ($settings.Urls -match ':(\d+)') { $port = [int]$Matches[1] }

Write-Host ""
Write-Host "  new build : $source"
Write-Host "  installed : $TargetPath"
Write-Host "  service   : $ServiceName ($(if ($svc) { $svc.Status } else { 'not installed' }))"
Write-Host "  port      : $port"
Write-Host ""
Write-Host "  Settings, documents and the database are left as they are." -ForegroundColor Yellow
Write-Host "  Take a database backup first: new migrations are applied on start." -ForegroundColor Yellow
Write-Host ""

if (-not $Yes) {
  $answer = Read-Host "Continue? [y/N]"
  if ($answer -notmatch '^(y|yes)$') { Write-Host "cancelled."; return }
}

# ── backup ────────────────────────────────────────────────────────────────────
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
# The settings file is the one thing that cannot be rebuilt from the package: it holds the
# signing key and the key the stored documents are encrypted with.
$settingsBackup = Join-Path $TargetPath "appsettings.Production.json.bak_$stamp"
Copy-Item $settingsPath $settingsBackup -Force
Write-Host "settings backed up -> $(Split-Path -Leaf $settingsBackup)"

if ($FullBackup) {
  $full = Join-Path (Split-Path -Parent $TargetPath) ("{0}_backup_{1}" -f (Split-Path -Leaf $TargetPath), $stamp)
  Write-Host "copying the current installation to $full ..."
  robocopy $TargetPath $full /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "backup copy failed (robocopy $LASTEXITCODE)" }
  Write-Host "full backup done."
}

# ── stop ──────────────────────────────────────────────────────────────────────
if ($svc -and $svc.Status -ne 'Stopped') {
  Write-Host "stopping '$ServiceName' ..."
  Stop-Service $ServiceName -Force
  $svc.WaitForStatus('Stopped', '00:00:60')
  # The executable keeps its file lock for a moment after the service reports Stopped; copying
  # into it too early fails with "used by another process".
  Start-Sleep -Seconds 3
}

# ── copy ──────────────────────────────────────────────────────────────────────
Write-Host "copying the new build ..."
# /XF: this install's settings must survive. Nothing is deleted from the target — a file the new
# build no longer ships is harmless, whereas /MIR would take the storage folder with it if someone
# left it inside the installation.
$exclude = @('appsettings.Production.json', 'appsettings.Production.json.bak_*')
robocopy $source $TargetPath /E /NFL /NDL /NJH /NJS /R:2 /W:2 /XF @exclude | Out-Null
if ($LASTEXITCODE -ge 8) { throw "copy failed (robocopy $LASTEXITCODE) — the service is stopped; restore from the backup or re-run." }
Write-Host "files updated."

# ── start ─────────────────────────────────────────────────────────────────────
Write-Host "starting '$ServiceName' ..."
Start-Service $ServiceName
$ok = $false
$deadline = (Get-Date).AddSeconds(120)   # first start after an update also applies migrations
do {
  Start-Sleep -Seconds 3
  try {
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:$port/api/public/stats" -UseBasicParsing -TimeoutSec 5
    if ($r.StatusCode -eq 200) { $ok = $true; break }
  } catch { }
} while ((Get-Date) -lt $deadline)

Write-Host ""
if ($ok) {
  Write-Host "updated. http://$env:COMPUTERNAME`:$port/" -ForegroundColor Green
  Write-Host "the settings backup can be deleted once you are satisfied: $(Split-Path -Leaf $settingsBackup)"
} else {
  Write-Warning "the service did not answer within two minutes."
  Write-Warning "check why:  Get-EventLog -LogName Application -Source Archiving -Newest 20"
  Write-Warning "settings backup kept at: $settingsBackup"
}
Get-Service $ServiceName | Format-Table Name, Status, StartType -AutoSize

# netsh and robocopy report non-zero for things that are not failures here — a URL reservation that
# was already there, files that were copied. PowerShell hands the last native exit code back to the
# caller, so Install.cmd would announce a failed install after a perfectly good one. Say plainly
# how it went instead of letting the last tool that ran decide.
if ($ok) { exit 0 } else { exit 1 }
