@echo off
rem ============================================================================
rem  Archiving - double-click uninstaller.
rem
rem  Run this from inside the INSTALLATION folder. It asks for administrator
rem  rights, then removes the Windows Service, the firewall rule, the URL
rem  reservation and the desktop shortcut.
rem
rem  It does NOT delete the database, the documents or the settings - it prints
rem  where they are so you can decide.
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-service.ps1"

echo.
echo Press any key to close.
pause >nul
endlocal
