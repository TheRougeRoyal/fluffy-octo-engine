using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class MarketDataManager : IMarketDataManager
{
    private readonly ILogger<MarketDataManager> _logger;
    private readonly IMarketDataProvider _dataProvider;
    private readonly HashSet<string> _validSymbols;
    private readonly Dictionary<string, decimal> _prices = new();
    private readonly object _lock = new();

    public MarketDataManager(
        ILogger<MarketDataManager> logger,
        IOptions<TradingServerConfig> config,
        IMarketDataProvider dataProvider)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _validSymbols = new HashSet<string>(config.Value.TradeableSymbols);
        var seedPrices = new Dictionary<string, decimal>
        {
            ["AAPL"] = 175.50m,
            ["GOOGL"] = 142.30m,
            ["MSFT"] = 378.90m,
            ["AMZN"] = 178.25m,
            ["TSLA"] = 248.75m,
            ["META"] = 485.60m,
            ["NVDA"] = 875.40m
        };

        foreach (var symbol in _validSymbols)
        {
            _prices[symbol] = seedPrices.TryGetValue(symbol, out var seed) ? seed : 100m;
        }

        _logger.LogInformation("Market Data Manager initialized with {Count} symbols", _validSymbols.Count);
    }

    public MarketDataManager(
        ILogger<MarketDataManager> logger,
        IOptions<TradingServerConfig> config)
        : this(logger, config, new SimulatedMarketDataProvider())
    {
    }

    public decimal GetPrice(string symbol)
    {
        if (!_validSymbols.Contains(symbol))
        {
            throw new InvalidOperationException($"No price data available for symbol: {symbol}");
        }

        lock (_lock)
        {
            if (_prices.TryGetValue(symbol, out var price))
            {
                return price;
            }
        }

        var providerPrice = _dataProvider.GetCurrentPrice(symbol);
        return providerPrice?.Price ?? throw new InvalidOperationException($"No price data available for symbol: {symbol}");
    }

    public bool IsValidSymbol(string symbol)
    {
        return _validSymbols.Contains(symbol);
    }

    public void UpdatePrice(string symbol, decimal price)
    {
        if (!_validSymbols.Contains(symbol))
        {
            throw new InvalidOperationException($"Cannot update price for invalid symbol: {symbol}");
        }

        lock (_lock)
        {
            _prices[symbol] = price;
        }

        _logger.LogInformation("Price update request: {Symbol} @ ${Price:N2}", symbol, price);
    }

    public Dictionary<string, decimal> GetAllPrices()
    {
        lock (_lock)
        {
            return _validSymbols.ToDictionary(symbol => symbol, symbol => _prices[symbol]);
        }
    }
}
