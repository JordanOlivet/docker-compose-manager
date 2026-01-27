#!/usr/bin/env pwsh
# Simple build/check script for backend and frontend

$ErrorActionPreference = "Continue"
$backendResult = $null
$frontendResult = $null

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Backend Build (.NET)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Push-Location docker-compose-manager-back
dotnet build --nologo -v q
$backendResult = $LASTEXITCODE
Pop-Location

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Frontend Check (SvelteKit)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Push-Location docker-compose-manager-front
npm run check
$frontendResult = $LASTEXITCODE
Pop-Location

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($backendResult -eq 0) {
    Write-Host "  Backend:  OK" -ForegroundColor Green
} else {
    Write-Host "  Backend:  FAILED" -ForegroundColor Red
}

if ($frontendResult -eq 0) {
    Write-Host "  Frontend: OK" -ForegroundColor Green
} else {
    Write-Host "  Frontend: FAILED" -ForegroundColor Red
}

Write-Host ""

if ($backendResult -eq 0 -and $frontendResult -eq 0) {
    Write-Host "All checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some checks failed." -ForegroundColor Red
    exit 1
}
