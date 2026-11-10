<#
.SYNOPSIS
  Builds the deployable package for Windows Server: one folder (and a .zip) holding the API, the
  built SPA and the install scripts.

.DESCRIPTION
  Produces a self-contained win-x64 build, so the target server needs no .NET runtime installed —
  copy the folder over, run install-service.ps1, done. The SPA is built into the API's wwwroot,
  so one service serves the pages and the API on one port with no IIS in front.

.EXAMPLE
  .\build-package.ps1
  .\build-package.ps1 -Output D:\packages -SkipZip
#>
[CmdletBinding()]
param(
  # Where the package folder is written. Defaults to <repo>\dist.
  [string] $Output,
  # Skip `npm ci` when node_modules is already present and current (faster local rebuilds).
  [switch] $SkipNpmInstall,
  # Leave the scan agent out. It is ~60 MB of the package, and a deployment where nobody scans
  # from their own PC does not need it.
  [switch] $SkipScanAgent,
  [switch] $SkipZip,

  <#
    The licence public key to stamp into the packaged appsettings template, so every installation
    made from this package verifies licences instead of running with licensing dormant.

    Omit it and the package ships exactly as before — dormant, nothing enforced — which is still the
    right thing for a pilot or an internal build. Take the value from license-public.txt, or from
    the licence manager's "نسخ المفتاح العام" button. It is a *public* key: it verifies and cannot sign,
    so it is safe on every customer machine.
  #>
  [string] $PublicKey,

  <#
    The customer's signed licence, copied into the package as license.lic so the installation is
    licensed the moment it is unpacked — no command line, no extra file to remember.

    Pair it with -PublicKey: the key is what verifies the licence, the licence is what satisfies it.
    Either one alone is warned about, because either one alone is a package that does not work the
    way whoever built it believed.
  #>
  [string] $LicenseFile
)

$ErrorActionPreference = 'Stop'

<#
  npm and dotnet write progress and warnings to stderr. Windows PowerShell turns any stderr line
  from a native program into an error record, which -ErrorActionPreference Stop would treat as a
  failed build. The exit code is the only honest signal, so native commands run through here.
#>
function Invoke-Native {
  param([Parameter(Mandatory)][string] $What, [Parameter(Mandatory)][scriptblock] $Command)
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & $Command 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $prev }
  if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)" }
}

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $repo 'dist' }
$stage = Join-Path $Output 'Archiving'

Write-Host "== Archiving — deployment package ==" -ForegroundColor Cyan
Write-Host "repo:   $repo"
Write-Host "output: $stage"

# ── 1. The SPA ────────────────────────────────────────────────────────────────
Push-Location (Join-Path $repo 'frontend')
try {
  if (-not $SkipNpmInstall) {
    Write-Host "`n-- npm ci --" -ForegroundColor Yellow
    Invoke-Native 'npm ci' { npm ci }
  }
  Write-Host "`n-- npm run build --" -ForegroundColor Yellow
  # The published SPA talks to the API it was served from, so no host is baked into the bundle.
  $env:VITE_API_URL = '/api'
  Invoke-Native 'frontend build' { npm run build }
} finally { Pop-Location }

