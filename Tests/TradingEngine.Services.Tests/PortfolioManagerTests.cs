using Xunit;
using FluentAssertions;
using TradingEngine.Models;
using TradingEngine.Services;
using TradingEngine.Services.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingEngine.Services.Tests;

public class PortfolioManagerTests
{
    private readonly Mock<ILogger<PortfolioManager>> _mockLogger;
    private readonly IOptions<TradingServerConfig> _config;

    public PortfolioManagerTests()
    {
        _mockLogger = new Mock<ILogger<PortfolioManager>>();
        _config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT" }
        });
    }

    #region Initialization

    [Fact]
    public void Constructor_InitializesCashBalance_Correctly()
    {
        // Act
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Assert
        manager.CashBalance.Should().Be(100000m);
        manager.GetBuyingPower().Should().Be(100000m);
    }

    [Fact]
    public void Constructor_InitializesEmptyPositions()
    {
        // Act
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Assert
        manager.Positions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCustomInitialCash_SetsCorrectly()
    {
        // Arrange
        var customConfig = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 50000m,
            Port = 5000,
            TradeableSymbols = new List<string>()
        });

        // Act
        var manager = new PortfolioManager(_mockLogger.Object, customConfig);

        // Assert
        manager.CashBalance.Should().Be(50000m);
    }

    #endregion

    #region Cash Management

    [Fact]
    public void UpdateOnBuy_DeductsCorrectAmount_FromCashBalance()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        var initialCash = manager.CashBalance;

        // Act
        manager.UpdateOnBuy("AAPL", 10, 100);

        // Assert
        var finalCash = manager.CashBalance;
        (initialCash - finalCash).Should().Be(1000);
        finalCash.Should().Be(99000m);
    }

    [Fact]
    public void UpdateOnBuy_LargeOrder_DeductsCorrectAmount()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act
        manager.UpdateOnBuy("AAPL", 100, 500);

        // Assert
        manager.CashBalance.Should().Be(50000m);
    }

    [Fact]
    public void UpdateOnSell_AddsCorrectAmount_ToCashBalance()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 100);
        var cashAfterBuy = manager.CashBalance;

        // Act
        manager.UpdateOnSell("AAPL", 5, 110);

        // Assert
        var cashAfterSell = manager.CashBalance;
        (cashAfterSell - cashAfterBuy).Should().Be(550);
    }

    [Fact]
    public void UpdateOnSell_FullPosition_AddsAllProceeds()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 20, 100);

        // Act
        manager.UpdateOnSell("AAPL", 20, 120);

        // Assert
        manager.CashBalance.Should().Be(100400m); // 100000 - 2000 + 2400
    }

    #endregion

    #region Position Tracking

    [Fact]
    public void UpdateOnBuy_FirstBuy_CreatesNewPosition()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act
        manager.UpdateOnBuy("AAPL", 10, 150);

        // Assert
        manager.Positions.Should().ContainKey("AAPL");
        manager.Positions["AAPL"].Quantity.Should().Be(10);
        manager.Positions["AAPL"].AverageCost.Should().Be(150);
        manager.Positions["AAPL"].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void UpdateOnBuy_SecondBuyHigherPrice_UpdatesAverageCost()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 150);

        // Act
        manager.UpdateOnBuy("AAPL", 10, 160);

        // Assert
        manager.Positions["AAPL"].Quantity.Should().Be(20);
        manager.Positions["AAPL"].AverageCost.Should().Be(155);
    }

    [Fact]
    public void UpdateOnBuy_SecondBuyLowerPrice_UpdatesAverageCost()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 160);

        // Act
        manager.UpdateOnBuy("AAPL", 10, 140);

        // Assert
        manager.Positions["AAPL"].Quantity.Should().Be(20);
        manager.Positions["AAPL"].AverageCost.Should().Be(150);
    }

    [Fact]
    public void UpdateOnBuy_UnequalQuantities_CalculatesCorrectAverage()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 100); // 1000 total

        // Act
        manager.UpdateOnBuy("AAPL", 20, 110); // 2200 total

        // Assert
        // Total: 30 shares, Total cost: 3200, Avg: 106.67
        manager.Positions["AAPL"].Quantity.Should().Be(30);
        manager.Positions["AAPL"].AverageCost.Should().BeApproximately(106.67m, 0.01m);
    }

    [Fact]
    public void UpdateOnSell_PartialSell_ReducesQuantityKeepsAverage()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 20, 150);

        // Act
        manager.UpdateOnSell("AAPL", 10, 160);

        // Assert
        manager.Positions["AAPL"].Quantity.Should().Be(10);
        manager.Positions["AAPL"].AverageCost.Should().Be(150);
    }

    [Fact]
    public void UpdateOnSell_FullSell_RemovesPosition()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 150);

        // Act
        manager.UpdateOnSell("AAPL", 10, 160);

        // Assert
        manager.Positions.Should().NotContainKey("AAPL");
        manager.Positions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateOnBuy_MultipleSymbols_TracksIndependently()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act
        manager.UpdateOnBuy("AAPL", 10, 150);
        manager.UpdateOnBuy("GOOGL", 5, 200);
        manager.UpdateOnBuy("MSFT", 20, 300);

        // Assert
        manager.Positions.Should().HaveCount(3);
        manager.Positions["AAPL"].Quantity.Should().Be(10);
        manager.Positions["GOOGL"].Quantity.Should().Be(5);
        manager.Positions["MSFT"].Quantity.Should().Be(20);
    }

    [Fact]
    public void TotalCost_CalculatedCorrectly()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 10, 150);

        // Act
        var position = manager.Positions["AAPL"];

        // Assert
        position.TotalCost.Should().Be(1500);
    }

    #endregion

    #region Buying Power

    [Fact]
    public void HasSufficientCash_WithExactAmount_ReturnsTrue()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 100, 100);
        var remainingCash = manager.GetBuyingPower();

        // Act & Assert
        manager.HasSufficientCash(remainingCash).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientCash_WithLessThanAvailable_ReturnsTrue()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.HasSufficientCash(50000m).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientCash_WithMoreThanAvailable_ReturnsFalse()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        var requiredCash = manager.GetBuyingPower() + 1000;

        // Act & Assert
        manager.HasSufficientCash(requiredCash).Should().BeFalse();
    }

    [Fact]
    public void HasSufficientCash_AfterMultipleBuys_CalculatesCorrectly()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 100, 100); // -10000
        manager.UpdateOnBuy("GOOGL", 50, 200); // -10000

        // Act & Assert
        manager.HasSufficientCash(80000m).Should().BeTrue();
        manager.HasSufficientCash(80001m).Should().BeFalse();
    }

    [Fact]
    public void GetBuyingPower_ReturnsCurrentCashBalance()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act
        var buyingPower = manager.GetBuyingPower();

        // Assert
        buyingPower.Should().Be(manager.CashBalance);
    }

    #endregion

    #region Share Validation

    [Fact]
    public void HasSufficientShares_WithExactQuantity_ReturnsTrue()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 50, 100);

        // Act & Assert
        manager.HasSufficientShares("AAPL", 50).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientShares_WithLessThanOwned_ReturnsTrue()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 50, 100);

        // Act & Assert
        manager.HasSufficientShares("AAPL", 25).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientShares_WithMoreThanOwned_ReturnsFalse()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 50, 100);

        // Act & Assert
        manager.HasSufficientShares("AAPL", 51).Should().BeFalse();
    }

    [Fact]
    public void HasSufficientShares_NonExistentSymbol_ReturnsFalse()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);

        // Act & Assert
        manager.HasSufficientShares("UNKNOWN", 1).Should().BeFalse();
    }

    [Fact]
    public void HasSufficientShares_AfterPartialSell_ReturnsCorrectResult()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        manager.UpdateOnBuy("AAPL", 100, 100);
        manager.UpdateOnSell("AAPL", 60, 110);

        // Act & Assert
        manager.HasSufficientShares("AAPL", 40).Should().BeTrue();
        manager.HasSufficientShares("AAPL", 41).Should().BeFalse();
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void ComplexScenario_MultipleBuysAndSells_CalculatesCorrectly()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        var initialCash = manager.CashBalance;

        // Act
        manager.UpdateOnBuy("AAPL", 10, 100);  // -1000, cash = 99000
        manager.UpdateOnBuy("AAPL", 10, 120);  // -1200, cash = 97800
        manager.UpdateOnSell("AAPL", 5, 130);  // +650, cash = 98450
        manager.UpdateOnBuy("GOOGL", 20, 200); // -4000, cash = 94450

        // Assert
        manager.CashBalance.Should().Be(94450m);
        manager.Positions["AAPL"].Quantity.Should().Be(15);
        manager.Positions["AAPL"].AverageCost.Should().Be(110); // (1000 + 1200) / 20 = 110
        manager.Positions["GOOGL"].Quantity.Should().Be(20);
    }

    [Fact]
    public void RealizedProfitScenario_BuyLowSellHigh()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        var initialCash = manager.CashBalance;

        // Act
        manager.UpdateOnBuy("AAPL", 100, 100);  // Cost: 10000, Cash: 90000
        manager.UpdateOnSell("AAPL", 100, 150); // Proceeds: 15000, Cash: 105000

        // Assert
        var profit = manager.CashBalance - initialCash;
        profit.Should().Be(5000m);
        manager.Positions.Should().NotContainKey("AAPL");
    }

    [Fact]
    public void RealizedLossScenario_BuyHighSellLow()
    {
        // Arrange
        var manager = new PortfolioManager(_mockLogger.Object, _config);
        var initialCash = manager.CashBalance;

        // Act
        manager.UpdateOnBuy("AAPL", 100, 150);  // Cost: 15000, Cash: 85000
        manager.UpdateOnSell("AAPL", 100, 100); // Proceeds: 10000, Cash: 95000

        // Assert
        var loss = initialCash - manager.CashBalance;
        loss.Should().Be(5000m);
    }

    #endregion
}
