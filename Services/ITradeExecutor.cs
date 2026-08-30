using TradingEngine.DTOs;

namespace TradingEngine.Services;

/// <summary>
/// Responsible for executing trades and updating portfolio.
/// Follows Single Responsibility Principle - execution only.
/// </summary>
public interface ITradeExecutor
{
    /// <summary>
    /// Executes a trade and updates the portfolio.
    /// </summary>
    /// <param name="order">The order to execute</param>
    /// <param name="executionPrice">Price at which the trade executes</param>
    void ExecuteTrade(OrderRequest order, decimal executionPrice);
}
