using TradingEngine.Models;

namespace TradingEngine.Services;

public interface IPortfolioManager
{
    decimal CashBalance { get; }
    Dictionary<string, Position> Positions { get; }
    
    bool HasSufficientCash(decimal amount);
    bool HasSufficientShares(string symbol, int quantity);
    void UpdateOnBuy(string symbol, int quantity, decimal price);
    void UpdateOnSell(string symbol, int quantity, decimal price);
    decimal GetBuyingPower();
    void DisplayPortfolio();
}
