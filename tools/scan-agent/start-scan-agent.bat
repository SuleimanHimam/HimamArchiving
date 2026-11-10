@echo off
rem Starts the Archiving scan agent (double-click to run). It shows a window and a tray icon.
rem
rem Useful switches:
rem   --mock        return a sample page instead of using a scanner (test the pipeline)
rem   --background  start straight to the tray, no window
rem   --console     no UI at all; log to this terminal (the pre-UI behaviour)
cd /d "%~dp0"
start "Archiving Scan Agent" "%~dp0archiving-scan-agent.exe" %*
