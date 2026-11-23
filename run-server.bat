@echo off
setlocal
set "DATABASE_URL=postgresql://postgres:password@localhost:5432/trading_engine"
set "DATABASE_SSL_MODE=Disable"
set "PORT=5000"

echo =========================================
echo Starting Trading Engine Server
echo =========================================
echo.

echo Building project...
dotnet build TradingEngine.csproj
if errorlevel 1 (
    echo.
    echo Build failed. Please check the error messages above.
    exit /b 1
)

echo.
echo Build successful! Starting server...
echo.
dotnet run --project TradingEngine.csproj
