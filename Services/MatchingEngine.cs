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
        // We've moved the LOB matching logic to the OrderHandler to support iterative fills
        // But we still support market price matching for Market orders here.

        if (order.OrderType == OrderType.Market)
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

        return (false, "Use OrderBook for Limit orders");
    }

    private (bool IsMatched, string Reason) MatchBuyOrder(OrderRequest order, decimal marketPrice)
    {
        // Buy order: execute if order price >= market price
        if (order.Price < marketPrice)
        {
            return (false, $"Buy order price ${order.Price:N2} is below market price ${marketPrice:N2}");
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

        return (true, "Match successful");
    }
}
