@echo off
rem ============================================================================
rem  Archiving - double-click updater.
rem
rem  Run this from inside the NEW package folder. It asks for administrator
rem  rights, finds where the service is installed, stops it, copies the new
rem  build over it and starts it again. Settings, documents and the database
rem  are left alone.
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-service.ps1"

echo.
echo Press any key to close.
pause >nul
endlocal
