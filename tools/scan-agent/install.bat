@echo off
rem Installs the Archiving scan agent as a program on this PC, for the CURRENT USER.
rem
rem Most people do not need this script: running archiving-scan-agent.exe offers to install
rem itself the first time, and the agent's own window has a "تثبيت البرنامج على هذا الجهاز"
rem button. This exists for IT deploying to many machines unattended.
rem
rem What it does (all per-user — no administrator rights needed):
rem   - copies the program to %LOCALAPPDATA%\Programs\ArchivingScanAgent
rem   - adds a Start-menu and desktop shortcut
rem   - registers it in Settings > Apps, so it can be uninstalled like any other program
rem   - starts it with Windows at sign-in
setlocal
set "TARGET=%~dp0archiving-scan-agent.exe"

if not exist "%TARGET%" (
  echo [!] Could not find archiving-scan-agent.exe next to this script.
  echo     Put this .bat in the same folder as the agent and try again.
  pause
  exit /b 1
)

"%TARGET%" --install --silent
if errorlevel 1 (
  echo [!] Install failed. Run archiving-scan-agent.exe --install to see why.
  pause
  exit /b 1
)

echo.
echo [OK] Installed to %LOCALAPPDATA%\Programs\ArchivingScanAgent
echo      Shortcut added, and the agent will start automatically at next sign-in.
echo      To start it now, use the Start-menu shortcut.
echo.
echo To remove it, run uninstall.bat or use Settings ^> Apps.
echo.
pause
