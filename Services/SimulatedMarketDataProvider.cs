using System.Collections.Concurrent;
using TradingEngine.Services;

namespace TradingEngine.Services;

public class SimulatedMarketDataProvider : IMarketDataProvider
{
    private readonly ConcurrentDictionary<string, decimal> _prices = new();
    private readonly Random _random = new();

    public SimulatedMarketDataProvider()
    {
        // Initial seed prices
        var seeds = new Dictionary<string, decimal>
        {
            { "AAPL", 175.50m }, { "GOOGL", 142.30m }, { "MSFT", 378.90m },
            { "AMZN", 178.25m }, { "TSLA", 248.75m }, { "META", 485.60m }, { "NVDA", 875.40m }
        };

        foreach (var seed in seeds)
        {
            _prices[seed.Key] = seed.Value;
        }
    }

    public MarketPrice GetCurrentPrice(string symbol)
    {
        // ponytail: Simple random walk to simulate movement
        var current = _prices.GetOrAdd(symbol, _ => (decimal)(_random.NextDouble() * 450 + 50));
        var change = (decimal)(_random.NextDouble() * 0.02 - 0.01) * current; // +/- 1%
        var next = current + change;
        _prices[symbol] = next;

        return new MarketPrice(symbol, next, DateTime.UtcNow);
    }

    public IEnumerable<MarketPrice> GetAllPrices()
    {
        return _prices.Select(kvp => new MarketPrice(kvp.Key, kvp.Value, DateTime.UtcNow));
    }
}
