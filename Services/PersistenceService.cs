using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using TradingEngine.Data.Repositories;
using TradingEngine.Models;
using TradingEngine.Models.Quant;

namespace TradingEngine.Services;

public interface IPersistenceService
{
    Task OnTradeExecutedAsync(string orderId, string symbol, int quantity,
        decimal executionPrice, OrderSide side, decimal cashBefore, decimal cashAfter, Greeks greeks);
    Task SavePortfolioSnapshotAsync(decimal cash, Dictionary<string, Position> positions);
    Task CalculateAndSaveMetricsAsync();
    Task<bool> TradeExistsAsync(string orderId);
}

public class PersistenceService : IPersistenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMarketDataManager _marketDataManager;
    private readonly ILogger<PersistenceService> _logger;

    public PersistenceService(
        IServiceScopeFactory scopeFactory,
        IMarketDataManager marketDataManager,
        ILogger<PersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _marketDataManager = marketDataManager;
        _logger = logger;
    }

    public async Task OnTradeExecutedAsync(string orderId, string symbol, int quantity,
        decimal executionPrice, OrderSide side, decimal cashBefore, decimal cashAfter, Greeks greeks)
    {
        using var scope = _scopeFactory.CreateScope();
        var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();

        try
        {
            var trade = new TradeEntity
            {
                OrderId = orderId,
                Symbol = symbol,
                Quantity = quantity,
                ExecutionPrice = executionPrice,
                Side = side == OrderSide.Buy ? "Buy" : "Sell",
                ExecutedAt = DateTime.UtcNow,
                CashBeforeTransaction = cashBefore,
                CashAfterTransaction = cashAfter,
                Delta = greeks.Delta,
                Gamma = greeks.Gamma,
                Theta = greeks.Theta,
                Vega = greeks.Vega,
                Rho = greeks.Rho
            };

            await tradeRepository.SaveTradeAsync(trade);
            _logger.LogInformation($"Trade persisted with Greeks: {orderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error persisting trade: {ex.Message}");
            throw;
        }
    }

    public async Task SavePortfolioSnapshotAsync(decimal cash, Dictionary<string, Position> positions)
    {
        using var scope = _scopeFactory.CreateScope();
        var snapshotRepository = scope.ServiceProvider.GetRequiredService<IPortfolioSnapshotRepository>();

        try
        {
            var snapshot = new PortfolioSnapshotEntity
            {
                SnapshotTime = DateTime.UtcNow,
                CashBalance = cash,
                TotalPortfolioValue = cash + positions.Sum(p => p.Value.Quantity * _marketDataManager.GetPrice(p.Key)),
                Positions = positions.Select(p => new PositionSnapshotEntity
                {
                    Symbol = p.Key,
                    Quantity = p.Value.Quantity,
                    AverageCost = p.Value.AverageCost,
                    CurrentPrice = _marketDataManager.GetPrice(p.Key)
                }).ToList()
            };

            await snapshotRepository.SaveSnapshotAsync(snapshot);
            _logger.LogInformation($"Portfolio snapshot saved");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving portfolio snapshot: {ex.Message}");
            throw;
        }
    }

    public async Task CalculateAndSaveMetricsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var metricsRepository = scope.ServiceProvider.GetRequiredService<IPerformanceMetricsRepository>();

        try
        {
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var today = DateTime.UtcNow;
            var metrics = await metricsRepository.CalculateMetricsAsync(yesterday, today);
            await metricsRepository.SaveMetricsAsync(metrics);
            _logger.LogInformation("Performance metrics calculated and saved");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating metrics: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> TradeExistsAsync(string orderId)
    {
        using var scope = _scopeFactory.CreateScope();
        var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();
        return await tradeRepository.TradeExistsAsync(orderId);
    }
}
