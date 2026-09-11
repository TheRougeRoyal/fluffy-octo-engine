# Fluffy Octo Engine

A simulated equity trading engine with a quantitative pricing core. Clients connect over WebSocket, authenticate, and submit JSON orders. An in-memory limit order book handles matching; PostgreSQL persists trades, portfolio snapshots, and performance metrics; and an OCaml PDE solver provides options pricing and Greeks on demand.

> **Simulation only.** Market data is a random walk. There is no integration with a real broker or live data feed. The engine is intentionally single-instance — the in-memory order book and portfolio are not distributed. State survives restarts by being reloaded from Postgres.

---

## Architecture

```
┌─────────────────────┐        WebSocket JSON        ┌─────────────────────────┐
│   React Frontend    │ ◄───────────────────────────► │  ASP.NET Core 8 Server  │
│  (Vite + Firebase)  │                               │  WebSocketOrderHandler  │
└─────────────────────┘                               └──────────┬──────────────┘
                                                                  │
                                          ┌───────────────────────▼───────────────────────┐
                                          │                 OrderHandler                   │
                                          │  Validate → Risk Check → Match → Execute →    │
                                          │  Persist                                       │
                                          └──┬───────────────┬───────────────┬────────────┘
                                             │               │               │
                                   ┌─────────▼──────┐ ┌──────▼──────┐ ┌────▼──────────┐
                                   │ LimitOrderBook │ │  Portfolio  │ │  Persistence  │
                                   │  (in-memory)   │ │  Manager    │ │  Service      │
                                   └────────────────┘ └─────────────┘ └───────┬───────┘
                                                                               │
                                                                        ┌──────▼──────┐
                                                                        │  PostgreSQL  │
                                                                        └─────────────┘
                                             │
                                   ┌─────────▼──────────────┐
                                   │   OcamlPdeBridge        │
                                   │  (subprocess stdio)     │
                                   └─────────┬──────────────┘
                                             │
                                   ┌─────────▼──────────────┐
                                   │  QuantCore pricing_api  │
                                   │  (OCaml binary: PDE,    │
                                   │   Greeks, implied vol)  │
                                   └────────────────────────┘
```

---

## Components

### C# Backend (`TradingEngine`)

| Component | Description |
|---|---|
| `WebSocketOrderHandler` | Entry point for all client sessions. Enforces a 30-second auth deadline, then loops reading JSON `OrderRequest` messages and writing back `OrderResponse`. |
| `OrderHandler` | Orchestrates the full order lifecycle: validation → duplicate check → market data → risk management → LOB matching → trade execution → persistence. |
| `LimitOrderBook` | Per-symbol order books backed by sorted price levels (bids descending, asks ascending). Supports price-time priority matching, partial fills, and O(1) cancellation via an internal order index. |
| `MatchingEngine` | Handles market orders by matching against the simulated market price. |
| `RiskManagementService` | Enforces five sequential limits: max order value, available cash, short-sell check, max per-symbol position value, and max total portfolio exposure. |
| `PortfolioManager` | Holds in-memory cash balance and positions. Updates weighted average cost on buys. |
| `SimulatedMarketDataProvider` | Seeds seven symbols (AAPL, GOOGL, MSFT, AMZN, TSLA, META, NVDA) with realistic prices, each `GetCurrentPrice` call applying a ±1% random walk. |
| `PersistenceService` | Writes trade records, portfolio snapshots, and performance metrics to Postgres. Creates its own DI scope per operation. |
| `OcamlPdeBridge` | Spawns the `pricing_api` binary, writes a JSON request to stdin, reads the JSON response with a 5-second timeout, and records duration metrics. |
| `FirebaseAuthenticationService` | Verifies Firebase JWT ID tokens. Falls back to a static API key if Firebase is not configured. |

### OCaml QuantCore (`QuantCore/`)

A self-contained quant library (package `pde_opt`). Built with Dune and compiled to a standalone binary (`pricing_api`) that communicates via JSON on stdin/stdout.

| Module | Description |
|---|---|
| `pde1d` | 1D Black-Scholes PDE solver using finite differences. Supports Backward Euler (θ=1) and Crank-Nicolson (θ=0.5) time stepping. |
| `tridiag` | Thomas algorithm tridiagonal solver, used at each time step. |
| `pricing` | Top-level interface. `price_option` returns PDE price, analytic Black-Scholes price, relative error, and all five Greeks (Δ, Γ, Θ, ν, ρ). |
| `implied_vol` | Newton-Raphson and bisection implied volatility solvers. |
| `calibration` | Estimates historical volatility and drift (Simple, EWMA, Combined). Estimates risk-free rate via CAPM. |
| `backtesting` | Runs predictions against historical prices, computes MAE, RMSE, correlation. |
| `grid_cache` | LRU-style caching of computed grids to avoid redundant work across repeated requests. |
| `crypto_model` | Monte Carlo forecasting for crypto assets. |

The `pricing_api` binary dispatches on a `requestType` field:
- `"impliedVol"` — Newton-Raphson implied vol solve.
- Anything else (or omitted) — PDE pricing with Greeks.

> The quant guardrail in `OrderHandler` is currently **disabled for equities** (commented out pending options trading). Greeks columns in the database are always `0` for current trades.

### React Frontend (`frontend/`)

