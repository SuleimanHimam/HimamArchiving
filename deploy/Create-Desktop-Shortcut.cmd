@echo off
rem ============================================================================
rem  Archiving - put the system on this computer's desktop as a program.
rem
rem  Double-click on any staff PC. It asks for the server address and creates a
rem  desktop shortcut that opens the system in its own window - no address bar,
rem  no browser tabs, its own taskbar icon.
rem
rem  No administrator rights needed for the current user's desktop.
rem
rem  ASCII-only on purpose: cmd.exe renders this file in the console code page.
rem ============================================================================
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0create-shortcut.ps1"
echo.
echo Press any key to close.
pause >nul
endlocal
