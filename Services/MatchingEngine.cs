using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

/// <summary>
/// Implements order matching logic.
/// Matches buy orders if price >= market price, sell orders if price <= market price.
/// </summary>
public class MatchingEngine : IMatchingEngine
{
    private readonly IPortfolioManager _portfolioManager;
    private readonly ILimitOrderBook _orderBook;

    public MatchingEngine(IPortfolioManager portfolioManager, ILimitOrderBook orderBook)
    {
        _portfolioManager = portfolioManager;
        _orderBook = orderBook;
    }

    public (bool IsMatched, string Reason) TryMatch(OrderRequest order, decimal marketPrice)
    {
        // First, try to match against the order book
        if (_orderBook.TryMatch(order, out decimal fillPrice, out int fillQuantity))
        {
            // For a simple engine, we just match the first available quantity
            // In a full engine, we'd handle partial fills
            return (true, "Matched against order book");
        }

        // Fallback to market price matching for Market orders or marketable Limit orders
        if (order.Side == OrderSide.Buy)
        {
            return MatchBuyOrder(order, marketPrice);
        }
        else
        {
            return MatchSellOrder(order, marketPrice);
        }
    }

    private (bool IsMatched, string Reason) MatchBuyOrder(OrderRequest order, decimal marketPrice)
    {
        // Buy order: execute if order price >= market price
        if (order.Price < marketPrice)
        {
            return (false, $"Buy order price ${order.Price:N2} is below market price ${marketPrice:N2}");
        }

        // Check if sufficient cash
        var requiredCash = order.Quantity * marketPrice;
        if (!_portfolioManager.HasSufficientCash(requiredCash))
        {
            return (false, $"Insufficient cash. Required: ${requiredCash:N2}, Available: ${_portfolioManager.GetBuyingPower():N2}");
        }

        return (true, "Match successful");
    }

    private (bool IsMatched, string Reason) MatchSellOrder(OrderRequest order, decimal marketPrice)
    {
        // Sell order: execute if order price <= market price
        if (order.Price > marketPrice)
        {
            return (false, $"Sell order price ${order.Price:N2} is above market price ${marketPrice:N2}");
        }

        // Check if sufficient shares
        if (!_portfolioManager.HasSufficientShares(order.Symbol, order.Quantity))
        {
            var currentPosition = _portfolioManager.Positions.ContainsKey(order.Symbol) 
                ? _portfolioManager.Positions[order.Symbol].Quantity 
                : 0;
            return (false, $"Insufficient shares. Required: {order.Quantity}, Available: {currentPosition}");
        }

        return (true, "Match successful");
    }
}
