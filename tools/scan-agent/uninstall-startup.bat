@echo off
rem Removes the Archiving scan agent's run-at-login entry (and the legacy Startup-folder
rem shortcut, if an older install left one behind). Does not uninstall or stop the agent.
setlocal
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
set "LEGACY=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Archiving Scan Agent.lnk"

reg delete "%RUNKEY%" /v ArchivingScanAgent /f >nul 2>&1
if exist "%LEGACY%" del "%LEGACY%" >nul 2>&1

echo.
echo [OK] The scan agent will no longer start automatically at sign-in.
echo      It keeps running until you quit it from the tray icon.
echo.
pause
