using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using TradingEngine.Data.Repositories;
using TradingEngine.Models;

namespace TradingEngine.Services;

public interface IPersistenceService
{
    Task OnTradeExecutedAsync(string orderId, string symbol, int quantity,
        decimal executionPrice, OrderSide side, decimal cashBefore, decimal cashAfter);
    Task SavePortfolioSnapshotAsync(decimal cash, Dictionary<string, Position> positions);
    Task CalculateAndSaveMetricsAsync();
}

public class PersistenceService : IPersistenceService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPerformanceMetricsRepository _metricsRepository;
    private readonly IMarketDataManager _marketDataManager;
    private readonly ILogger<PersistenceService> _logger;

    public PersistenceService(
        ITradeRepository tradeRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IPerformanceMetricsRepository metricsRepository,
        IMarketDataManager marketDataManager,
        ILogger<PersistenceService> logger)
    {
        _tradeRepository = tradeRepository;
        _snapshotRepository = snapshotRepository;
        _metricsRepository = metricsRepository;
        _marketDataManager = marketDataManager;
        _logger = logger;
    }

    public async Task OnTradeExecutedAsync(string orderId, string symbol, int quantity,
        decimal executionPrice, OrderSide side, decimal cashBefore, decimal cashAfter)
    {
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
                CashAfterTransaction = cashAfter
            };

            await _tradeRepository.SaveTradeAsync(trade);
            _logger.LogInformation($"Trade persisted: {orderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error persisting trade: {ex.Message}");
            throw;
        }
    }

    public async Task SavePortfolioSnapshotAsync(decimal cash, Dictionary<string, Position> positions)
    {
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

            await _snapshotRepository.SaveSnapshotAsync(snapshot);
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
        try
        {
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var today = DateTime.UtcNow;
            var metrics = await _metricsRepository.CalculateMetricsAsync(yesterday, today);
            await _metricsRepository.SaveMetricsAsync(metrics);
            _logger.LogInformation("Performance metrics calculated and saved");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating metrics: {ex.Message}");
            throw;
        }
    }
}
