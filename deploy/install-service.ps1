<#
.SYNOPSIS
  Installs the Archiving system on a Windows Server: writes its settings, creates the database,
  registers the Windows Service and starts it.

.DESCRIPTION
  Run this from inside the package folder, as administrator. It is safe to run again — it updates
  an existing installation rather than refusing.

.EXAMPLE
  # SQL Server on the same machine, Windows authentication (the service account connects):
  .\install-service.ps1 -SqlServer '.\SQLEXPRESS'

  # A named server and a SQL login:
  .\install-service.ps1 -SqlServer 'SQL01' -Database 'Archiving' -SqlUser 'archiving' -SqlPassword 'S3cret!'

  # MySQL instead:
  .\install-service.ps1 -Provider MySql -ConnectionString 'server=localhost;port=3306;database=archiving_db;uid=archiver;pwd=...;SslMode=Preferred;'
#>
[CmdletBinding()]
param(
  [ValidateSet('SqlServer', 'MySql')]
  [string] $Provider = 'SqlServer',

  # SQL Server instance, e.g. '.', '.\SQLEXPRESS', 'SQL01', 'SQL01\PROD'.
  [string] $SqlServer = '.',
  [string] $Database = 'Archiving',
  # Omit both to use Windows authentication as the service account.
  [string] $SqlUser,
  [string] $SqlPassword,

  # Full connection string, when the pieces above do not fit (MySQL, or an unusual SQL Server one).
  [string] $ConnectionString,

  [string] $ServiceName = 'Archiving',
  [string] $DisplayName = 'Archiving — الديوان الإلكتروني',
  [int]    $Port = 7070,
  # The account the service runs as. 'NT AUTHORITY\NetworkService' works for Windows auth against a
  # local SQL Server once the login is granted; a domain account is the usual choice for a remote one.
  [string] $ServiceAccount = 'NT AUTHORITY\NetworkService',
  # Where documents and attachments are stored. Must be on a volume with room to grow.
  [string] $StoragePath,
  [switch] $NoFirewallRule,

  <#
    The customer's signed licence file, copied into place as part of the install.

    Worth passing whenever the package carries a licence public key: without it the server starts,
    finds no licence, and refuses every write from the first minute — reads keep working, so it
    looks like a broken save rather than a missing licence.
  #>
  [string] $LicenseFile,

  <# Overrides the licence public key baked into the package. Rarely needed — it exists for
     re-keying an existing installation without rebuilding the package. #>
  [string] $PublicKey
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$exe = Join-Path $here 'Archiving.Api.exe'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  throw "Run this script from an elevated PowerShell (Run as administrator)."
}
if (-not (Test-Path $exe)) { throw "Archiving.Api.exe not found in $here — run this inside the package folder." }

# ── connection string ─────────────────────────────────────────────────────────
<#
  Did the caller actually say anything about the database? A re-run that only changes the port —
  `.\install-service.ps1 -Port 5080` — must keep the connection string that is already there.
  Without this, the defaults below would quietly rewrite a working install to point at a local
  default instance and a database named 'Archiving', and the service would come back up empty.
#>
$dbExplicit = $PSBoundParameters.ContainsKey('ConnectionString') -or $PSBoundParameters.ContainsKey('SqlServer') -or
              $PSBoundParameters.ContainsKey('Database') -or $PSBoundParameters.ContainsKey('SqlUser') -or
              $PSBoundParameters.ContainsKey('Provider')

if (-not $ConnectionString) {
  if ($Provider -eq 'SqlServer') {
    $auth = if ($SqlUser) { "User ID=$SqlUser;Password=$SqlPassword" } else { 'Integrated Security=true' }
    # TrustServerCertificate: the driver encrypts by default and a stock instance has a self-signed
    # certificate. Replace with a trusted certificate on a server that leaves the machine.
    $ConnectionString = "Server=$SqlServer;Database=$Database;$auth;TrustServerCertificate=true;MultipleActiveResultSets=true;"
  } else {
    throw "For -Provider MySql, pass -ConnectionString."
  }
}

