<#
.SYNOPSIS
  Builds Archiving-Setup.exe: one self-contained installer carrying the whole package inside it.

.DESCRIPTION
  Runs build-package.ps1 first (unless -UseExistingPackage), then compiles the small WinForms front
  end in setup-src\ with the C# compiler that ships inside Windows, embedding the package zip as a
  resource and stamping on the application icon and an administrator manifest.

  Nothing has to be installed to build this — no Inno Setup, no WiX, no SDK beyond what the package
  build already needs.

.EXAMPLE
  .\build-setup.ps1
  .\build-setup.ps1 -UseExistingPackage
  .\build-setup.ps1 -PublicKey "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE…"
#>
[CmdletBinding()]
param(
  # Reuse the newest zip already in dist\ instead of rebuilding the app.
  [switch] $UseExistingPackage,
  [switch] $SkipNpmInstall,

  <#
    The licence public key, handed to build-package.ps1 so every installation made by this setup
    verifies licences. Omit it and the setup ships with licensing dormant, exactly as before.

    Ignored with -UseExistingPackage: the key is stamped while the package is built, so re-using an
    existing zip re-uses whatever key that zip was built with. Warned about rather than silently
    dropped — a setup believed to be licensed but built from an unstamped zip enforces nothing.
  #>
  [string] $PublicKey,

  <# The customer's signed licence, packaged inside the setup so the installed server is licensed
     without anyone passing an argument. Ignored with -UseExistingPackage, like -PublicKey. #>
  [string] $LicenseFile
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo 'dist'
$src = Join-Path $PSScriptRoot 'setup-src'

Write-Host "== Archiving — Setup.exe ==" -ForegroundColor Cyan

# ── 1. the package ────────────────────────────────────────────────────────────
if (-not $UseExistingPackage) {
  $pkgArgs = @{ SkipNpmInstall = $SkipNpmInstall }
  if ($PublicKey)   { $pkgArgs['PublicKey']   = $PublicKey }
  if ($LicenseFile) { $pkgArgs['LicenseFile'] = $LicenseFile }
  & (Join-Path $PSScriptRoot 'build-package.ps1') @pkgArgs | Out-Host
} elseif ($PublicKey -or $LicenseFile) {
  Write-Warning ("-PublicKey/-LicenseFile are ignored with -UseExistingPackage: both are applied " +
                 "while the package is built. Re-run without -UseExistingPackage to apply them.")
}
$zip = Get-ChildItem (Join-Path $dist 'Archiving_*.zip') -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $zip) { throw "No package zip in $dist — run build-package.ps1 first." }
Write-Host "payload: $($zip.Name) ($('{0:N1}' -f ($zip.Length / 1MB)) MB)"

# ── 2. compile ────────────────────────────────────────────────────────────────
# The .NET Framework compiler is part of Windows; using it keeps this build dependency-free and
# produces an exe that runs on any Windows Server without a runtime of its own.
$csc = Get-ChildItem 'C:\Windows\Microsoft.NET\Framework64' -Filter csc.exe -Recurse -ErrorAction SilentlyContinue |
  Sort-Object FullName | Select-Object -Last 1
if (-not $csc) { throw "csc.exe not found — the .NET Framework 4.x compiler is required." }

# The installer gets its own icon — the mark with an install badge. Setup.exe and the desktop
# shortcut sit in the same folders often enough that one icon for both means double-clicking the
# wrong file. Falls back to the plain app icon if the setup variant has not been generated.
$icon = Join-Path $PSScriptRoot 'setup-icon.ico'
if (-not (Test-Path $icon)) { $icon = Join-Path $PSScriptRoot 'app-icon.ico' }
if (-not (Test-Path $icon)) { throw "Neither setup-icon.ico nor app-icon.ico is in $PSScriptRoot." }

$out = Join-Path $dist 'Archiving-Setup.exe'
if (Test-Path $out) { Remove-Item $out -Force }

# /resource with an explicit name: the code looks the payload up as "package.zip" regardless of
# what the dated zip on disk is called.
$cscArgs = @(
  '/target:winexe'
  '/platform:anycpu'
  '/optimize+'
  "/out:$out"
  "/win32icon:$icon"
  "/win32manifest:$(Join-Path $src 'app.manifest')"
  "/resource:$($zip.FullName),package.zip"
  '/reference:System.dll'
  # ServiceController: setup stops a running installation before writing over its files.
  '/reference:System.ServiceProcess.dll'
  '/reference:System.Drawing.dll'
  '/reference:System.Windows.Forms.dll'
  '/reference:System.IO.Compression.dll'
  '/reference:System.IO.Compression.FileSystem.dll'
  '/nologo'
  (Join-Path $src 'Setup.cs')
)

Write-Host "compiling with $($csc.FullName) ..."
$prev = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try { & $csc.FullName @cscArgs 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $prev }
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit $LASTEXITCODE)" }

$size = (Get-Item $out).Length / 1MB
Write-Host ""
Write-Host ("setup: {0} ({1:N1} MB)" -f $out, $size) -ForegroundColor Green
Write-Host "Copy that single file to the server and double-click it." -ForegroundColor Green
