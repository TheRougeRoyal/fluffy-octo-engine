using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public interface ILimitOrderBook
{
    void AddOrder(OrderRequest order);
    decimal GetBestBid(string symbol);
    decimal GetBestAsk(string symbol);
    bool TryMatch(OrderRequest order, out decimal fillPrice, out int fillQuantity);
}