# ── settings ──────────────────────────────────────────────────────────────────
$settingsPath = Join-Path $here 'appsettings.Production.json'
$previousPort = 0
if (Test-Path $settingsPath) {
  Write-Host "appsettings.Production.json exists — updating the connection string and provider only." -ForegroundColor Yellow
  $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
  # Remembered so a re-install on a different port can take the old port's reservation and
  # firewall rule down with it, instead of leaving one behind for every port ever tried.
  if ($settings.Urls -match ':(\d+)') { $previousPort = [int]$Matches[1] }
  if (-not $dbExplicit -and $settings.ConnectionStrings.Default) {
    $ConnectionString = $settings.ConnectionStrings.Default
    if ($settings.Database.Provider) { $Provider = $settings.Database.Provider }
    Write-Host "keeping the existing database settings — nothing about the database was passed." -ForegroundColor Yellow
  }
} else {
  $settings = Get-Content (Join-Path $here 'appsettings.Production.json.template') -Raw | ConvertFrom-Json
  # A fresh install gets its own signing key: the template's placeholder must never reach production,
  # because a shared key means anyone with the package can mint a valid token for this server.
  $bytes = New-Object byte[] 48
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  $settings.Jwt.Key = [Convert]::ToBase64String($bytes)

  # The at-rest key is NOT generated here any more. It used to be written into this file as
  # Storage:EncryptionKey, which meant the master key for the whole archive sat in cleartext next to
  # the binaries and could never be rotated without orphaning every document. The KEK now lives in the
  # keyring the KMS provider owns (Kms:KeyringPath), DPAPI-protected and bound to this machine; the
  # service creates it on first start and rotates it on the Kms:RotationIntervalDays schedule.
  $keyDir = Split-Path -Parent $settings.Kms.KeyringPath
  if ($keyDir -and -not (Test-Path $keyDir)) { New-Item -ItemType Directory -Force -Path $keyDir | Out-Null }
}

# The keyring holds the only copy of the key encryption key: lock it to the service account and the
# administrators, and take it OFF the inheritance chain so a permissive parent folder cannot widen it.
# Anyone who can read both this folder and the object store can read every archived document.
if ($settings.Kms -and $settings.Kms.KeyringPath) {
  $keyDir = Split-Path -Parent $settings.Kms.KeyringPath
  if ($keyDir -and (Test-Path $keyDir)) {
    & icacls $keyDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
    Write-Host "locked keyring folder $keyDir to SYSTEM + Administrators." -ForegroundColor Green
  }
}

# ── licence ───────────────────────────────────────────────────────────────────
<#
  An upgrade keeps the installation's own appsettings.Production.json, which is right for the
  database and the JWT key but wrong for the licence: an installation made before licensing was
  switched on has no License section at all, so a newly stamped package's key would never reach it
  and the server would go on reporting "No licence public key is configured" no matter how many
  times the setup was re-run. So the key is adopted from the package when the installation has none.

  An existing non-empty key is never overwritten here — re-keying a live installation invalidates
  its licence, and that has to be asked for explicitly with -PublicKey.
#>
$licenceKey = $PublicKey
$licenceKeySource = 'the -PublicKey argument'
if (-not $licenceKey) {
  $tplPath = Join-Path $here 'appsettings.Production.json.template'
  if (Test-Path $tplPath) {
    $tplKey = "$((Get-Content $tplPath -Raw | ConvertFrom-Json).License.PublicKey)".Trim()
    $currentKey = if ($settings.PSObject.Properties['License']) { "$($settings.License.PublicKey)".Trim() } else { '' }
    if ($tplKey -and -not $currentKey) { $licenceKey = $tplKey; $licenceKeySource = 'the package' }
  }
}

if ($licenceKey) {
  if (-not $settings.PSObject.Properties['License']) {
    $settings | Add-Member -NotePropertyName License -NotePropertyValue ([pscustomobject]@{
      Path = 'license.lic'; PublicKey = ''; Enforcement = 'ReadOnly'; RecheckInterval = '00:05:00'
    })
  }
  $settings.License.PublicKey = $licenceKey.Trim()
  Write-Host "licence: public key set from $licenceKeySource"
}

$settings.ConnectionStrings.Default = $ConnectionString
$settings.Database.Provider = $Provider
$settings.Urls = "http://0.0.0.0:$Port"
if ($StoragePath) { $settings.Storage.RootPath = $StoragePath }

