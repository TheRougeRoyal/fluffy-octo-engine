using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradingEngine.Data.Repositories;

public class PerformanceMetricsRepository : IPerformanceMetricsRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<PerformanceMetricsRepository> _logger;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;

    public PerformanceMetricsRepository(
        TradingDbContext context,
        ILogger<PerformanceMetricsRepository> logger,
        ITradeRepository tradeRepository,
        IPortfolioSnapshotRepository snapshotRepository)
    {
        _context = context;
        _logger = logger;
        _tradeRepository = tradeRepository;
        _snapshotRepository = snapshotRepository;
    }

    public async Task<int> SaveMetricsAsync(PerformanceMetricsEntity metrics)
    {
        _context.PerformanceMetrics.Add(metrics);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Performance metrics saved for period {metrics.PeriodStart} to {metrics.PeriodEnd}");
        return metrics.Id;
    }

    public async Task<PerformanceMetricsEntity?> GetLatestMetricsAsync()
    {
        return await _context.PerformanceMetrics
            .OrderByDescending(m => m.CalculatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PerformanceMetricsEntity>> GetMetricsHistoryAsync(int periods)
    {
        return await _context.PerformanceMetrics
            .OrderByDescending(m => m.CalculatedAt)
            .Take(periods)
            .ToListAsync();
    }

    public async Task<PerformanceMetricsEntity> CalculateMetricsAsync(DateTime from, DateTime to)
    {
        var trades = await _tradeRepository.GetTradesAsync(from, to);
        var snapshots = await _snapshotRepository.GetSnapshotHistoryAsync(from, to);

        var tradeList = trades.ToList();
        var snapshotList = snapshots.ToList();

        // Calculate returns
        var startValue = snapshotList.LastOrDefault()?.TotalPortfolioValue ?? 100000;
        var endValue = snapshotList.FirstOrDefault()?.TotalPortfolioValue ?? 100000;
        var totalReturn = (endValue - startValue) / startValue;

        // Calculate drawdown
        var maxValue = snapshotList.Any() ? snapshotList.Max(s => s.TotalPortfolioValue) : 0;
        var minValue = snapshotList.Any() ? snapshotList.Min(s => s.TotalPortfolioValue) : 0;
        var maxDrawdown = maxValue > 0 ? (maxValue - minValue) / maxValue : 0;

        // Calculate win rate
        var winningTrades = tradeList.Count(t => t.ExecutionPrice > 100); // Simplified
        var losingTrades = tradeList.Count(t => t.ExecutionPrice < 100);
        var winRate = tradeList.Count > 0 ? (decimal)winningTrades / tradeList.Count : 0;

        return new PerformanceMetricsEntity
        {
            CalculatedAt = DateTime.UtcNow,
            PeriodStart = from,
            PeriodEnd = to,
            TotalReturn = totalReturn,
            AnnualizedReturn = totalReturn * 252, // Trading days per year
            MaxDrawdown = maxDrawdown,
            SharpeRatio = CalculateSharpeRatio(snapshotList),
            TotalTrades = tradeList.Count,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate
        };
    }

    private decimal CalculateSharpeRatio(List<PortfolioSnapshotEntity> snapshots)
    {
        if (snapshots.Count < 2) return 0;

        var returns = new List<decimal>();
        for (int i = 0; i < snapshots.Count - 1; i++)
        {
            var ret = (snapshots[i].TotalPortfolioValue - snapshots[i + 1].TotalPortfolioValue)
                      / snapshots[i + 1].TotalPortfolioValue;
            returns.Add(ret);
        }

        var avgReturn = returns.Average();
        var variance = returns.Sum(r => (decimal)Math.Pow((double)(r - avgReturn), 2)) / returns.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        return stdDev > 0 ? avgReturn / stdDev : 0;
    }
}
