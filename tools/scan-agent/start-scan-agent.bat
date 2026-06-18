@echo off
rem Starts the Archiving scan agent (double-click to run). Add --mock to test without a scanner.
cd /d "%~dp0"
start "Archiving Scan Agent" "%~dp0archiving-scan-agent.exe" %*
