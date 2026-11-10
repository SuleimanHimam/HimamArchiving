<#
.SYNOPSIS
  Puts «الديوان الإلكتروني» on the desktop as a program: its own window, its own icon, no address
  bar and no browser tabs around it.

.DESCRIPTION
  The shortcut launches the installed browser in application mode (--app=), which is the same
  window a PWA install produces. That matters here because the browser's own «تثبيت» button only
  appears on a secure origin: a LAN deployment served over plain http:// will never offer it, and
  this is the way to get the same result without a certificate.

  Run it on the server (for whoever signs in there) or on any staff PC. Nothing is installed and
  nothing needs administrator rights unless -AllUsers is used.

.EXAMPLE
  .\create-shortcut.ps1 -Url 'http://SERVER01:7070/'
  .\create-shortcut.ps1 -Url 'http://192.168.1.10:7070/' -AllUsers      # needs administrator
#>
[CmdletBinding()]
param(
  # The address staff open the system at. Asked for when omitted.
  [string] $Url,
  [string] $Name = 'الديوان الإلكتروني',
  # Put it on the Public desktop, so every user of this machine gets it (administrator only).
  [switch] $AllUsers,
  # Skip application mode and just open the default browser normally.
  [switch] $PlainBrowser
)

$ErrorActionPreference = 'Stop'

if (-not $Url) {
  $default = "http://$env:COMPUTERNAME`:7070/"
  $Url = Read-Host "Address of the system [$default]"
  if (-not $Url.Trim()) { $Url = $default }
}
$Url = $Url.Trim()
if ($Url -notmatch '^https?://') { $Url = "http://$Url" }
# The app's own start route, so the shortcut lands inside the system rather than on the marketing
# page — matching start_url in the web manifest.
if ($Url -notmatch '/app') { $Url = $Url.TrimEnd('/') + '/app' }

# ── which browser ─────────────────────────────────────────────────────────────
function Find-Browser {
  $candidates = @(
    "$env:ProgramFiles(x86)\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
  )
  foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
  $null
}

$browser = if ($PlainBrowser) { $null } else { Find-Browser }

$icon = Join-Path $PSScriptRoot 'app-icon.ico'
$desktop = if ($AllUsers) {
  if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
          ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "-AllUsers writes to the Public desktop — run this from an elevated PowerShell."
  }
  [Environment]::GetFolderPath('CommonDesktopDirectory')
} else {
  [Environment]::GetFolderPath('Desktop')
}

$linkPath = Join-Path $desktop "$Name.lnk"
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($linkPath)

if ($browser) {
  $link.TargetPath = $browser
  # --app: a window with no tab strip and no address bar, and its own taskbar button.
  # --window-size is a first-run hint only; Windows remembers the size the user leaves it at.
  $link.Arguments = "--app=`"$Url`" --window-size=1400,900"
} else {
  # No Chromium browser found: hand the URL to whatever the system opens http with. It is a
  # normal browser window rather than an app window, but it works everywhere.
  $link.TargetPath = 'rundll32.exe'
  $link.Arguments = "url.dll,FileProtocolHandler $Url"
}
$link.WorkingDirectory = if ($browser) { Split-Path -Parent $browser } else { $env:SystemRoot }
$link.Description = 'نظام إدارة الوثائق والأرشفة'
if (Test-Path $icon) { $link.IconLocation = "$icon,0" }
$link.Save()

Write-Host ""
Write-Host "shortcut: $linkPath" -ForegroundColor Green
Write-Host "opens:    $Url"
Write-Host "mode:     $(if ($browser) { "app window ($(Split-Path -Leaf $browser))" } else { 'default browser' })"
if (-not (Test-Path $icon)) {
  Write-Warning "app-icon.ico was not found next to this script — the shortcut uses the browser's icon."
}