# The scan agent staff install on their own PCs, served by the API so they get it from the system
# itself rather than from a link someone mailed around. Path written per install, since the folder
# is whatever was chosen at setup.
$agent = Join-Path $here 'tools\archiving-scan-agent.exe'
if (Test-Path $agent) {
  if (-not $settings.PSObject.Properties['Downloads']) {
    # An installation from before the agent shipped has no Downloads section at all.
    $settings | Add-Member -NotePropertyName Downloads -NotePropertyValue ([pscustomobject]@{ ScanAgentPath = '' })
  }
  $settings.Downloads.ScanAgentPath = $agent
  Write-Host "scan agent: $agent (offered at /api/downloads/scan-agent)"
}
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding utf8
Write-Host "wrote $settingsPath"

if ($StoragePath) {
  New-Item -ItemType Directory -Force -Path $StoragePath | Out-Null
  icacls $StoragePath /grant "${ServiceAccount}:(OI)(CI)M" /T | Out-Null
  Write-Host "storage: $StoragePath (write access granted to $ServiceAccount)"
}

# ── licence file ──────────────────────────────────────────────────────────────
<#
  Copied in before the service starts, so the first request the server ever handles already sees a
  valid licence. Installing it afterwards works too (Settings > Licence takes a pasted token), but
  only after somebody has watched every save fail in the meantime.
#>
$licenceTarget = Join-Path $here 'license.lic'
if ($LicenseFile) {
  if (-not (Test-Path $LicenseFile)) { throw "-LicenseFile not found: $LicenseFile" }
  $text = (Get-Content $LicenseFile -Raw).Trim()
  if (-not $text.StartsWith('ARCHLIC-')) {
    throw "$LicenseFile does not look like a licence (expected it to start with ARCHLIC-)."
  }
  Set-Content $licenceTarget -Value $text -Encoding utf8
  Write-Host "licence: installed $licenceTarget"
}

<#
  The combination that produces a silently crippled server: a key to verify with, and nothing to
  verify. Said here, loudly, rather than left for the customer to discover on their first save.
#>
$configuredKey = if ($settings.PSObject.Properties['License']) { "$($settings.License.PublicKey)".Trim() } else { '' }
if ($configuredKey -and -not (Test-Path $licenceTarget)) {
  Write-Warning ("This build VERIFIES licences, but no licence is installed. The server will start " +
                 "in $($settings.License.Enforcement): reads work, writes are refused. Install the " +
                 "licence from Settings > Licence, or re-run with -LicenseFile.")
}

# ── database ──────────────────────────────────────────────────────────────────
# The service creates the schema itself on first start (EF migrations run at startup), but the
# database has to exist and the service account has to be able to reach it.
if ($Provider -eq 'SqlServer' -and $dbExplicit) {
  $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
  if ($sqlcmd) {
    Write-Host "ensuring database [$Database] exists on $SqlServer ..."
    $args = @('-S', $SqlServer, '-C', '-b', '-Q',
      "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];")
    if ($SqlUser) { $args += @('-U', $SqlUser, '-P', $SqlPassword) } else { $args += '-E' }
    & sqlcmd @args | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "Could not create the database automatically — create [$Database] yourself, then re-run." }

    if (-not $SqlUser) {
      # Windows auth: the service account is what connects, so it needs a login and ownership of
      # this database (db_owner, because EF applies migrations and creates the schema).
      $grant = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$ServiceAccount')
    CREATE LOGIN [$ServiceAccount] FROM WINDOWS;
USE [$Database];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$ServiceAccount')
    CREATE USER [$ServiceAccount] FOR LOGIN [$ServiceAccount];
ALTER ROLE db_owner ADD MEMBER [$ServiceAccount];
"@
      & sqlcmd -S $SqlServer -E -C -b -Q $grant | Out-Null
      if ($LASTEXITCODE -eq 0) { Write-Host "granted $ServiceAccount db_owner on [$Database]" }
      else { Write-Warning "Could not grant $ServiceAccount access to [$Database] — do it in SSMS, then start the service." }
    }
  } else {
    Write-Warning "sqlcmd not found — create the database [$Database] and grant the service account access yourself."
  }
}

