@echo off
rem NOTE: this only registers autostart. To install the agent properly — a copy in a stable
rem folder, a Start-menu shortcut and an entry in Settings > Apps — use install.bat instead.
rem Registers the Archiving scan agent to start when this user signs in.
rem
rem NOTE: most people do not need this script — the agent's own window has a
rem "تشغيل الوكيل تلقائياً عند بدء تشغيل Windows" checkbox that does the same thing.
rem This exists for IT deploying to many machines unattended.
rem
rem It writes the SAME registry value the checkbox writes, so the two agree and the
rem checkbox correctly shows as ticked afterwards. The old version of this script
rem dropped a shortcut in the Startup folder instead; that is removed below, because
rem having both makes Windows launch the agent twice at login.
setlocal
set "TARGET=%~dp0archiving-scan-agent.exe"
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
set "LEGACY=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Archiving Scan Agent.lnk"

if not exist "%TARGET%" (
  echo [!] Could not find archiving-scan-agent.exe next to this script.
  echo     Put this .bat in the same folder as the agent and try again.
  pause
  exit /b 1
)

rem --background starts it straight to the tray, so signing in does not throw a window up.
reg add "%RUNKEY%" /v ArchivingScanAgent /t REG_SZ /d "\"%TARGET%\" --background" /f >nul
if errorlevel 1 (
  echo [!] Could not write the startup entry. It may be locked by group policy.
  pause
  exit /b 1
)

if exist "%LEGACY%" del "%LEGACY%" >nul 2>&1

echo.
echo [OK] Installed. The scan agent will start automatically on next sign-in.
echo      To start it now, run start-scan-agent.bat
echo.
echo To remove autostart, run uninstall-startup.bat (or untick the box in the agent window).
echo.
pause
