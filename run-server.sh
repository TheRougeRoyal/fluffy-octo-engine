#!/bin/bash

echo "========================================="
echo "Starting Trading Engine Server"
echo "========================================="
echo ""

# Build the project
echo "Building project..."
dotnet build TradingEngine.csproj

if [ $? -eq 0 ]; then
    echo ""
    echo "Build successful! Starting server..."
    echo ""
    dotnet run --project TradingEngine.csproj
else
    echo ""
    echo "Build failed. Please check the error messages above."
    exit 1
fi