# ── service ───────────────────────────────────────────────────────────────────
<#
  Surviving a reboot.

  A service marked simply 'auto' is started by Windows *in parallel with* the database engine, not
  after it. On a cold boot the API then applies EF migrations against a MySQL/SQL Server that is
  still opening its files, throws before the web host is listening, and exits — and the machine
  comes back up with the site down. The three settings below close that off, and the API also waits
  for the database at startup (WaitForDatabaseAsync in Program.cs). Either alone leaves a gap: a
  dependency only guarantees the database service was *started*, not that it accepts connections.
#>

# Which database service to wait for, when it is on this machine. A remote database has no local
# service to depend on, and naming one that does not exist would stop our service from starting at
# all — so this only ever returns something it has verified is installed here.
function Get-LocalDatabaseService {
  param([string] $ConnectionString, [string] $Provider)

  $isLocal = $ConnectionString -match 'server\s*=\s*(\.|localhost|127\.0\.0\.1|\(local\))' -or
             $ConnectionString -match 'data source\s*=\s*(\.|localhost|127\.0\.0\.1|\(local\))'
  if (-not $isLocal) { return $null }

  $candidates = if ($Provider -eq 'MySql') {
    # MySQL's service name carries its version (MySQL80, MySQL84 …), and MariaDB is a drop-in that
    # people do use with this connection string, so match rather than guess a single name.
    @(Get-Service -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^(MySQL|MariaDB)' } | Select-Object -ExpandProperty Name)
  } else {
    <#
      A server can host several SQL Server instances, and depending on the wrong one is worse than
      depending on none: the service would wait for a database engine that has nothing to do with
      it and start while its own is still coming up. So the instance named in the connection string
      decides — 'SQL01\PROD' or '.\SQLEXPRESS' means MSSQL$PROD / MSSQL$SQLEXPRESS, and a bare '.'
      or 'localhost' means the default instance, MSSQLSERVER.
    #>
    $all = @(Get-Service -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' } | Select-Object -ExpandProperty Name)

    $wanted = if ($ConnectionString -match '(?:server|data source)\s*=\s*[^;\\]*\\([^;\s]+)') {
      "MSSQL`$$($Matches[1])"
    } else {
      'MSSQLSERVER'
    }
    # Only fall back to whatever is installed when the named instance genuinely is not here; a wrong
    # guess is the failure mode this block exists to avoid, so it returns nothing instead.
    @($all | Where-Object { $_ -eq $wanted })
  }
  return ($candidates | Select-Object -First 1)
}

$dbService = Get-LocalDatabaseService -ConnectionString $ConnectionString -Provider $Provider
if ($dbService) { Write-Host "database service on this machine: $dbService (the service will wait for it at boot)" }
else { Write-Host "no local database service detected — assuming the database is remote." }

<#
  delayed-auto rather than auto: it puts the start after the boot burst, which is exactly when the
  database engine is finishing its own startup. It costs about a minute of availability after a
  reboot and buys the difference between a site that comes back by itself and one that needs a
  person to go and start it.
#>
$startType = 'delayed-auto'

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  Write-Host "service '$ServiceName' exists — stopping it to update." -ForegroundColor Yellow
  if ($existing.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; $existing.WaitForStatus('Stopped', '00:00:30') }
  sc.exe config $ServiceName binPath= "`"$exe`"" start= $startType obj= "$ServiceAccount" | Out-Null
} else {
  Write-Host "creating service '$ServiceName' ..."
  # No password for the built-in accounts; a domain account needs obj=/password= supplied here.
  sc.exe create $ServiceName binPath= "`"$exe`"" start= $startType obj= "$ServiceAccount" DisplayName= "$DisplayName" | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with $LASTEXITCODE" }
}

