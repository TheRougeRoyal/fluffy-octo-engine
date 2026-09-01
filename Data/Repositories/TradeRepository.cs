using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradingEngine.Data.Repositories;

public class TradeRepository : ITradeRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<TradeRepository> _logger;

    public TradeRepository(TradingDbContext context, ILogger<TradeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveTradeAsync(TradeEntity trade)
    {
        _context.Trades.Add(trade);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Trade saved: {trade.OrderId}");
        return trade.Id;
    }

    public async Task<TradeEntity?> GetTradeByIdAsync(int id)
    {
        return await _context.Trades.FindAsync(id);
    }

    public async Task<IEnumerable<TradeEntity>> GetTradesAsync(DateTime from, DateTime to)
    {
        return await _context.Trades
            .Where(t => t.ExecutedAt >= from && t.ExecutedAt <= to)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TradeEntity>> GetTradesBySymbolAsync(string symbol)
    {
        return await _context.Trades
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync();
    }

    public async Task<int> GetTradeCountAsync()
    {
        return await _context.Trades.CountAsync();
    }

    public async Task<bool> TradeExistsAsync(string orderId)
    {
        return await _context.Trades.AnyAsync(t => t.OrderId == orderId);
    }

    public async Task<decimal> CalculateTotalVolumeAsync(DateTime from, DateTime to)
    {
        return await _context.Trades
            .Where(t => t.ExecutedAt >= from && t.ExecutedAt <= to)
            .SumAsync(t => t.Quantity * t.ExecutionPrice);
    }
}
