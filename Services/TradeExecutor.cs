using Microsoft.Extensions.Logging;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

/// <summary>
/// Executes trades and updates portfolio state.
/// Maintains trade history for audit trail.
/// </summary>
public class TradeExecutor : ITradeExecutor
{
    private readonly ILogger<TradeExecutor> _logger;
    private readonly IPortfolioManager _portfolioManager;
    private readonly List<Trade> _tradeHistory;
    private readonly object _lock = new();

    public TradeExecutor(
        ILogger<TradeExecutor> logger,
        IPortfolioManager portfolioManager)
    {
        _logger = logger;
        _portfolioManager = portfolioManager;
        _tradeHistory = new List<Trade>();
    }

    public void ExecuteTrade(OrderRequest order, decimal executionPrice)
    {
        lock (_lock)
        {
            if (order.Side == OrderSide.Buy)
            {
                _portfolioManager.UpdateOnBuy(order.Symbol, order.Quantity, executionPrice);
            }
            else
            {
                _portfolioManager.UpdateOnSell(order.Symbol, order.Quantity, executionPrice);
            }

            // Record the trade for history
            var trade = new Trade
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = order.Symbol,
                Quantity = order.Quantity,
                Price = executionPrice,
                Side = order.Side,
                Timestamp = DateTime.UtcNow
            };

            _tradeHistory.Add(trade);
            _logger.LogInformation(
                "{Side} trade executed: {Symbol} x{Quantity} @ ${Price:N2}",
                order.Side, order.Symbol, order.Quantity, executionPrice);
        }
    }

    public List<Trade> GetTradeHistory()
    {
        lock (_lock)
        {
            return new List<Trade>(_tradeHistory);
        }
    }
}
