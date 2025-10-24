@echo off
echo ====================================
echo Docker Compose Manager - Dev Mode
echo ====================================
echo.
echo Starting Backend and Frontend...
echo.

REM Start Backend in a new window
start "Docker Manager - Backend" cmd /k "cd docker-compose-manager-back && echo Starting Backend... && dotnet watch run"

REM Wait a moment before starting frontend
timeout /t 2 /nobreak > nul

REM Start Frontend in a new window
start "Docker Manager - Frontend" cmd /k "cd docker-compose-manager-front && echo Starting Frontend... && npm run dev"

echo.
echo ====================================
echo Applications started!
echo ====================================
echo.
echo Backend: http://localhost:5000
echo Swagger: http://localhost:5000/swagger
echo Frontend: http://localhost:5173
echo.
echo Credentials: admin / admin
echo.
echo Press any key to exit this window (apps will continue running)...
pause > nul
