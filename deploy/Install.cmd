@echo off
rem ============================================================================
rem  Archiving - double-click installer.
rem
rem  Asks for administrator rights (the install needs them: a Windows Service, a
rem  firewall rule and a URL reservation), then opens the install window, so
rem  nobody has to open a console or type a parameter name.
rem
rem  Kept ASCII-only on purpose: cmd.exe renders this file in the console code
rem  page, where Arabic comments would come out as mojibake. The window itself
rem  (install-gui.ps1) is in Arabic.
rem ============================================================================
setlocal

rem Already elevated? Open the window. Otherwise re-launch this file elevated and exit.
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-gui.ps1"

if %errorlevel% neq 0 (
  echo.
  echo The installer exited with an error. Press any key to close.
  pause >nul
)
endlocal
