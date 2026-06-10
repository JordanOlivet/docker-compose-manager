#!/usr/bin/env pwsh
# Simple build/check script for backend and frontend

# -NoPause is passed by build-check.cmd, which handles the "press a key" pause itself.
param([switch]$NoPause)

$ErrorActionPreference = "Continue"
$backendResult = $null
$frontendResult = $null

try {
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
        $exitCode = 0
    } else {
        Write-Host "Some checks failed." -ForegroundColor Red
        $exitCode = 1
    }
}
catch {
    # Make sure we surface whatever blew up instead of closing silently
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  Script error" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    $exitCode = 1
}
finally {
    # Ensure we never leave a pushed location behind
    while ($true) {
        try { Pop-Location -ErrorAction Stop } catch { break }
    }
    Write-Host ""
    if (-not $NoPause) { Read-Host "Press Enter to close" }
}

exit $exitCode
