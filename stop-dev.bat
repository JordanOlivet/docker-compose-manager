@echo off
echo ====================================
echo Docker Compose Manager - Stop Dev
echo ====================================
echo.
echo Stopping all dotnet and node processes...
echo.

REM Kill all dotnet processes (backend)
taskkill /F /IM dotnet.exe 2>nul
if %errorlevel% equ 0 (
    echo Backend processes stopped.
) else (
    echo No backend processes found.
)

REM Kill all node processes (frontend)
taskkill /F /IM node.exe 2>nul
if %errorlevel% equ 0 (
    echo Frontend processes stopped.
) else (
    echo No frontend processes found.
)

echo.
echo ====================================
echo All development processes stopped!
echo ====================================
echo.
pause
