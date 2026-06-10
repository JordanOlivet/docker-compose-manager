#!/usr/bin/env pwsh
# Build both apps, then launch each in its own terminal window (Windows).
# Backend:  dotnet watch run (http profile, http://localhost:5050)
# Frontend: npm run dev (http://localhost:5173)
# Close a window (or Ctrl-C inside it) to stop that app.
# Run dev-setup\setup.ps1 first if you haven't.

# -NoPause is passed by run.cmd, which handles the "press a key" pause itself.
param([switch]$NoPause)

$ErrorActionPreference = "Stop"

function Wait-BeforeClose {
    if (-not $NoPause) { Read-Host "Press Enter to close" }
}

# Keep the window open on unexpected errors (e.g. launched by double-click).
trap {
    Write-Host "`nFAILED:" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    Write-Host ""
    Wait-BeforeClose
    exit 1
}

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
if ($backBuild -ne 0) { Write-Host "Backend build FAILED." -ForegroundColor Red; Wait-BeforeClose; exit 1 }

Write-Header "Build frontend (SvelteKit)"
Push-Location $FrontDir
npm run build
$frontBuild = $LASTEXITCODE
Pop-Location
if ($frontBuild -ne 0) { Write-Host "Frontend build FAILED." -ForegroundColor Red; Wait-BeforeClose; exit 1 }

# ----- Launch (each in its own window) ---------------------------------------
Write-Header "Launch (each app in its own window)"

# Backend window
Start-Process -FilePath "powershell" -ArgumentList @(
    "-NoExit", "-Command",
    "`$host.UI.RawUI.WindowTitle='Backend (.NET)'; Set-Location '$BackDir'; dotnet watch run --project docker-compose-manager-back --launch-profile http"
)

# Frontend window
Start-Process -FilePath "powershell" -ArgumentList @(
    "-NoExit", "-Command",
    "`$host.UI.RawUI.WindowTitle='Frontend (SvelteKit)'; Set-Location '$FrontDir'; npm run dev"
)

Write-Host "  backend  -> http://localhost:5050   (window: 'Backend (.NET)')" -ForegroundColor White
Write-Host "  frontend -> http://localhost:5173   (window: 'Frontend (SvelteKit)')" -ForegroundColor White
Write-Host "  login     : admin / adminadmin" -ForegroundColor White
Write-Host "`n  Two new windows opened. Close a window (or Ctrl-C in it) to stop that app." -ForegroundColor White
Write-Host ""
if (-not $NoPause) { Read-Host "Press Enter to close this launcher" }
