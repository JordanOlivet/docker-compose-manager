#!/usr/bin/env pwsh
# Build then launch both apps for local development (Windows).
# Backend:  dotnet watch run (http profile, http://localhost:5050)
# Frontend: npm run dev (http://localhost:5173)
# Press Ctrl-C once to stop both. Run dev-setup\setup.ps1 first if you haven't.

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
$BackDir  = Join-Path $RepoRoot "docker-compose-manager-back"
$FrontDir = Join-Path $RepoRoot "docker-compose-manager-front"

function Write-Header($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

# ----- Build -----------------------------------------------------------------
Write-Header "Build backend (.NET)"
Push-Location $BackDir
dotnet build --nologo -v q
$backBuild = $LASTEXITCODE
Pop-Location
if ($backBuild -ne 0) { Write-Host "Backend build FAILED." -ForegroundColor Red; exit 1 }

Write-Header "Build frontend (SvelteKit)"
Push-Location $FrontDir
npm run build
$frontBuild = $LASTEXITCODE
Pop-Location
if ($frontBuild -ne 0) { Write-Host "Frontend build FAILED." -ForegroundColor Red; exit 1 }

# ----- Launch ----------------------------------------------------------------
Write-Header "Launch (Ctrl-C to stop both)"
Write-Host "  backend  -> http://localhost:5050" -ForegroundColor White
Write-Host "  frontend -> http://localhost:5173" -ForegroundColor White
Write-Host "  login     : admin / adminadmin`n" -ForegroundColor White

$procs = @()
try {
    $backend = Start-Process -FilePath "dotnet" `
        -ArgumentList "watch","run","--project","docker-compose-manager-back","--launch-profile","http" `
        -WorkingDirectory $BackDir -NoNewWindow -PassThru
    $procs += $backend

    $frontend = Start-Process -FilePath "cmd.exe" `
        -ArgumentList "/c","npm run dev" `
        -WorkingDirectory $FrontDir -NoNewWindow -PassThru
    $procs += $frontend

    # Wait until either process exits (or Ctrl-C lands in finally).
    while ($true) {
        Start-Sleep -Milliseconds 500
        if ($backend.HasExited -or $frontend.HasExited) { break }
    }
}
finally {
    Write-Host "`nStopping..." -ForegroundColor Yellow
    foreach ($p in $procs) {
        if ($p -and -not $p.HasExited) {
            # Kill the whole process tree (npm/dotnet spawn children).
            taskkill /PID $p.Id /T /F *> $null
        }
    }
}
