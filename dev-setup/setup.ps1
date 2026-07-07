#!/usr/bin/env pwsh
# Dev environment bootstrap for Lighthouse (Windows).
# Detects prerequisites, prompts before installing any missing ones, generates
# gitignored local dev config, and installs project dependencies.
# This script NEVER launches the app - use ../run.ps1 to build and run.

$ErrorActionPreference = "Stop"

# Keep the window open if anything throws, so the error stays readable
# (important when launched by double-click instead of from a terminal).
trap {
    Write-Host "`nSetup FAILED:" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to close"
    exit 1
}

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$BackDir    = Join-Path $RepoRoot "lighthouse-back"
$BackProj   = Join-Path $BackDir "lighthouse-back"
$FrontDir   = Join-Path $RepoRoot "lighthouse-front"
$SampleDir  = Join-Path $PSScriptRoot "sample-compose-files"

function Write-Header($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Read-YesNo($prompt, $defaultYes = $true) {
    $suffix = if ($defaultYes) { "[Y/n]" } else { "[y/N]" }
    $answer = Read-Host "$prompt $suffix"
    if ([string]::IsNullOrWhiteSpace($answer)) { return $defaultYes }
    return $answer -match '^(y|yes)$'
}

function Test-Command($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

# Try to install a tool via winget (preferred) or choco. Returns $true on attempt.
function Install-Tool($wingetId, $chocoId, $manualUrl) {
    if (Test-Command winget) {
        Write-Host "  Installing via winget ($wingetId)..." -ForegroundColor Yellow
        winget install --id $wingetId -e --accept-source-agreements --accept-package-agreements
        return $true
    }
    elseif (Test-Command choco) {
        Write-Host "  Installing via choco ($chocoId)..." -ForegroundColor Yellow
        choco install $chocoId -y
        return $true
    }
    else {
        Write-Host "  No package manager (winget/choco) found." -ForegroundColor Red
        Write-Host "  Install manually: $manualUrl" -ForegroundColor Red
        return $false
    }
}

# ---------------------------------------------------------------------------
Write-Header "Prerequisite check"

$missing = @()

# git
if (Test-Command git) {
    Write-Host ("  git       OK   " + (git --version)) -ForegroundColor Green
} else {
    Write-Host "  git       MISSING" -ForegroundColor Red
    $missing += "git"
}

# .NET SDK (require major >= 10)
$dotnetOk = $false
if (Test-Command dotnet) {
    $dotnetVer = (dotnet --version)
    $dotnetMajor = [int]($dotnetVer.Split('.')[0])
    if ($dotnetMajor -ge 10) {
        Write-Host "  .NET SDK  OK   $dotnetVer" -ForegroundColor Green
        $dotnetOk = $true
    } else {
        Write-Host "  .NET SDK  TOO OLD ($dotnetVer, need >= 10)" -ForegroundColor Red
        $missing += "dotnet"
    }
} else {
    Write-Host "  .NET SDK  MISSING" -ForegroundColor Red
    $missing += "dotnet"
}

# Node (require major >= 24)
$nodeOk = $false
if (Test-Command node) {
    $nodeVer = (node -v).TrimStart('v')
    $nodeMajor = [int]($nodeVer.Split('.')[0])
    if ($nodeMajor -ge 24) {
        Write-Host "  Node      OK   v$nodeVer" -ForegroundColor Green
        $nodeOk = $true
    } else {
        Write-Host "  Node      TOO OLD (v$nodeVer, need >= 24)" -ForegroundColor Red
        $missing += "node"
    }
} else {
    Write-Host "  Node      MISSING" -ForegroundColor Red
    $missing += "node"
}

# Docker (+ daemon reachable)
if (Test-Command docker) {
    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Docker    OK   (daemon reachable)" -ForegroundColor Green
    } else {
        Write-Host "  Docker    INSTALLED but daemon NOT running - start Docker Desktop" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Docker    MISSING" -ForegroundColor Red
    $missing += "docker"
}

# ---------------------------------------------------------------------------
if ($missing.Count -gt 0) {
    Write-Header "Install missing prerequisites"
    foreach ($tool in $missing) {
        switch ($tool) {
            "git"    { $w="Git.Git";              $c="git";           $u="https://git-scm.com/downloads" }
            "dotnet" { $w="Microsoft.DotNet.SDK.10"; $c="dotnet-sdk"; $u="https://dotnet.microsoft.com/download/dotnet/10.0" }
            "node"   { $w="OpenJS.NodeJS.LTS";    $c="nodejs-lts";    $u="https://nodejs.org/" }
            "docker" { $w="Docker.DockerDesktop"; $c="docker-desktop"; $u="https://www.docker.com/products/docker-desktop/" }
        }
        if (Read-YesNo "  Install '$tool' now?") {
            Install-Tool $w $c $u | Out-Null
        } else {
            Write-Host "  Skipped '$tool'. Install manually: $u" -ForegroundColor Yellow
        }
    }
    Write-Host "`n  NOTE: newly installed tools may require a new terminal (PATH refresh)." -ForegroundColor Yellow
    Write-Host "  Re-run this script after installing to continue." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
Write-Header "Generate local dev config"

# Sample compose dir so discovery has something to show.
if (-not (Test-Path $SampleDir)) {
    New-Item -ItemType Directory -Path $SampleDir | Out-Null
}

# Backend per-developer override (gitignored).
$localSettings = Join-Path $BackProj "appsettings.Development.local.json"
if (Test-Path $localSettings) {
    Write-Host "  appsettings.Development.local.json already exists - leaving as is." -ForegroundColor Yellow
} else {
    $hostPath = $SampleDir.Replace('\', '\\')  # escape backslashes for JSON
    @"
{
  "ComposeDiscovery": {
    "HostPathMapping": "$hostPath"
  }
}
"@ | Set-Content -Path $localSettings -Encoding utf8
    Write-Host "  Created $localSettings" -ForegroundColor Green
}

# Frontend env (gitignored).
$frontEnv = Join-Path $FrontDir ".env.development"
if (Test-Path $frontEnv) {
    Write-Host "  .env.development already exists - leaving as is." -ForegroundColor Yellow
} else {
    # API calls are proxied through the Vite dev server (see vite.config.ts).
    # Set VITE_API_URL here only if your backend runs on a non-default host/port.
    "# VITE_API_URL=http://localhost:5050`n" | Set-Content -Path $frontEnv -Encoding utf8
    Write-Host "  Created $frontEnv" -ForegroundColor Green
}

# Arm the shared git hooks (auto-relock the frontend lockfile - see
# .githooks/pre-commit) so a locally installed npm never commits a lockfile
# that CI's npm rejects.
Push-Location $RepoRoot
git config core.hooksPath .githooks
Pop-Location
Write-Host "  Git hooks armed (core.hooksPath = .githooks)" -ForegroundColor Green

# ---------------------------------------------------------------------------
Write-Header "Install dependencies"

if ($dotnetOk) {
    Write-Host "  dotnet restore (backend)..." -ForegroundColor Cyan
    Push-Location $BackDir
    dotnet restore
    Pop-Location
} else {
    Write-Host "  Skipped dotnet restore (.NET SDK not ready)." -ForegroundColor Yellow
}

if ($nodeOk) {
    # Node 25+ ships npm 11, which resolves the lockfile differently from CI's
    # npm 10 (Node 24). Always run the install through the pinned npm@10.9.2 via
    # npx so setup never churns the lockfile, whatever npm is installed locally.
    Write-Host "  npm install (frontend, pinned npm@10.9.2)..." -ForegroundColor Cyan
    Push-Location $FrontDir
    npx -y npm@10.9.2 install
    Pop-Location
} else {
    Write-Host "  Skipped npm install (Node not ready)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
Write-Header "Optional tools"

if ($dotnetOk -and (Read-YesNo "  Install dotnet-ef global tool (only needed to author DB migrations)?" $false)) {
    dotnet tool install --global dotnet-ef
}

if ($dotnetOk -and (Read-YesNo "  Trust the .NET HTTPS dev cert (only needed if you switch the app to HTTPS)?" $false)) {
    dotnet dev-certs https --trust
}

# ---------------------------------------------------------------------------
Write-Header "Done"
Write-Host "  Setup complete. To build and run both apps:" -ForegroundColor Green
Write-Host "      .\run.ps1   (from the repo root)" -ForegroundColor White
Write-Host ""
Write-Host "  App URLs:  backend http://localhost:5050   frontend http://localhost:5173" -ForegroundColor White
Write-Host "  Login:     admin / adminadmin   (must change on first login)" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter to close"
