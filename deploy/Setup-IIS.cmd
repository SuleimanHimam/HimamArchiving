@echo off
rem ============================================================================
rem  Archiving - put IIS in front of the service (double-click).
rem
rem  Only needed when the site must be served by IIS - a TLS certificate, a host
rem  name, or a policy that all web traffic goes through IIS. Without one of
rem  those, the service is already a complete web server and IIS only adds a
rem  moving part.
rem
rem  Asks for administrator rights (it edits IIS configuration, URL reservations
rem  and the firewall), then opens the window.
rem
rem  Kept ASCII-only on purpose: cmd.exe renders this file in the console code
rem  page, where Arabic comments would come out as mojibake. The window itself
rem  (iis-setup.ps1) is in Arabic.
rem ============================================================================
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0iis-setup.ps1"

if %errorlevel% neq 0 (
  echo.
  echo The IIS setup exited with an error. Press any key to close.
  pause >nul
)
endlocal
