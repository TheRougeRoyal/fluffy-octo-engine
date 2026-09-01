using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        // Bind configuration
        builder.Services.Configure<TradingServerConfig>(
            builder.Configuration.GetSection("TradingServer"));

        // Database Context
        builder.Services.AddDbContext<TradingDbContext>(options =>
            options.UseSqlite("Data Source=/data/trading.db"));

        // Repositories
        builder.Services.AddScoped<ITradeRepository, TradeRepository>();
        builder.Services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
        builder.Services.AddScoped<IPerformanceMetricsRepository, PerformanceMetricsRepository>();

        // Risk Management
        builder.Services.AddSingleton<IRiskManagementService, RiskManagementService>();

        // Quant Services
        builder.Services.AddSingleton<IPdeModel, OcamlPdeBridge>();

        // Persistence Service
        builder.Services.AddScoped<IPersistenceService, PersistenceService>();

        // Register services as singletons
        builder.Services.AddSingleton<IMarketDataManager, MarketDataManager>();
        builder.Services.AddSingleton<IPortfolioManager, PortfolioManager>();

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
}
