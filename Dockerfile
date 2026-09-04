# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update \
    && apt-get install -y --no-install-recommends dune libyojson-ocaml-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . .

# Build OCaml QuantCore
WORKDIR /app/QuantCore
RUN dune build bin/pricing_api.exe

# Build .NET TradingEngine
WORKDIR /app
RUN dotnet publish TradingEngine.csproj -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y libgmp-dev

WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/QuantCore/bin
# Keep the runtime path stable across local and container builds.
COPY --from=build /app/QuantCore/_build/default/bin/pricing_api.exe /app/QuantCore/bin/pricing_api
RUN chmod +x /app/QuantCore/bin/pricing_api

# Railway provides the PORT env var
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradingEngine.dll"]
