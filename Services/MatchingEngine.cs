using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

/// <summary>
/// Implements order matching logic.
/// Market orders are matched unconditionally; limit orders are handled via the OrderBook.
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

        if (order.Side == OrderSide.Buy)
        {
            if (order.Price < marketPrice)
            {
                return (false, $"Buy limit price ${order.Price:N2} is below market price ${marketPrice:N2}");
            }

            return MatchBuyOrder(order, marketPrice);
        }

        if (order.Price > marketPrice)
        {
            return (false, $"Sell limit price ${order.Price:N2} is above market price ${marketPrice:N2}");
        }

        return MatchSellOrder(order, marketPrice);
    }

    private (bool IsMatched, string Reason) MatchBuyOrder(OrderRequest order, decimal marketPrice)
    {
        var orderValue = order.Quantity * marketPrice;
        if (!_portfolioManager.HasSufficientCash(orderValue))
        {
            return (false, $"Insufficient cash. Required: ${orderValue:N2}, Available: ${_portfolioManager.GetBuyingPower():N2}");
        }

        return (true, "Match successful");
    }


    private (bool IsMatched, string Reason) MatchSellOrder(OrderRequest order, decimal marketPrice)
    {
        if (!_portfolioManager.HasSufficientShares(order.Symbol, order.Quantity))
        {
            var heldQuantity = _portfolioManager.Positions.TryGetValue(order.Symbol, out var position)
                ? position.Quantity
                : 0;
            return (false, $"Insufficient shares of {order.Symbol}. Required: {order.Quantity}, Available: {heldQuantity}");
        }

        return (true, "Match successful");
    }
}
