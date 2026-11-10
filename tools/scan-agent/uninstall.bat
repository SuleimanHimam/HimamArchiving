@echo off
rem Removes the installed Archiving scan agent for the CURRENT USER: the program folder, both
rem shortcuts, the run-at-login entry and the Settings > Apps registration.
rem
rem This is the same thing the "Uninstall" button in Settings > Apps does. It stops a running
rem agent first, because a running program cannot delete its own files.
rem
rem To keep the program but stop it launching at sign-in, use uninstall-startup.bat instead
rem (or untick the box in the agent's window).
setlocal
set "INSTALLED=%LOCALAPPDATA%\Programs\ArchivingScanAgent\archiving-scan-agent.exe"

if not exist "%INSTALLED%" (
  echo [i] The agent does not appear to be installed for this user.
  echo     Nothing to remove.
  pause
  exit /b 0
)

"%INSTALLED%" --uninstall --silent

echo.
echo [OK] The scan agent has been removed.
echo.
pause
