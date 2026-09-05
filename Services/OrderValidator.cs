using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

/// <summary>
/// Validates order parameters.
/// Implements IOrderValidator interface for testability and flexibility.
/// </summary>
public class OrderValidator : IOrderValidator
{
    private readonly IMarketDataManager _marketDataManager;

    public OrderValidator(IMarketDataManager marketDataManager)
    {
        _marketDataManager = marketDataManager;
    }

    public (bool IsValid, string? ErrorMessage) Validate(OrderRequest order)
    {
        // Validate symbol
        if (string.IsNullOrWhiteSpace(order.Symbol))
        {
            return (false, "Symbol cannot be empty");
        }

        if (!_marketDataManager.IsValidSymbol(order.Symbol))
        {
            return (false, $"Invalid symbol: {order.Symbol}");
        }

        // Validate quantity
        if (order.Quantity <= 0)
        {
            return (false, "Quantity must be greater than 0");
        }

        // Validate price - only required for limit orders
        if (order.OrderType == OrderType.Limit && order.Price <= 0)
        {
            return (false, "Limit orders must have a price greater than 0");
        }

        return (true, null);
    }
}
