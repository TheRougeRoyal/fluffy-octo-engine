using Xunit;
using FluentAssertions;
using Moq;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using Microsoft.Extensions.Logging;

namespace TradingEngine.Services.Tests;

public class TradeExecutorTests
{
    private readonly Mock<IPortfolioManager> _mockPortfolio;
    private readonly Mock<ILogger<TradeExecutor>> _mockLogger;
    private readonly TradeExecutor _executor;

    public TradeExecutorTests()
    {
        _mockPortfolio = new Mock<IPortfolioManager>();
        _mockLogger = new Mock<ILogger<TradeExecutor>>();
        _executor = new TradeExecutor(_mockLogger.Object, _mockPortfolio.Object);
    }

    [Fact]
    public void ExecuteTrade_BuyOrder_UpdatesPortfolioWithBuy()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 150, Side = OrderSide.Buy };

        // Act
        _executor.ExecuteTrade(order, 175);

        // Assert
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 10, 175), Times.Once);
    }

    [Fact]
    public void ExecuteTrade_SellOrder_UpdatesPortfolioWithSell()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 5, Price = 170, Side = OrderSide.Sell };

        // Act
        _executor.ExecuteTrade(order, 175);

        // Assert
        _mockPortfolio.Verify(p => p.UpdateOnSell("AAPL", 5, 175), Times.Once);
    }

    [Fact]
    public void ExecuteTrade_MultipleTrades_RecordsAllInHistory()
    {
        // Arrange
        var order1 = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 150, Side = OrderSide.Buy };
        var order2 = new OrderRequest { Symbol = "GOOGL", Quantity = 5, Price = 200, Side = OrderSide.Buy };

        // Act
        _executor.ExecuteTrade(order1, 175);
        _executor.ExecuteTrade(order2, 142);

        // Assert
        var history = _executor.GetTradeHistory();
        history.Should().HaveCount(2);
        history.Should().AllSatisfy(t => t.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void GetTradeHistory_ReturnsImmutableCopy()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 150, Side = OrderSide.Buy };
        _executor.ExecuteTrade(order, 175);

        // Act
        var history1 = _executor.GetTradeHistory();
        var history2 = _executor.GetTradeHistory();

        // Assert
        history1.Should().NotBeSameAs(history2);
        history1.Should().HaveCount(1);
        history2.Should().HaveCount(1);
    }

    [Fact]
    public void ExecuteTrade_ConcurrentExecutions_AllRecorded()
    {
        // Arrange
        var orders = Enumerable.Range(0, 10)
            .Select(i => new OrderRequest 
            { 
                Symbol = "AAPL", 
                Quantity = 1, 
                Price = 150, 
                Side = OrderSide.Buy 
            })
            .ToList();

        // Act
        Parallel.ForEach(orders, order => _executor.ExecuteTrade(order, 175));

        // Assert
        var history = _executor.GetTradeHistory();
        history.Should().HaveCount(10);
    }
}
