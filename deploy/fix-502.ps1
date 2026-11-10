<#
.SYNOPSIS
  Diagnoses — and where it safely can, repairs — a 502 from the IIS site in front of the Archiving
  service. Run it ON THE SERVER, as administrator (double-click Fix-502.cmd).

.DESCRIPTION
  502 from this deployment always means one thing: IIS is alive and answered you, but the service
  behind it did not answer IIS. IIS is never the culprit — it is the messenger. So this script walks
  the chain from the public port inward and reports the first link that is broken:

      browser ─► IIS site (public port) ─► web.config rewrite ─► 127.0.0.1:<internal> ─► service ─► DB

  The four causes, in the order they actually happen:

    1. the service is stopped or crashed          → IIS gets a refused connection  → 502.3
    2. web.config points at a port the service    → same, and the service looks     → 502.3
       no longer listens on (someone re-ran the     perfectly healthy, which is why
       installer with a different -Port)            this one is so confusing
    3. the URL reservation for the internal port  → the service starts, fails to bind, exits
       is missing or owned by another account
    4. the database is unreachable                → the service exits during startup

  It repairs 1 and 2 by itself when -Fix is given (start the service; rewrite web.config to the port
  the service really listens on). It never touches the database or the certificates.

.PARAMETER Fix
  Apply the safe repairs instead of only reporting them.

.PARAMETER SiteName
  The IIS site to inspect. Default: the site whose binding matches -PublicPort, else 'Archiving'.

.PARAMETER PublicPort
  The public port the browser uses. Default 5080 — the port this deployment was reported on.

.EXAMPLE
  .\fix-502.ps1                 # report only
  .\fix-502.ps1 -Fix            # report and repair
  .\fix-502.ps1 -PublicPort 80 -Fix
#>
[CmdletBinding()]
param(
  [switch] $Fix,
  [string] $SiteName,
  [int] $PublicPort = 5080
)

$ErrorActionPreference = 'Continue'
$ServiceName = 'Archiving'
$appcmd = Join-Path $env:windir 'system32\inetsrv\appcmd.exe'
$problems = @()
$repairs = @()

function Say  { param([string] $t) Write-Host $t }
function Head { param([string] $t) Write-Host ''; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok   { param([string] $t) Write-Host "  [ok]   $t" -ForegroundColor Green }
function Bad  { param([string] $t) Write-Host "  [BAD]  $t" -ForegroundColor Red; $script:problems += $t }
function Note { param([string] $t) Write-Host "  [ .. ] $t" -ForegroundColor Yellow }

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Write-Warning 'Not elevated — service and IIS checks will be incomplete. Run Fix-502.cmd instead.'
}

Write-Host ''
Write-Host '  Archiving — 502 diagnosis' -ForegroundColor White
Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm')  ·  $env:COMPUTERNAME" -ForegroundColor DarkGray

# ── 1. the service ─────────────────────────────────────────────────────────────────────────────
Head '1. the Windows service'
$svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
$wmi = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
$installFolder = $null

if (-not $svc) {
  Bad "the service '$ServiceName' is not installed on this machine"
} else {
  Say "  state: $($svc.Status)   startup: $($wmi.StartMode)   account: $($wmi.StartName)"
  if ($svc.Status -ne 'Running') {
    Bad "the service is $($svc.Status) — this alone produces 502 on every request"
    if ($Fix) {
      Note 'starting it…'
      try {
        Start-Service $ServiceName -ErrorAction Stop
        $svc.WaitForStatus('Running', '00:00:45')
        Ok 'service started'
        $repairs += 'started the service'
      } catch {
        Bad "could not start it: $($_.Exception.Message)"
        Note 'the reason is almost always in the event log — see section 6'
      }
    }
  } else { Ok 'running' }

  if ($wmi -and $wmi.PathName) {
    $dir = Split-Path -Parent ($wmi.PathName.Trim('"'))
    if (Test-Path (Join-Path $dir 'appsettings.Production.json')) { $installFolder = $dir }
  }
}
if ($installFolder) { Say "  installed in: $installFolder" }
else { Bad 'could not locate the installation folder from the service registration' }

