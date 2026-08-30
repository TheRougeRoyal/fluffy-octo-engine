using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingEngine.Data.Models;

namespace TradingEngine.Data.Repositories;

public interface ITradeRepository
{
    Task<int> SaveTradeAsync(TradeEntity trade);
    Task<TradeEntity?> GetTradeByIdAsync(int id);
    Task<IEnumerable<TradeEntity>> GetTradesAsync(DateTime from, DateTime to);
    Task<IEnumerable<TradeEntity>> GetTradesBySymbolAsync(string symbol);
    Task<int> GetTradeCountAsync();
    Task<decimal> CalculateTotalVolumeAsync(DateTime from, DateTime to);
}
