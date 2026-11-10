@echo off
rem ============================================================================
rem  Archiving - COMPLETE REMOVAL. Read this before double-clicking.
rem
rem  Uninstall.cmd removes the program and keeps the records.
rem  THIS file removes the records too:
rem
rem      - every stored document, deleted from disk
rem      - the database, dropped
rem      - the installation folder, deleted
rem
rem  There is no undo. You will be asked to type the database name first, and
rem  nothing is touched until you do - that prompt is deliberately not skippable.
rem
rem  Take a backup first if there is any doubt at all.
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
echo.
echo  ******************************************************************
echo  *  COMPLETE REMOVAL - documents, database and folder             *
echo  *  Close this window now if that is not what you want.           *
echo  ******************************************************************
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-service.ps1" -RemoveAll

echo.
echo Press any key to close.
pause >nul
endlocal
