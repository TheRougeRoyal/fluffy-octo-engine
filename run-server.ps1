#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and run the Trading Engine server on Windows.
.DESCRIPTION
    Builds the TradingEngine project and starts the server.
    Set the DATABASE_URL environment variable before running.
#>

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Starting Trading Engine Server" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build TradingEngine.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful! Starting server..." -ForegroundColor Green
    Write-Host ""
    dotnet run --project TradingEngine.csproj
} else {
    Write-Host ""
    Write-Host "Build failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}
