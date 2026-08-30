using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class PortfolioManager : IPortfolioManager
{
    private readonly ILogger<PortfolioManager> _logger;
    private readonly object _lock = new();
    
    public decimal CashBalance { get; private set; }
    public Dictionary<string, Position> Positions { get; private set; }

    public PortfolioManager(
        ILogger<PortfolioManager> logger,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        CashBalance = config.Value.InitialCashBalance;
        Positions = new Dictionary<string, Position>();
        
        _logger.LogInformation("Portfolio Manager initialized with cash balance: ${Balance:N2}", CashBalance);
    }

    public bool HasSufficientCash(decimal amount)
    {
        lock (_lock)
        {
            return CashBalance >= amount;
        }
    }

    public bool HasSufficientShares(string symbol, int quantity)
    {
        lock (_lock)
        {
            return Positions.ContainsKey(symbol) && Positions[symbol].Quantity >= quantity;
        }
    }

    public void UpdateOnBuy(string symbol, int quantity, decimal price)
    {
        lock (_lock)
        {
            var totalCost = quantity * price;
            CashBalance -= totalCost;

            if (Positions.ContainsKey(symbol))
            {
                var position = Positions[symbol];
                var totalShares = position.Quantity + quantity;
                var totalValue = (position.Quantity * position.AverageCost) + totalCost;
                position.Quantity = totalShares;
                position.AverageCost = totalValue / totalShares;
            }
            else
            {
                Positions[symbol] = new Position
                {
                    Symbol = symbol,
                    Quantity = quantity,
                    AverageCost = price
                };
            }

            _logger.LogInformation(
                "BUY executed: {Symbol} x{Quantity} @ ${Price:N2} | Cash: ${Cash:N2} | Position: {Shares} shares @ ${AvgCost:N2}",
                symbol, quantity, price, CashBalance, Positions[symbol].Quantity, Positions[symbol].AverageCost);
        }
    }

    public void UpdateOnSell(string symbol, int quantity, decimal price)
    {
        lock (_lock)
        {
            var totalProceeds = quantity * price;
            CashBalance += totalProceeds;

            if (Positions.ContainsKey(symbol))
            {
                var position = Positions[symbol];
                position.Quantity -= quantity;

                if (position.Quantity == 0)
                {
                    Positions.Remove(symbol);
                    _logger.LogInformation(
                        "SELL executed: {Symbol} x{Quantity} @ ${Price:N2} | Cash: ${Cash:N2} | Position closed",
                        symbol, quantity, price, CashBalance);
                }
                else
                {
                    _logger.LogInformation(
                        "SELL executed: {Symbol} x{Quantity} @ ${Price:N2} | Cash: ${Cash:N2} | Position: {Shares} shares remaining",
                        symbol, quantity, price, CashBalance, position.Quantity);
                }
            }
        }
    }

    public decimal GetBuyingPower()
    {
        lock (_lock)
        {
            return CashBalance;
        }
    }

    public void DisplayPortfolio()
    {
        lock (_lock)
        {
            _logger.LogInformation("===== PORTFOLIO STATUS =====");
            _logger.LogInformation("Cash Balance: ${Cash:N2}", CashBalance);
            _logger.LogInformation("Positions:");
            
            if (Positions.Count == 0)
            {
                _logger.LogInformation("  No positions");
            }
            else
            {
                foreach (var position in Positions.Values)
                {
                    _logger.LogInformation(
                        "  {Symbol}: {Quantity} shares @ ${AvgCost:N2} avg (Total: ${Total:N2})",
                        position.Symbol, position.Quantity, position.AverageCost, position.TotalCost);
                }
            }
            
            _logger.LogInformation("============================");
        }
    }
}