# ── 2. the port the service listens on ─────────────────────────────────────────────────────────
Head '2. the internal port'
$internal = 0
if ($installFolder) {
  $file = Join-Path $installFolder 'appsettings.Production.json'
  try {
    $settings = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
    Say "  Urls = $($settings.Urls)"
    if ($settings.Urls -match ':(\d+)') { $internal = [int]$Matches[1] }
  } catch { Bad "could not read $file : $($_.Exception.Message)" }
}
if ($internal) { Ok "the service is configured for port $internal" }
else { Bad 'no port found in appsettings.Production.json (Urls)' }

# Is anything actually listening there? A configured port and a bound port are different claims.
if ($internal) {
  $listening = @(Get-NetTCPConnection -State Listen -LocalPort $internal -ErrorAction SilentlyContinue)
  if ($listening.Count -gt 0) {
    $owner = (Get-Process -Id $listening[0].OwningProcess -ErrorAction SilentlyContinue)
    Ok "port $internal is bound by $($owner.ProcessName) (pid $($owner.Id))"
    if ($owner -and $owner.ProcessName -notmatch 'Archiving') {
      Bad "port $internal is held by $($owner.ProcessName), NOT by the Archiving service — a port conflict"
    }
  } else {
    Bad "nothing is listening on 127.0.0.1:$internal — this is exactly what IIS reports as 502"
  }
}

# ── 3. does the service answer? ────────────────────────────────────────────────────────────────
Head '3. the service, asked directly (bypassing IIS)'
if ($internal) {
  $url = "http://127.0.0.1:$internal/api/public/stats"
  try {
    $r = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 10
    Ok "$url -> $($r.StatusCode)  — the service itself is healthy, so the fault is between IIS and it"
  } catch {
    Bad "$url did not answer: $($_.Exception.Message)"
    Note 'if the service is Running but this fails, it started and then broke — see section 6'
  }
}

# ── 4. the IIS site and its rewrite rule ───────────────────────────────────────────────────────
Head '4. the IIS site'
$sitePath = $null
$ruleTarget = 0
if (-not (Test-Path $appcmd)) {
  Bad 'appcmd.exe not found — IIS does not appear to be installed here'
} else {
  $sites = & $appcmd list sites
  $sites | ForEach-Object { Say "  $_" }

  if (-not $SiteName) {
    $match = $sites | Where-Object { $_ -match ":${PublicPort}:" }
    if ($match -and $match -match '^SITE "([^"]+)"') { $SiteName = $Matches[1] }
    elseif ($sites -match '^SITE "Archiving"') { $SiteName = 'Archiving' }
  }
  if (-not $SiteName) {
    Bad "no IIS site is bound to port $PublicPort — is the 502 coming from a different machine or a proxy in front of this one?"
  } else {
    Say "  inspecting site: $SiteName"
    $state = ($sites | Where-Object { $_ -match "^SITE `"$([regex]::Escape($SiteName))`"" }) -replace '.*state:([A-Za-z]+).*', '$1'
    if ($state -eq 'Started') { Ok 'site is started' } else { Bad "site state is $state" }

    $vdir = & $appcmd list vdir /app.name:"$SiteName/"
    if ($vdir -match 'physicalPath:(.+?)\)') {
      $sitePath = [Environment]::ExpandEnvironmentVariables($Matches[1].Trim())
      Say "  folder: $sitePath"
    }

    # ARR must be installed AND switched on; installed is not the same as forwarding.
    $proxy = & $appcmd list config -section:system.webServer/proxy 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $proxy) {
      Bad 'Application Request Routing is not installed — the rewrite rule cannot forward anywhere'
    } elseif ($proxy | Select-String -SimpleMatch 'enabled="true"' -Quiet) {
      Ok 'ARR proxy is enabled'
    } else {
      Bad 'ARR proxy is installed but DISABLED (usually shows as 403.14 or 500, sometimes 502)'
      if ($Fix) {
        & $appcmd set config -section:system.webServer/proxy /enabled:"True" /commit:apphost | Out-Null
        Ok 'ARR proxy enabled'; $repairs += 'enabled the ARR proxy'
      }
    }
  }
}

if ($sitePath) {
  $cfg = Join-Path $sitePath 'web.config'
  if (-not (Test-Path $cfg)) {
    Bad "no web.config in $sitePath — nothing forwards to the service"
  } else {
    $xml = Get-Content $cfg -Raw
    if ($xml -match 'url="http://127\.0\.0\.1:(\d+)/') {
      $ruleTarget = [int]$Matches[1]
      Ok "the rewrite rule forwards to 127.0.0.1:$ruleTarget"
    } elseif ($xml -match 'url="http://localhost:(\d+)/') {
      $ruleTarget = [int]$Matches[1]
      Bad "the rule targets localhost:$ruleTarget — on a machine with IPv6 that resolves to [::1], and the service listens on 127.0.0.1 only, so every request is refused. Use 127.0.0.1."
    } else {
      Bad 'no rewrite rule found in web.config'
    }
  }
}

# ── 5. the mismatch that looks like nothing is wrong ───────────────────────────────────────────
Head '5. do IIS and the service agree on the port?'
if ($internal -and $ruleTarget) {
  if ($internal -eq $ruleTarget) {
    Ok "both say $internal"
  } else {
    Bad "MISMATCH — the service listens on $internal, IIS forwards to $ruleTarget"
    if ($Fix -and $sitePath) {
      $cfg = Join-Path $sitePath 'web.config'
      $xml = Get-Content $cfg -Raw
      $xml = $xml -replace 'url="http://(?:127\.0\.0\.1|localhost):\d+/', "url=`"http://127.0.0.1:$internal/"
      Set-Content -Path $cfg -Value $xml -Encoding UTF8
      Ok "web.config now forwards to 127.0.0.1:$internal"
      $repairs += "repointed web.config from $ruleTarget to $internal"
    }
  }
} else { Note 'not enough information to compare' }

# ── 6. why the service died, in its own words ──────────────────────────────────────────────────
Head '6. the last words of the service (event log)'
$events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddDays(-2) } -ErrorAction SilentlyContinue |
  Where-Object { $_.ProviderName -match 'Archiving|\.NET Runtime|Application Error' } |
  Select-Object -First 6
