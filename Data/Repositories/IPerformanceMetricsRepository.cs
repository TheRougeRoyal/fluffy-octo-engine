using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingEngine.Data.Models;

namespace TradingEngine.Data.Repositories;

public interface IPerformanceMetricsRepository
{
    Task<int> SaveMetricsAsync(PerformanceMetricsEntity metrics);
    Task<PerformanceMetricsEntity?> GetLatestMetricsAsync();
    Task<IEnumerable<PerformanceMetricsEntity>> GetMetricsHistoryAsync(int periods);
    Task<PerformanceMetricsEntity> CalculateMetricsAsync(DateTime from, DateTime to);
}
