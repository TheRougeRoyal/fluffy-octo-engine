namespace TradingEngine.Services;

public interface IMarketDataManager
{
    decimal GetPrice(string symbol);
    bool IsValidSymbol(string symbol);
    void UpdatePrice(string symbol, decimal price);
    Dictionary<string, decimal> GetAllPrices();
}