if ($events) {
  foreach ($e in $events) {
    Say ("  {0:yyyy-MM-dd HH:mm}  [{1}]  {2}" -f $e.TimeCreated, $e.LevelDisplayName,
      ($e.Message -split "`n")[0].Trim())
  }
  if ($events.Message -match 'Access is denied|address already in use|urlacl|HTTP\.sys') {
    Bad 'the service could not bind its port — the URL reservation is missing or owned by another account'
    Note "fix: netsh http add urlacl url=http://127.0.0.1:$internal/ user=`"$($wmi.StartName)`""
  }
  if ($events.Message -match 'MySql|SqlException|Unable to connect to any of the specified MySQL hosts|A network-related') {
    Bad 'the service could not reach its database — it starts, fails, and exits, and IIS then answers 502'
    Note 'check the database server is running and the connection string in appsettings.Production.json'
  }
} else { Note 'nothing from the service in the Application log in the last 2 days' }

# ── 7. end to end ──────────────────────────────────────────────────────────────────────────────
Head '7. the whole chain, through IIS'
try {
  $r = Invoke-WebRequest "http://127.0.0.1:$PublicPort/api/public/stats" -UseBasicParsing -TimeoutSec 12
  Ok "http://127.0.0.1:$PublicPort -> $($r.StatusCode)  — the 502 is gone"
} catch {
  $code = $_.Exception.Response.StatusCode.value__
  if ($code) { Bad "still $code through IIS" } else { Bad "no answer through IIS: $($_.Exception.Message)" }
}

# ── verdict ────────────────────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
if ($repairs.Count -gt 0) {
  Write-Host 'REPAIRED:' -ForegroundColor Green
  $repairs | ForEach-Object { Write-Host "  · $_" }
}
if ($problems.Count -eq 0) {
  Write-Host 'No fault found in the chain.' -ForegroundColor Green
} else {
  Write-Host 'FINDINGS (first one is usually the whole story):' -ForegroundColor Yellow
  $problems | ForEach-Object { Write-Host "  · $_" }
  if (-not $Fix) {
    Write-Host ''
    Write-Host 'Re-run with -Fix to apply the safe repairs (start the service, align the port).' -ForegroundColor White
  }
}
Write-Host '════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
Write-Host ''
