using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradingEngine.Data.Repositories;

public class PortfolioSnapshotRepository : IPortfolioSnapshotRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<PortfolioSnapshotRepository> _logger;

    public PortfolioSnapshotRepository(TradingDbContext context, ILogger<PortfolioSnapshotRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveSnapshotAsync(PortfolioSnapshotEntity snapshot)
    {
        _context.PortfolioSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Portfolio snapshot saved at {snapshot.SnapshotTime}");
        return snapshot.Id;
    }

    public async Task<PortfolioSnapshotEntity?> GetLatestSnapshotAsync()
    {
        return await _context.PortfolioSnapshots
            .Include(p => p.Positions)
            .OrderByDescending(p => p.SnapshotTime)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PortfolioSnapshotEntity>> GetSnapshotHistoryAsync(DateTime from, DateTime to)
    {
        return await _context.PortfolioSnapshots
            .Include(p => p.Positions)
            .Where(p => p.SnapshotTime >= from && p.SnapshotTime <= to)
            .OrderByDescending(p => p.SnapshotTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<PortfolioSnapshotEntity>> GetDailySnapshotsAsync(int days)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        return await GetSnapshotHistoryAsync(startDate, DateTime.UtcNow);
    }

    public async Task<decimal> GetPortfolioValueAtDateAsync(DateTime date)
    {
        var snapshot = await _context.PortfolioSnapshots
            .Where(p => p.SnapshotTime <= date)
            .OrderByDescending(p => p.SnapshotTime)
            .FirstOrDefaultAsync();

        return snapshot?.TotalPortfolioValue ?? 0;
    }
}
