using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class MarketDataManager : IMarketDataManager
{
    private readonly ILogger<MarketDataManager> _logger;
    private readonly IMarketDataProvider _dataProvider;
    private readonly HashSet<string> _validSymbols;
    private readonly object _lock = new();

    public MarketDataManager(
        ILogger<MarketDataManager> logger,
        IOptions<TradingServerConfig> config,
        IMarketDataProvider dataProvider)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _validSymbols = new HashSet<string>(config.Value.TradeableSymbols);

        _logger.LogInformation("Market Data Manager initialized with {Count} symbols", _validSymbols.Count);
    }

    public decimal GetPrice(string symbol)
    {
        if (!_validSymbols.Contains(symbol))
        {
            throw new InvalidOperationException($"No price data available for symbol: {symbol}");
        }
        return _dataProvider.GetCurrentPrice(symbol).Price;
    }

    public bool IsValidSymbol(string symbol)
    {
        return _validSymbols.Contains(symbol);
    }

    public void UpdatePrice(string symbol, decimal price)
    {
        // ponytail: In a real provider, this would push to a feed.
        // For the simulated one, we might just ignore it or update a local cache.
        _logger.LogInformation("Price update request: {Symbol} @ ${Price:N2}", symbol, price);
    }

    public Dictionary<string, decimal> GetAllPrices()
    {
        return _dataProvider.GetAllPrices()
            .Where(p => _validSymbols.Contains(p.Symbol))
            .ToDictionary(p => p.Symbol, p => p.Price);
    }
}
