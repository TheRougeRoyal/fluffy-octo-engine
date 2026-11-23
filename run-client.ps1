#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and run the Trading Engine test client on Windows.
.DESCRIPTION
    Builds the TestClient project and connects to the running server.
    Make sure the server is running first!
#>

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Starting Trading Engine Test Client" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Make sure the server is running first!" -ForegroundColor Yellow
Write-Host ""

Write-Host "Building test client..." -ForegroundColor Yellow
dotnet build clients\TestClient.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful! Starting test client..." -ForegroundColor Green
    Write-Host ""
    dotnet run --project clients\TestClient.csproj
} else {
    Write-Host ""
    Write-Host "Build failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}
