using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using TradingEngine.Data;
using TradingEngine.Data.Repositories;
using TradingEngine.Models;
using TradingEngine.Services;
using TradingEngine.Services.Quant;

namespace TradingEngine;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuredPort = builder.Configuration.GetValue<int?>("TradingServer:Port");
        var port = Environment.GetEnvironmentVariable("PORT")
            ?? configuredPort?.ToString()
            ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        // Bind configuration
        builder.Services.Configure<TradingServerConfig>(
            builder.Configuration.GetSection("TradingServer"));

        // Railway provides DATABASE_URL for the PostgreSQL service.
        var databaseUrl = builder.Configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("DATABASE_URL must be configured for PostgreSQL.");
        }

        builder.Services.AddDbContext<TradingDbContext>(options =>
            options.UseNpgsql(ToPostgresConnectionString(databaseUrl)));

        // Repositories
        builder.Services.AddScoped<ITradeRepository, TradeRepository>();
        builder.Services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
        builder.Services.AddScoped<IPerformanceMetricsRepository, PerformanceMetricsRepository>();

        // Risk Management
        builder.Services.AddSingleton<IRiskManagementService, RiskManagementService>();

        var firebaseServiceAccountJson = builder.Configuration["TradingServer:FirebaseServiceAccountJson"];
        var firebaseProjectId = builder.Configuration["TradingServer:FirebaseProjectId"];
        if (!string.IsNullOrWhiteSpace(firebaseServiceAccountJson))
        {
            builder.Services.AddSingleton<IFirebaseAuthenticationService, FirebaseAuthenticationService>();
        }
        builder.Logging.AddFilter("TradingEngine.Services", LogLevel.Information);
        Console.WriteLine(
            $"Firebase authentication configured: {!string.IsNullOrWhiteSpace(firebaseServiceAccountJson)}, " +
            $"project configured: {!string.IsNullOrWhiteSpace(firebaseProjectId)}");

        // Quant Services
        builder.Services.AddSingleton<IPdeModel, OcamlPdeBridge>();

        // Persistence Service
        builder.Services.AddScoped<IPersistenceService, PersistenceService>();

        // Register services as singletons
        builder.Services.AddSingleton<IMarketDataProvider, SimulatedMarketDataProvider>();
        builder.Services.AddSingleton<IMarketDataManager, MarketDataManager>();
        builder.Services.AddSingleton<IPortfolioManager, PortfolioManager>();
        builder.Services.AddSingleton<ILimitOrderBook, LimitOrderBook>();

        // Register order processing services
        builder.Services.AddSingleton<IOrderValidator, OrderValidator>();
        builder.Services.AddSingleton<IMatchingEngine, MatchingEngine>();
        builder.Services.AddSingleton<ITradeExecutor, TradeExecutor>();
        builder.Services.AddSingleton<IOrderHandler, OrderHandler>();

        // WebSocket Handler
        builder.Services.AddScoped<WebSocketOrderHandler>();

        var app = builder.Build();

        // Ensure DB is created
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            db.Database.EnsureCreated();
        }

        app.UseWebSockets();

        app.MapGet("/", () => Results.Content(
            """
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Fluffy Octo Engine</title>
              <style>
                :root { color-scheme: dark; font-family: Inter, system-ui, sans-serif; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center;
                       background: #0f172a; color: #e2e8f0; }
                main { width: min(680px, calc(100% - 40px)); padding: 40px; border: 1px solid #334155;
                       border-radius: 18px; background: #1e293b; box-shadow: 0 20px 60px #020617; }
                h1 { margin-top: 0; color: #a78bfa; }
                .status { display: inline-flex; gap: 8px; align-items: center; color: #86efac; }
                .dot { width: 10px; height: 10px; border-radius: 50%; background: #22c55e; }
                code { padding: 3px 7px; border-radius: 5px; background: #0f172a; color: #c4b5fd; }
                a { color: #c4b5fd; }
                li { margin: 12px 0; }
              </style>
            </head>
            <body>
              <main>
                <h1>Fluffy Octo Engine</h1>
                <p class="status"><span class="dot"></span> Trading engine is running</p>
                <p>This service exposes a WebSocket trading API.</p>
                <ul>
                  <li>Health: <a href="/health"><code>/health</code></a></li>
                  <li>WebSocket endpoint: <code>/ws</code></li>
                </ul>
                <p>Use a WebSocket client to authenticate and submit orders.</p>
              </main>
            </body>
            </html>
            """, "text/html"));

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.Map("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var handler = context.RequestServices.GetRequiredService<WebSocketOrderHandler>();
                await handler.HandleAsync(webSocket, context.RequestServices);
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Expected WebSocket request.");
            }
        });

        await app.RunAsync();
    }

    private static string ToPostgresConnectionString(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new InvalidOperationException("DATABASE_URL is not a valid PostgreSQL connection URL.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Require
        };

        return builder.ConnectionString;
    }
}
