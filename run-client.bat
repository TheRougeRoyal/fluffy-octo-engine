@echo off
setlocal

echo =========================================
echo Starting Trading Engine Test Client
echo =========================================
echo.
echo Make sure the server is running first!
echo.

echo Building test client...
dotnet build clients\TestClient.csproj
if errorlevel 1 (
    echo.
    echo Build failed. Please check the error messages above.
    exit /b 1
)

echo.
echo Build successful! Starting test client...
echo.
dotnet run --project clients\TestClient.csproj
