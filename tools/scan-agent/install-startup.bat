@echo off
rem Installs the Archiving scan agent to run automatically when the user logs in.
rem Run this once on the PC that has the scanner. Re-run install-startup.bat to update the path.
setlocal
set "TARGET=%~dp0archiving-scan-agent.exe"
set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"

if not exist "%TARGET%" (
  echo [!] Could not find archiving-scan-agent.exe next to this script.
  echo     Put this .bat in the same folder as the agent and try again.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%STARTUP%\Archiving Scan Agent.lnk'); $s.TargetPath='%TARGET%'; $s.WorkingDirectory='%~dp0'; $s.WindowStyle=7; $s.Description='Archiving Scan Agent'; $s.Save()"

echo.
echo [OK] Installed. The scan agent will start automatically on next sign-in.
echo      To start it now without signing out, run start-scan-agent.bat
echo.
echo To remove autostart, delete:
echo   %STARTUP%\Archiving Scan Agent.lnk
echo.
pause
