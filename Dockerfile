# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update && apt-get install -y opam dune

# Init opam and install dune non-interactively
RUN opam init --disable-sandboxing && \
    eval $(opam env) && \
    opam install -y dune

WORKDIR /app
COPY . .

# Build OCaml QuantCore
WORKDIR /app/QuantCore
RUN dune build

# Build .NET TradingEngine
WORKDIR /app
RUN dotnet publish TradingEngine.csproj -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN apt-get update && apt-get install -y libgmp-dev

WORKDIR /app
COPY --from=build /app/publish .
# Copy the OCaml binaries into the expected path
COPY --from=build /app/QuantCore/_build/default /app/QuantCore/bin

# Railway provides the PORT env var
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradingEngine.dll"]