# Applied on every run, re-installs included: an installation made by an earlier version of this
# script has plain 'auto' and no dependency, and upgrading it is the point of running this again.
sc.exe config $ServiceName start= $startType | Out-Null
if ($dbService) {
  sc.exe config $ServiceName depend= "$dbService" | Out-Null
  Write-Host "service will not start before '$dbService'"
} else {
  # Clears a dependency left by a previous install whose database has since moved off this machine.
  # A dependency naming a service that is not installed blocks the start outright.
  sc.exe config $ServiceName depend= "" | Out-Null
}
sc.exe description $ServiceName "نظام الأرشفة الإلكترونية — الديوان الإلكتروني" | Out-Null
# Restart on failure rather than leaving the site down until someone notices. The third action also
# covers every later failure, so this keeps retrying every 30s instead of giving up after three.
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
<#
  Without this flag Windows runs the recovery actions only when the process dies *unexpectedly*: a
  service that starts, fails on its own terms and exits cleanly is left stopped. That is precisely
  the cold-boot failure described above, so without it the restarts configured on the line before
  would never have fired for the one case that needed them most.
#>
sc.exe failureflag $ServiceName 1 | Out-Null

# The service account must be allowed to read the folder it runs from.
icacls $here /grant "${ServiceAccount}:(OI)(CI)RX" /T | Out-Null

# ── firewall + URL reservation ────────────────────────────────────────────────
if ($previousPort -and $previousPort -ne $Port) {
  Write-Host "port changed $previousPort -> $Port; removing the old reservation and rule."
  netsh http delete urlacl url="http://+:$previousPort/" 2>&1 | Out-Null
  Get-NetFirewallRule -DisplayName "Archiving (TCP $previousPort)" -ErrorAction SilentlyContinue |
    Remove-NetFirewallRule -ErrorAction SilentlyContinue
}
if (-not $NoFirewallRule) {
  $ruleName = "Archiving (TCP $Port)"
  if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
    Write-Host "firewall: opened TCP $Port"
  }
}
# Kestrel binds 0.0.0.0 as a non-administrator service, which needs a URL reservation.
netsh http add urlacl url="http://+:$Port/" user="$ServiceAccount" 2>&1 | Out-Null

# ── start ─────────────────────────────────────────────────────────────────────
Write-Host "`nstarting '$ServiceName' ..."
Start-Service $ServiceName
$deadline = (Get-Date).AddSeconds(90)
do {
  Start-Sleep -Seconds 3
  try {
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/public/stats" -UseBasicParsing -TimeoutSec 5
    if ($r.StatusCode -eq 200) { break }
  } catch { }
} while ((Get-Date) -lt $deadline)

$svc = Get-Service $ServiceName

# ── desktop shortcut ──────────────────────────────────────────────────────────
# On the Public desktop, so anyone who signs in to this server finds the system as a program
# rather than as an address they have to remember. Staff PCs run Create-Desktop-Shortcut.cmd.
$shortcutScript = Join-Path $here 'create-shortcut.ps1'
if (Test-Path $shortcutScript) {
  try {
    & $shortcutScript -Url "http://$env:COMPUTERNAME`:$Port/" -AllUsers | Out-Null
    Write-Host "desktop shortcut created for all users of this server."
  } catch {
    Write-Warning "could not create the desktop shortcut: $($_.Exception.Message)"
  }
}

Write-Host "`nservice: $($svc.Status)" -ForegroundColor Green
Write-Host "on boot: $startType$(if ($dbService) { ", after '$dbService'" })" -ForegroundColor Green
Write-Host "open:    http://$env:COMPUTERNAME`:$Port/" -ForegroundColor Green
if ($configuredKey) {
  Write-Host ("licence: " + $(if (Test-Path $licenceTarget) { "installed · enforcement $($settings.License.Enforcement)" }
                              else { "MISSING — writes will be refused until one is installed" })) -ForegroundColor $(
    if (Test-Path $licenceTarget) { 'Green' } else { 'Yellow' })
} else {
  Write-Host "licence: not enforced in this build" -ForegroundColor DarkGray
}
Write-Host "sign in: admin / Admin@12345  — change this password immediately." -ForegroundColor Yellow
Write-Host "logs:    Get-EventLog -LogName Application -Source Archiving -Newest 20"

# netsh and robocopy report non-zero for things that are not failures here — a URL reservation that
# was already there, files that were copied. PowerShell hands the last native exit code back to the
# caller, so Install.cmd would announce a failed install after a perfectly good one. Say plainly
# how it went instead of letting the last tool that ran decide.
exit 0
