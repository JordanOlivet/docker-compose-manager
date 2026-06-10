@echo off
REM Double-click this file to build both apps and launch them.
REM It keeps the window open no matter what happens (even if PowerShell fails to start).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" -NoPause
echo.
pause
