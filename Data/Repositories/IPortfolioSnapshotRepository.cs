using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingEngine.Data.Models;

namespace TradingEngine.Data.Repositories;

public interface IPortfolioSnapshotRepository
{
    Task<int> SaveSnapshotAsync(PortfolioSnapshotEntity snapshot);
    Task<PortfolioSnapshotEntity?> GetLatestSnapshotAsync();
    Task<IEnumerable<PortfolioSnapshotEntity>> GetSnapshotHistoryAsync(DateTime from, DateTime to);
    Task<IEnumerable<PortfolioSnapshotEntity>> GetDailySnapshotsAsync(int days);
    Task<decimal> GetPortfolioValueAtDateAsync(DateTime date);
}
