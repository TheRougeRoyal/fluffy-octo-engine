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

    public MatchingEngine(IPortfolioManager portfolioManager)
    {
        _portfolioManager = portfolioManager;
    }

    public (bool IsMatched, string Reason) TryMatch(OrderRequest order, decimal marketPrice)
    {
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
