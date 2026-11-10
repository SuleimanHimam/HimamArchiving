<#
.SYNOPSIS
  Licenses an installation that is already running, without reinstalling it.

.DESCRIPTION
  Two things have to be true before a licence takes effect, and an installation made before
  licensing was switched on has neither:

    1. License:PublicKey in appsettings.Production.json — what the server verifies signatures with.
       Without it every licence is refused with "No licence public key is configured", including a
       perfectly valid one pasted into Settings > Licence.
    2. license.lic beside the executable — the licence itself.

  This script sets both and restarts the service, so the licence is live when it comes back.

  Run it from inside the installation folder (where Archiving.Api.exe is), or pass -InstallPath.

.EXAMPLE
  .\set-license.ps1 -LicenseFile C:\Users\me\Desktop\license.lic
  .\set-license.ps1 -LicenseFile .\license.lic -PublicKey "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE…"
#>
[CmdletBinding()]
param(
  # The signed licence. Defaults to a license.lic sitting next to this script.
  [string] $LicenseFile,

  <#
    The licence public key. Only needed when the installation has no key yet and there is no
    packaged template to take one from — normally it is found automatically.
  #>
  [string] $PublicKey,

  # The installation folder. Defaults to the folder this script is in.
  [string] $InstallPath,

  [string] $ServiceName = 'Archiving'
)

$ErrorActionPreference = 'Stop'
$here = if ($InstallPath) { $InstallPath } else { $PSScriptRoot }

Write-Host "== Archiving — licence ==" -ForegroundColor Cyan
Write-Host "installation: $here"

$settingsPath = Join-Path $here 'appsettings.Production.json'
if (-not (Test-Path $settingsPath)) {
  throw "appsettings.Production.json not found in $here. Run this from the installation folder, or pass -InstallPath."
}

# ── 1. the licence file ───────────────────────────────────────────────────────
if (-not $LicenseFile) { $LicenseFile = Join-Path $PSScriptRoot 'license.lic' }
if (-not (Test-Path $LicenseFile)) {
  throw "No licence file. Pass -LicenseFile <path to license.lic>."
}
$token = (Get-Content $LicenseFile -Raw).Trim()

<#
  The prefix is checked because of how a licence usually arrives: pasted out of an email, where the
  leading ARCHLIC- is easy to miss when selecting the text. The server rejects the result as "not an
  Archiving licence key", which reads like the licence is wrong rather than the copy of it.
#>
if (-not $token.StartsWith('ARCHLIC-')) {
  throw ("$LicenseFile does not start with ARCHLIC-. A licence looks like " +
         "ARCHLIC-1.<payload>.<signature> — check the beginning was not lost when it was copied.")
}

$licenceTarget = Join-Path $here 'license.lic'
Set-Content $licenceTarget -Value $token -Encoding utf8
Write-Host "licence file: $licenceTarget" -ForegroundColor Green

# What it says, before anything is restarted — a licence for the wrong machine is the common case,
# and reading it here beats discovering it from a red banner afterwards.
try {
  $body = ($token -split '\.')[1].Replace('-', '+').Replace('_', '/')
  switch ($body.Length % 4) { 2 { $body += '==' } 3 { $body += '=' } }
  $info = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($body)) | ConvertFrom-Json
  Write-Host ("             customer $($info.customer) · machine $($info.machineId) · " +
              "expires $(if ($info.expiresAt) { $info.expiresAt } else { 'never' })")
} catch {
  Write-Warning "The licence payload could not be read for display. It will still be verified by the server."
}

# ── 2. the public key ─────────────────────────────────────────────────────────
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if (-not $settings.PSObject.Properties['License']) {
  $settings | Add-Member -NotePropertyName License -NotePropertyValue ([pscustomobject]@{
    Path = 'license.lic'; PublicKey = ''; Enforcement = 'ReadOnly'; RecheckInterval = '00:05:00'
  })
}

$key = $PublicKey
if (-not $key) {
  # The packaged template carries the key when the package was built with one.
  $tpl = Join-Path $here 'appsettings.Production.json.template'
  if (Test-Path $tpl) { $key = "$((Get-Content $tpl -Raw | ConvertFrom-Json).License.PublicKey)".Trim() }
}
if ($key) {
  try { $null = [Convert]::FromBase64String($key) }
  catch { throw "The public key is not valid base64. Copy the whole single line from license-public.txt." }
  $settings.License.PublicKey = $key
  Write-Host "public key:   set ($($key.Length) chars)" -ForegroundColor Green
} elseif ("$($settings.License.PublicKey)".Trim()) {
  Write-Host "public key:   already configured — left as it is"
} else {
  throw ("No public key: this installation has none and the package carries none. Pass -PublicKey " +
         "with the contents of license-public.txt, or the licence cannot be verified.")
}

$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding utf8
Write-Host "wrote $settingsPath"

# ── 3. restart ────────────────────────────────────────────────────────────────
# The licence is re-read on a timer, so a restart is not strictly required — but it makes the change
# take effect now rather than within RecheckInterval, which is what someone standing at the server
# expects to see.
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
  Write-Host "restarting service '$ServiceName' ..."
  Restart-Service $ServiceName -Force
  $svc.WaitForStatus('Running', '00:01:00')
  Write-Host "service running." -ForegroundColor Green
} else {
  Write-Warning ("Service '$ServiceName' not found — the files are in place, but restart the site " +
                 "(or the app pool, on IIS) for the licence to be picked up immediately.")
}

Write-Host ""
Write-Host "Done. Check الإعدادات ← الترخيص in the browser: it should now show the customer and expiry." -ForegroundColor Green
