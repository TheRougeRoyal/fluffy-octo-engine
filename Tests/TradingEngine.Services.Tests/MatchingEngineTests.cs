using Xunit;
using FluentAssertions;
using Moq;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;

namespace TradingEngine.Services.Tests;

public class MatchingEngineTests
{
    private readonly Mock<IPortfolioManager> _mockPortfolio;
    private readonly MatchingEngine _engine;

    public MatchingEngineTests()
    {
        _mockPortfolio = new Mock<IPortfolioManager>();
        _engine = new MatchingEngine(_mockPortfolio.Object);
    }

    #region Buy Order Matching

    [Fact]
    public void TryMatch_BuyOrderAboveMarket_ReturnsMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 180, Side = OrderSide.Buy };
        _mockPortfolio.Setup(p => p.HasSufficientCash(1750)).Returns(true);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_BuyOrderAtMarket_ReturnsMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 175, Side = OrderSide.Buy };
        _mockPortfolio.Setup(p => p.HasSufficientCash(1750)).Returns(true);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_BuyOrderBelowMarket_ReturnsNotMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 170, Side = OrderSide.Buy };

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeFalse();
        result.Reason.Should().Contain("below market price");
    }

    [Fact]
    public void TryMatch_BuyWithInsufficientCash_ReturnsNotMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 180, Side = OrderSide.Buy };
        _mockPortfolio.Setup(p => p.HasSufficientCash(1750)).Returns(false);
        _mockPortfolio.Setup(p => p.GetBuyingPower()).Returns(1000);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeFalse();
        result.Reason.Should().Contain("Insufficient cash");
    }

    #endregion

    #region Sell Order Matching

    [Fact]
    public void TryMatch_SellOrderBelowMarket_ReturnsMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 170, Side = OrderSide.Sell };
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", 10)).Returns(true);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_SellOrderAtMarket_ReturnsMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 175, Side = OrderSide.Sell };
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", 10)).Returns(true);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeTrue();
    }

    [Fact]
    public void TryMatch_SellOrderAboveMarket_ReturnsNotMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 180, Side = OrderSide.Sell };

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeFalse();
        result.Reason.Should().Contain("above market price");
    }

    [Fact]
    public void TryMatch_SellWithInsufficientShares_ReturnsNotMatched()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 100, Price = 170, Side = OrderSide.Sell };
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", 100)).Returns(false);
        
        var positions = new Dictionary<string, Position>
        {
            { "AAPL", new Position { Symbol = "AAPL", Quantity = 50, AverageCost = 160 } }
        };
        _mockPortfolio.Setup(p => p.Positions).Returns(positions);

        // Act
        var result = _engine.TryMatch(order, 175);

        // Assert
        result.IsMatched.Should().BeFalse();
        result.Reason.Should().Contain("Insufficient shares");
        result.Reason.Should().Contain("50");
    }

    #endregion
}
