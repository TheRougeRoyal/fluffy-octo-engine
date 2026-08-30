using Xunit;
using FluentAssertions;
using TradingEngine.Services;
using TradingEngine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingEngine.Services.Tests;

public class MarketDataManagerTests
{
    private readonly Mock<ILogger<MarketDataManager>> _mockLogger;
    private readonly IOptions<TradingServerConfig> _config;

    public MarketDataManagerTests()
    {
        _mockLogger = new Mock<ILogger<MarketDataManager>>();
        _config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });
    }

    #region Initialization

    [Fact]
    public void Constructor_InitializesPrices_ForAllSymbols()
    {
        // Act
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Assert
        var prices = manager.GetAllPrices();
        prices.Should().HaveCount(7);
        prices.Keys.Should().Contain("AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA");
    }

    [Fact]
    public void Constructor_InitializesKnownPrices_WithCorrectValues()
    {
        // Act
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Assert
        manager.GetPrice("AAPL").Should().Be(175.50m);
        manager.GetPrice("GOOGL").Should().Be(142.30m);
        manager.GetPrice("MSFT").Should().Be(378.90m);
        manager.GetPrice("AMZN").Should().Be(178.25m);
        manager.GetPrice("TSLA").Should().Be(248.75m);
        manager.GetPrice("META").Should().Be(485.60m);
        manager.GetPrice("NVDA").Should().Be(875.40m);
    }

    #endregion

    #region GetPrice

    [Fact]
    public void GetPrice_ValidSymbol_ReturnsPrice()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act
        var price = manager.GetPrice("AAPL");

        // Assert
        price.Should().BeGreaterThan(0);
        price.Should().Be(175.50m);
    }

    [Fact]
    public void GetPrice_AllValidSymbols_ReturnsPositivePrices()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);
        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" };

        // Act & Assert
        foreach (var symbol in symbols)
        {
            var price = manager.GetPrice(symbol);
            price.Should().BeGreaterThan(0, $"{symbol} should have a positive price");
        }
    }

    [Fact]
    public void GetPrice_InvalidSymbol_ThrowsException()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetPrice("INVALID"));
        exception.Message.Should().Contain("No price data available");
        exception.Message.Should().Contain("INVALID");
    }

    [Fact]
    public void GetPrice_EmptySymbol_ThrowsException()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => manager.GetPrice(""));
    }

    #endregion

    #region IsValidSymbol

    [Fact]
    public void IsValidSymbol_KnownSymbol_ReturnsTrue()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.IsValidSymbol("AAPL").Should().BeTrue();
        manager.IsValidSymbol("GOOGL").Should().BeTrue();
        manager.IsValidSymbol("MSFT").Should().BeTrue();
    }

    [Fact]
    public void IsValidSymbol_UnknownSymbol_ReturnsFalse()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.IsValidSymbol("UNKNOWN").Should().BeFalse();
        manager.IsValidSymbol("FAKE").Should().BeFalse();
        manager.IsValidSymbol("XYZ").Should().BeFalse();
    }

    [Fact]
    public void IsValidSymbol_EmptyString_ReturnsFalse()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.IsValidSymbol("").Should().BeFalse();
    }

    [Fact]
    public void IsValidSymbol_CaseSensitive()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.IsValidSymbol("AAPL").Should().BeTrue();
        manager.IsValidSymbol("aapl").Should().BeFalse();
        manager.IsValidSymbol("Aapl").Should().BeFalse();
    }

    #endregion

    #region GetAllPrices

    [Fact]
    public void GetAllPrices_ReturnsAllConfiguredSymbols()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act
        var prices = manager.GetAllPrices();

        // Assert
        prices.Should().NotBeEmpty();
        prices.Keys.Should().Contain("AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA");
        prices.Should().HaveCount(7);
    }

    [Fact]
    public void GetAllPrices_ReturnsPositiveValues()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act
        var prices = manager.GetAllPrices();

        // Assert
        prices.Values.Should().AllSatisfy(price => price.Should().BeGreaterThan(0));
    }

    [Fact]
    public void GetAllPrices_ReturnsCopy_NotReference()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act
        var prices1 = manager.GetAllPrices();
        var prices2 = manager.GetAllPrices();

        // Assert
        prices1.Should().NotBeSameAs(prices2);
    }

    #endregion

    #region UpdatePrice

    [Fact]
    public void UpdatePrice_ValidSymbol_UpdatesSuccessfully()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);
        var originalPrice = manager.GetPrice("AAPL");

        // Act
        manager.UpdatePrice("AAPL", 200m);

        // Assert
        manager.GetPrice("AAPL").Should().Be(200m);
        manager.GetPrice("AAPL").Should().NotBe(originalPrice);
    }

    [Fact]
    public void UpdatePrice_InvalidSymbol_ThrowsException()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.UpdatePrice("INVALID", 100m));
        exception.Message.Should().Contain("Cannot update price for invalid symbol");
        exception.Message.Should().Contain("INVALID");
    }

    [Fact]
    public void UpdatePrice_MultipleUpdates_KeepsLatestPrice()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);

        // Act
        manager.UpdatePrice("AAPL", 180m);
        manager.UpdatePrice("AAPL", 190m);
        manager.UpdatePrice("AAPL", 200m);

        // Assert
        manager.GetPrice("AAPL").Should().Be(200m);
    }

    [Fact]
    public void UpdatePrice_DoesNotAffectOtherSymbols()
    {
        // Arrange
        var manager = new MarketDataManager(_mockLogger.Object, _config);
        var googlPrice = manager.GetPrice("GOOGL");

        // Act
        manager.UpdatePrice("AAPL", 200m);

        // Assert
        manager.GetPrice("GOOGL").Should().Be(googlPrice);
    }

    #endregion

    #region Custom Configuration

    [Fact]
    public void Constructor_WithCustomSymbols_InitializesCorrectly()
    {
        // Arrange
        var customConfig = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "CUSTOM1", "CUSTOM2" }
        });

        // Act
        var manager = new MarketDataManager(_mockLogger.Object, customConfig);

        // Assert
        manager.IsValidSymbol("CUSTOM1").Should().BeTrue();
        manager.IsValidSymbol("CUSTOM2").Should().BeTrue();
        manager.IsValidSymbol("AAPL").Should().BeFalse();
        manager.GetPrice("CUSTOM1").Should().BeGreaterThan(0);
        manager.GetPrice("CUSTOM2").Should().BeGreaterThan(0);
    }

    #endregion
}
