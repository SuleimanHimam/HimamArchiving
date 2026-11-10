@echo off
rem ============================================================================
rem  Archiving - diagnose a 502 from the IIS site in front of the service.
rem
rem  502 means IIS answered but the service behind it did not. This walks the
rem  chain - service, its port, the rewrite rule, the event log - and names the
rem  broken link.
rem
rem  Run it ON THE SERVER. It asks for administrator rights, reports what it
rem  finds, then offers to apply the safe repairs (start the service, align the
rem  rewrite port). Nothing is changed unless you answer Y.
rem
rem  Kept ASCII-only: cmd.exe renders this file in the console code page.
rem ============================================================================
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0"

set PORT=%1
if "%PORT%"=="" set PORT=5080

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fix-502.ps1" -PublicPort %PORT%

echo.
set /p ANSWER=Apply the safe repairs now? [Y/N]
if /i "%ANSWER%"=="Y" (
  echo.
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fix-502.ps1" -PublicPort %PORT% -Fix
)

echo.
echo Press any key to close.
pause >nul
endlocal
