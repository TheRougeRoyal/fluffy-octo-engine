namespace TradingEngine.Services;

public record MarketPrice(string Symbol, decimal Price, DateTime Timestamp);

public interface IMarketDataProvider
{
    MarketPrice GetCurrentPrice(string symbol);
    IEnumerable<MarketPrice> GetAllPrices();
}
