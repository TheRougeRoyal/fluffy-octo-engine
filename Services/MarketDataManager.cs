using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class MarketDataManager : IMarketDataManager
{
    private readonly ILogger<MarketDataManager> _logger;
    private readonly Dictionary<string, decimal> _prices;
    private readonly HashSet<string> _validSymbols;
    private readonly object _lock = new();
    private readonly Random _random = new();

    public MarketDataManager(
        ILogger<MarketDataManager> logger,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _prices = new Dictionary<string, decimal>();
        _validSymbols = new HashSet<string>(config.Value.TradeableSymbols);

        // Initialize with random prices for demo purposes
        InitializePrices();
        
        _logger.LogInformation("Market Data Manager initialized with {Count} symbols", _validSymbols.Count);
    }

    private void InitializePrices()
    {
        // Set realistic initial prices for each symbol
        var initialPrices = new Dictionary<string, decimal>
        {
            { "AAPL", 175.50m },
            { "GOOGL", 142.30m },
            { "MSFT", 378.90m },
            { "AMZN", 178.25m },
            { "TSLA", 248.75m },
            { "META", 485.60m },
            { "NVDA", 875.40m }
        };

        lock (_lock)
        {
            foreach (var symbol in _validSymbols)
            {
                if (initialPrices.ContainsKey(symbol))
                {
                    _prices[symbol] = initialPrices[symbol];
                }
                else
                {
                    // Random price between $50 and $500 for unlisted symbols
                    _prices[symbol] = (decimal)(_random.NextDouble() * 450 + 50);
                }
                
                _logger.LogInformation("Initialized {Symbol} @ ${Price:N2}", symbol, _prices[symbol]);
            }
        }
    }

    public decimal GetPrice(string symbol)
    {
        lock (_lock)
        {
            if (_prices.ContainsKey(symbol))
            {
                return _prices[symbol];
            }
            
            throw new InvalidOperationException($"No price data available for symbol: {symbol}");
        }
    }

    public bool IsValidSymbol(string symbol)
    {
        lock (_lock)
        {
            return _validSymbols.Contains(symbol);
        }
    }

    public void UpdatePrice(string symbol, decimal price)
    {
        lock (_lock)
        {
            if (_validSymbols.Contains(symbol))
            {
                _prices[symbol] = price;
                _logger.LogInformation("Price updated: {Symbol} @ ${Price:N2}", symbol, price);
            }
            else
            {
                throw new InvalidOperationException($"Cannot update price for invalid symbol: {symbol}");
            }
        }
    }

    public Dictionary<string, decimal> GetAllPrices()
    {
        lock (_lock)
        {
            return new Dictionary<string, decimal>(_prices);
        }
    }
}
