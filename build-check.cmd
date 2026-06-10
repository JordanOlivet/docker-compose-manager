@echo off
REM Double-click this file to run the build/check.
REM It keeps the window open no matter what happens (even if PowerShell fails to start).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-check.ps1" -NoPause
echo.
pause
