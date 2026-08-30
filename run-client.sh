#!/bin/bash

echo "========================================="
echo "Starting Trading Engine Test Client"
echo "========================================="
echo ""
echo "Make sure the server is running first!"
echo ""

# Build the project
echo "Building test client..."
dotnet build TestClient.csproj

if [ $? -eq 0 ]; then
    echo ""
    echo "Build successful! Starting test client..."
    echo ""
    dotnet run --project TestClient.csproj
else
    echo ""
    echo "Build failed. Please check the error messages above."
    exit 1
fi
