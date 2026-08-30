using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingEngine.Data;
using TradingEngine.Data.Repositories;
using TradingEngine.Models;
using TradingEngine.Services;

namespace TradingEngine;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Trading Engine...");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();

        // Ensure database is created
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            db.Database.EnsureCreated();
        }

        await host.RunAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Bind configuration
                services.Configure<TradingServerConfig>(
                    context.Configuration.GetSection("TradingServer"));

                // Database Context
                services.AddDbContext<TradingDbContext>(options =>
                    options.UseSqlite("Data Source=trading.db"));

                // Repositories
                services.AddScoped<ITradeRepository, TradeRepository>();
                services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
                services.AddScoped<IPerformanceMetricsRepository, PerformanceMetricsRepository>();

                // Persistence Service
                services.AddScoped<IPersistenceService, PersistenceService>();

                // Register services as singletons (maintain state throughout application lifetime)
                services.AddSingleton<IMarketDataManager, MarketDataManager>();
                services.AddSingleton<IPortfolioManager, PortfolioManager>();

                // Register SOLID-compliant order processing services
                services.AddSingleton<IOrderValidator, OrderValidator>();
                services.AddSingleton<IMatchingEngine, MatchingEngine>();
                services.AddSingleton<ITradeExecutor, TradeExecutor>();
                services.AddSingleton<IOrderHandler, OrderHandler>();

                // Register the hosted service
                services.AddHostedService<TradingServerService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                
                // Set log levels from configuration
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            });
}
