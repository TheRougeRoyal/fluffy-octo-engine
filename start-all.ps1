#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the complete Trading Engine stack on Windows with Docker Compose.
.DESCRIPTION
    Starts PostgreSQL, the OCaml pricing engine inside the backend container,
    the .NET backend, and the React frontend.
#>

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Starting the complete Docker stack (PostgreSQL + OCaml + backend + frontend)..."
        docker compose up --build -d
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose failed to start the stack."
        }
        Write-Host ""
        Write-Host "Frontend:  http://localhost:3000"
        Write-Host "Backend:   http://localhost:5000"
        Write-Host "Health:    http://localhost:5000/health"
        Write-Host "PostgreSQL: localhost:5432"
        exit 0
    }
}

throw "Docker Desktop is not available. Install/start Docker Desktop and rerun this script."
