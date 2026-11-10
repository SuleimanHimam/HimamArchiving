@echo off
rem ============================================================================
rem  Archiving - double-click licence installer.
rem
rem  Run this from inside the INSTALLATION folder (where Archiving.Api.exe is),
rem  with license.lic sitting next to it. It writes the licence, sets the
rem  public key if the installation has none, and restarts the service.
rem
rem  ASCII-only on purpose: cmd.exe renders this file in the console code page.
rem ============================================================================
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0set-license.ps1"

echo.
echo Press any key to close.
pause >nul
endlocal
