using TradingEngine.DTOs;
using TradingEngine.Models;
using System.Collections.Generic;

namespace TradingEngine.Services;

public interface ILimitOrderBook
{
    void AddOrder(OrderRequest order);
    decimal GetBestBid(string symbol);
    decimal GetBestAsk(string symbol);
    bool TryMatch(OrderRequest order, out decimal fillPrice, out int fillQuantity);

    /// <summary>
    /// Matches an order iteratively against the book until it is fully filled or no more matches are possible.
    /// </summary>
    IEnumerable<(decimal Price, int Quantity)> MatchIteratively(OrderRequest order);
    void CancelOrder(string orderId, string symbol);
}
