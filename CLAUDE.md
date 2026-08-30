# Fluffy Octo Engine

A multi-component trading engine with C# and OCaml integration.

## Architecture
- **Trading Server**: TCP server handling JSON order requests.
- **Order Handler**: Orchestrates validation, matching, and execution.
- **Limit Order Book**: Maintains bid/ask queues for symbols.
- **Portfolio Manager**: Manages cash and positions with thread-safe locks.
- **Market Data Manager**: Provides real-time prices via a provider abstraction.
- **QuantCore**: OCaml-based PDE solver for fair value pricing.

## Technical Stack
- .NET 8
- EF Core & SQLite (Persistence)
- TCP/IP Networking
- OCaml (Quantitative Analysis)

## Development
- **Build**: `dotnet build`
- **Run**: `dotnet run --project TradingEngine.csproj`
- **Test**: `dotnet test`

## Key Improvements (2026 Update)
- Upgraded to .NET 8.
- Implemented Limit Order Book (LOB) for more realistic matching.
- Added Market Data Provider abstraction with a simulated random-walk feed.
- Integrated basic API Key authentication for the TCP server.
- Strengthened OCaml bridge with binary validation and improved error handling.