Built with Vite, React 18, TypeScript, and Tailwind CSS. Uses Firebase Auth to obtain ID tokens, which are sent as the first message over the WebSocket. Deployed to Vercel.

---

## Database Schema

Four tables managed by Entity Framework Core migrations (applied automatically at startup):

- **`Trades`** — Primary trade record. Includes `OrderId` (server-assigned, unique), `ClientOrderId` (client-supplied, unique partial index for idempotency), symbol, quantity, execution price, side, timestamps, cash balances before/after, and Greek columns.
- **`PortfolioSnapshots`** — Point-in-time portfolio snapshots with cash balance and total value.
- **`PositionSnapshots`** — Per-symbol position data within a snapshot.
- **`PerformanceMetrics`** — Calculated analytics: total return, Sharpe ratio, max drawdown, win rate.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- OCaml + opam + Dune (`opam install yojson`)
- PostgreSQL (or Docker for a local instance)

---

## Local Setup

### 1. Build QuantCore

```bash
cd QuantCore
opam install yojson
dune build bin/pricing_api.exe
cd ..
```

### 2. Configure environment

```bash
export DATABASE_URL="postgres://user:pass@localhost:5432/trading"
export DATABASE_SSL_MODE="Disable"           # for local dev without TLS

# Authentication — choose one:
export TradingServer__ApiKey="your-secret-key"
# Or, for Firebase:
export TradingServer__FirebaseServiceAccountJson='{ ... service account JSON ... }'
export TradingServer__FirebaseProjectId="your-project-id"
```

### 3. Run the server

```bash
dotnet run
# or
./run-server.sh
```

The server starts on port `5000` by default. Override with `PORT` or `TradingServer__Port`.

### 4. Run the test client

```bash
./run-client.sh
```

### 5. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

---

## Docker

```bash
docker build -t fluffy-octo-engine .
docker run \
  -e DATABASE_URL="postgres://user:pass@host:5432/trading" \
  -e TradingServer__ApiKey="your-secret-key" \
  -p 8080:8080 \
  fluffy-octo-engine
```

The multi-stage Dockerfile builds the OCaml binary first (`dune build`), then the .NET app (`dotnet publish`), and copies both to the ASP.NET runtime image.

---

## Configuration Reference

All settings live under the `TradingServer` section of `appsettings.json` and can be overridden with environment variables using `TradingServer__<Field>` naming.

| Variable | Default | Description |
|---|---|---|
| `DATABASE_URL` | — | **Required.** PostgreSQL connection URL (`postgres://user:pass@host:5432/db`). |
| `DATABASE_SSL_MODE` | `Require` | Npgsql SSL mode. Set `Disable` for local dev. |
| `PORT` | `5000` | HTTP listen port. Railway sets this automatically. |
| `TradingServer__FirebaseServiceAccountJson` | — | Full Firebase service account JSON. Enables Firebase JWT auth when set. |
| `TradingServer__FirebaseProjectId` | — | Firebase project ID. |
| `TradingServer__ApiKey` | — | Fallback static credential. Used when Firebase is not configured. |
| `TradingServer__InitialCashBalance` | `100000` | Starting cash balance. |
| `TradingServer__MaxOrderValue` | `1000000` | Maximum single order value. |
| `TradingServer__MaxPositionValue` | `10000000` | Maximum value of a single symbol position. |
| `TradingServer__MaxPortfolioExposure` | `50000000` | Maximum total portfolio exposure. |
| `TradingServer__TradeableSymbols` | AAPL, GOOGL, MSFT, AMZN, TSLA, META, NVDA | Allowed trading symbols. |
| `TradingServer__PdeBinaryPath` | `QuantCore/bin/pricing_api` | Path to the OCaml pricing binary. |

---

## WebSocket API

Connect to `ws://<host>/ws`. After connecting:

1. The server sends: `Trading Server Ready. Please send your Firebase ID token first.`
2. Send your Firebase ID token (or API key if using fallback auth).
3. On success the server sends: `Authenticated. You can now send JSON order requests.`
4. Send order requests as JSON. Receive order responses as JSON.

### Order Request

```json
{
  "orderId": "client-order-id-123",
  "symbol": "AAPL",
  "side": "Buy",
  "orderType": "Limit",
  "quantity": 10,
  "price": 185.00,
  "timeInForce": "GTC"
}
```

`orderId` is optional but recommended for idempotency. `orderType` can be `"Market"` or `"Limit"`. `side` is `"Buy"` or `"Sell"`.

### Order Response

```json
{
  "orderId": "ORD-20260911-000001",
  "status": "Executed",
  "executedPrice": 185.00,
  "executedQuantity": 10,
  "message": "Order executed successfully at avg price $185.00"
}
```

`status` can be `Executed`, `Pending` (limit order resting in the book), or `Rejected`.

---

## Endpoints

| Path | Description |
|---|---|
| `GET /` | Status page (HTML) |
| `GET /health` | JSON health check for Postgres connectivity and PDE binary presence |
| `WS /ws` | WebSocket trading endpoint |

---

## Deployment

The project is configured for [Railway](https://railway.app/). Railway provides the `DATABASE_URL` and `PORT` environment variables automatically when a PostgreSQL plugin is attached. Set the remaining `TradingServer__*` variables in the Railway service environment.

The frontend is deployed separately to [Vercel](https://vercel.com/).
