using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradingEngine.Models;
using TradingEngine.Services;

namespace TradingEngine.Services.Tests.Fixtures;

public class PortfolioBuilder
{
    private decimal _initialCash = 100000m;

    public PortfolioBuilder WithInitialCash(decimal cash)
    {
        _initialCash = cash;
        return this;
    }

    public IPortfolioManager Build()
    {
        var mockLogger = new Mock<ILogger<PortfolioManager>>();
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = _initialCash,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        return new PortfolioManager(mockLogger.Object, config);
    }
}
