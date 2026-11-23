#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the full Trading Engine stack on Windows (PostgreSQL + Backend).
.DESCRIPTION
    Sets up environment variables and starts the backend server.

    Prerequisites:
      - PostgreSQL installed and accessible (e.g. via Scoop, winget, or the PostgreSQL MSI)
      - .NET 8 SDK installed
      - DATABASE_URL environment variable set, OR PostgreSQL running locally

    Usage:
      1. Set required environment variables (edit this script or set them in your shell):
           $env:DATABASE_URL = "postgresql://postgres:password@localhost:5432/trading_engine"
      2. Run:
           .\start-all.ps1
#>

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. Environment configuration
# ---------------------------------------------------------------------------
# If DATABASE_URL is not already set, default to a local PostgreSQL instance.
if (-not $env:DATABASE_URL) {
    $env:DATABASE_URL    = "postgresql://postgres@localhost:5432/trading_engine"
}
if (-not $env:DATABASE_SSL_MODE) {
    $env:DATABASE_SSL_MODE = "Disable"
}
if (-not $env:PORT) {
    $env:PORT = "5000"
}

# ---------------------------------------------------------------------------
# 2. (Optional) Start PostgreSQL service if it is not already running.
#    Adjust the service name to match your PostgreSQL installation.
#    Common names: "postgresql-x64-16", "postgresql", "PostgreSQL"
# ---------------------------------------------------------------------------
$pgServiceName = "postgresql-x64-16"
$pgService = Get-Service -Name $pgServiceName -ErrorAction SilentlyContinue
if ($pgService -and $pgService.Status -ne "Running") {
    Write-Host "Starting PostgreSQL service '$pgServiceName'..." -ForegroundColor Yellow
    Start-Service -Name $pgServiceName
    Start-Sleep -Seconds 2
}

# ---------------------------------------------------------------------------
# 3. Start the backend
# ---------------------------------------------------------------------------
Write-Host "PostgreSQL configuration: $env:DATABASE_URL" -ForegroundColor Cyan
Write-Host "Starting Backend on http://localhost:$env:PORT ..." -ForegroundColor Cyan
Write-Host ""

Set-Location $PSScriptRoot
dotnet run --project TradingEngine.csproj