# ── 2. The API, self-contained ────────────────────────────────────────────────
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
Write-Host "`n-- dotnet publish (win-x64, self-contained) --" -ForegroundColor Yellow
$apiProject = Join-Path $repo 'backend\src\Archiving.Api\Archiving.Api.csproj'
Invoke-Native 'dotnet publish' {
  dotnet publish $apiProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -p:GenerateDocumentationFile=false `
    -o $stage
}

# ── 3. SPA into wwwroot, so one service serves both ───────────────────────────
$wwwroot = Join-Path $stage 'wwwroot'
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $repo 'frontend\dist\*') $wwwroot -Recurse -Force
Write-Host "SPA -> wwwroot ($(@(Get-ChildItem $wwwroot -Recurse -File).Count) files)"

# ── 4. The scan agent, for staff PCs ──────────────────────────────────────────
<#
  Shipped inside the package and served by the API at /api/downloads/scan-agent, so a user who
  needs to scan gets it from the system itself. Browsers cannot reach a USB scanner; this agent
  runs on the user's own machine and exposes it over loopback.

  Single file and self-contained: what people download is one .exe that runs on a PC with no .NET
  installed, which is the only form that survives being emailed around an office.
#>
if (-not $SkipScanAgent) {
  Write-Host "`n-- dotnet publish (scan agent) --" -ForegroundColor Yellow
  $scanProject = Join-Path $repo 'tools\scan-agent\ScanAgent.csproj'
  $scanOut = Join-Path $Output 'scan-agent-publish'
  Invoke-Native 'scan agent publish' {
    # Compressed: this one is downloaded by every user who scans, over the office link, so ~60 MB
    # instead of ~110 MB is worth the second of extra start-up decompression.
    dotnet publish $scanProject -c Release -r win-x64 --self-contained true `
      -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
      -p:DebugType=None -o $scanOut
  }
  $agentExe = Join-Path $scanOut 'archiving-scan-agent.exe'
  if (-not (Test-Path $agentExe)) { throw "scan agent exe not found at $agentExe" }
  $toolsDir = Join-Path $stage 'tools'
  New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
  Copy-Item $agentExe $toolsDir -Force
  Write-Host ("scan agent -> tools\archiving-scan-agent.exe ({0:N1} MB)" -f ((Get-Item $agentExe).Length / 1MB))
  Remove-Item $scanOut -Recurse -Force -ErrorAction SilentlyContinue
}

# ── 5. Install scripts + settings template ────────────────────────────────────
foreach ($f in 'Install.cmd', 'install-gui.ps1', 'install-service.ps1',
               'Update.cmd', 'update-service.ps1',
               'Uninstall.cmd', 'Remove-Everything.cmd', 'uninstall-service.ps1',
               'Create-Desktop-Shortcut.cmd', 'create-shortcut.ps1', 'app-icon.ico',
               'Setup-IIS.cmd', 'iis-setup.ps1',
               'Fix-502.cmd', 'fix-502.ps1',
               'Set-License.cmd', 'set-license.ps1',
               'appsettings.Production.json.template', 'README.md', 'README.ar.md') {
  Copy-Item (Join-Path $PSScriptRoot $f) $stage -Force
}

# ── 5b. stamp the licence public key ──────────────────────────────────────────
if ($PublicKey) {
  $key = $PublicKey.Trim()

  # Checked before it is written rather than after a customer install fails to verify anything:
  # a truncated or line-wrapped key produces a server that rejects every licence with a message
  # about the licence rather than about the key, which is a long way to the real cause.
  try { $null = [Convert]::FromBase64String($key) }
  catch { throw "-PublicKey is not valid base64. Copy the whole single line from license-public.txt." }
  if ($key.Length -lt 80) { throw "-PublicKey looks too short ($($key.Length) chars) to be a P-256 public key." }

  $tpl = Join-Path $stage 'appsettings.Production.json.template'
  $json = Get-Content $tpl -Raw
  $json = $json -replace '"PublicKey"\s*:\s*""', ('"PublicKey": "' + $key + '"')
  Set-Content $tpl -Value $json -Encoding utf8

  Write-Host "licence: public key stamped into the packaged template — installations will VERIFY licences." -ForegroundColor Green
} else {
  Write-Host "licence: no -PublicKey given — packaged installs run with licensing dormant." -ForegroundColor DarkGray
}

# ── 5c. carry the customer's licence inside the package ───────────────────────
<#
  Shipping license.lic in the package is what makes the double-click path work. Setup.exe unpacks
  the folder and hands off to the install wizard, which builds install-service.ps1's arguments from
  its own form and has no field for a licence — so a licence passed on a command line never reaches
  that path at all. A licence sitting in the folder does: install-service.ps1 finds it already in
  place and reports it, and the server reads it on first start.
#>
if ($LicenseFile) {
  if (-not (Test-Path $LicenseFile)) { throw "-LicenseFile not found: $LicenseFile" }
  $lic = (Get-Content $LicenseFile -Raw).Trim()
  if (-not $lic.StartsWith('ARCHLIC-')) {
    throw "$LicenseFile does not look like a licence (expected it to start with ARCHLIC-)."
  }
  Set-Content (Join-Path $stage 'license.lic') -Value $lic -Encoding utf8

  # Which customer and machine, said out loud: building the wrong customer's licence into a package
  # is silent otherwise, and only surfaces as "wrong machine" on their server.
  $body = ($lic -split '\.')[1]
  try {
    $pad = $body.Replace('-', '+').Replace('_', '/')
    switch ($pad.Length % 4) { 2 { $pad += '==' } 3 { $pad += '=' } }
    $info = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($pad)) | ConvertFrom-Json
    Write-Host "licence: $($info.customer) · machine $($info.machineId) · packaged as license.lic" -ForegroundColor Green
  } catch {
    Write-Host "licence: packaged as license.lic (payload could not be summarised)" -ForegroundColor Yellow
  }

  if (-not $PublicKey) {
    Write-Warning ("A licence is packaged but no -PublicKey: the build will not verify it and the " +
                   "licence has no effect. Pass -PublicKey to make this package enforce licensing.")
  }
} elseif ($PublicKey) {
  Write-Warning ("This package VERIFIES licences but carries none. Every install from it starts in " +
                 "ReadOnly — reads work, writes refused. Pass -LicenseFile to include one.")
}

# A dev appsettings must never ship: it carries the local MySQL password and the dev JWT key.
Get-ChildItem $stage -Filter 'appsettings.Development.json' -ErrorAction SilentlyContinue |
  ForEach-Object { Remove-Item $_.FullName -Force }

# ── 6. Zip ────────────────────────────────────────────────────────────────────
if (-not $SkipZip) {
  $zip = Join-Path $Output ("Archiving_{0}.zip" -f (Get-Date -Format 'yyyyMMdd_HHmm'))
  if (Test-Path $zip) { Remove-Item $zip -Force }
  Write-Host "`n-- zipping --" -ForegroundColor Yellow
  Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
  Write-Host ("package: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
}

Write-Host "`nDone. Copy the folder (or the .zip) to the server, then run install-service.ps1 as administrator." -ForegroundColor Green
