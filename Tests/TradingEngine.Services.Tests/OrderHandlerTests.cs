using Xunit;
using Moq;
using FluentAssertions;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using TradingEngine.Services.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace TradingEngine.Services.Tests;

public class OrderHandlerTests
{
    private readonly Mock<IPortfolioManager> _mockPortfolio;
    private readonly Mock<IMarketDataManager> _mockMarketData;
    private readonly Mock<ILogger<OrderHandler>> _mockLogger;
    private readonly OrderHandler _handler;

    public OrderHandlerTests()
    {
        _mockPortfolio = new Mock<IPortfolioManager>();
        _mockMarketData = new Mock<IMarketDataManager>();
        _mockLogger = new Mock<ILogger<OrderHandler>>();

        _handler = new OrderHandler(_mockLogger.Object, _mockPortfolio.Object, _mockMarketData.Object);
    }

    #region Valid Order Scenarios

    [Fact]
    public void ProcessOrder_BuyOrderAboveMarketPrice_ExecutesSuccessfully()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(180)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedPrice.Should().Be(175);
        response.ExecutedQuantity.Should().Be(10);
        response.OrderId.Should().NotBeNullOrEmpty();
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 10, 175), Times.Once);
    }

    [Fact]
    public void ProcessOrder_SellOrderBelowMarketPrice_ExecutesSuccessfully()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(5)
            .WithPrice(170)
            .AsSell()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", 5)).Returns(true);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedPrice.Should().Be(175);
        response.ExecutedQuantity.Should().Be(5);
        _mockPortfolio.Verify(p => p.UpdateOnSell("AAPL", 5, 175), Times.Once);
    }

    [Fact]
    public void ProcessOrder_BuyWithExactMarketPrice_ExecutesAtMarketPrice()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("GOOGL")
            .WithQuantity(20)
            .WithPrice(150)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("GOOGL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("GOOGL")).Returns(150);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.ExecutedPrice.Should().Be(150);
        response.Status.Should().Be(OrderStatus.Executed);
    }

    [Fact]
    public void ProcessOrder_SellWithExactMarketPrice_ExecutesAtMarketPrice()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("MSFT")
            .WithQuantity(15)
            .WithPrice(378.90m)
            .AsSell()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("MSFT")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("MSFT")).Returns(378.90m);
        _mockPortfolio.Setup(p => p.HasSufficientShares("MSFT", 15)).Returns(true);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.ExecutedPrice.Should().Be(378.90m);
        response.Status.Should().Be(OrderStatus.Executed);
    }

    [Fact]
    public void ProcessOrder_LargeBuyOrder_ExecutesCorrectly()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("TSLA")
            .WithQuantity(1000)
            .WithPrice(300)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("TSLA")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("TSLA")).Returns(248.75m);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedQuantity.Should().Be(1000);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("TSLA", 1000, 248.75m), Times.Once);
    }

    #endregion

    #region Validation Failures

    [Fact]
    public void ProcessOrder_InvalidSymbol_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("INVALID")
            .WithQuantity(10)
            .WithPrice(100)
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("INVALID")).Returns(false);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Invalid symbol");
        response.ExecutedQuantity.Should().Be(0);
        response.ExecutedPrice.Should().Be(0);
    }

    [Fact]
    public void ProcessOrder_ZeroQuantity_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(0)
            .WithPrice(100)
            .Build();

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public void ProcessOrder_NegativeQuantity_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(-10)
            .WithPrice(100)
            .Build();

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public void ProcessOrder_NegativePrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(-50)
            .Build();

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Price must be greater than 0");
    }

    [Fact]
    public void ProcessOrder_ZeroPrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(0)
            .Build();

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Price must be greater than 0");
    }

    [Fact]
    public void ProcessOrder_EmptySymbol_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("")
            .WithQuantity(10)
            .WithPrice(100)
            .Build();

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Symbol cannot be empty");
    }

    [Fact]
    public void ProcessOrder_NullSymbol_ReturnsRejected()
    {
        // Arrange
        var order = new OrderRequest
        {
            Symbol = null!,
            Quantity = 10,
            Price = 100,
            Side = OrderSide.Buy
        };

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Symbol cannot be empty");
    }

    #endregion

    #region Matching Failures

    [Fact]
    public void ProcessOrder_BuyBelowMarketPrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(170)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("below market price");
    }

    [Fact]
    public void ProcessOrder_SellAboveMarketPrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(180)
            .AsSell()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("above market price");
    }

    [Fact]
    public void ProcessOrder_InsufficientCash_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(180)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(1750)).Returns(false);
        _mockPortfolio.Setup(p => p.GetBuyingPower()).Returns(1000);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient cash");
        response.Message.Should().Contain("1000");
    }

    [Fact]
    public void ProcessOrder_InsufficientShares_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(100)
            .WithPrice(170)
            .AsSell()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", 100)).Returns(false);
        
        var positions = new Dictionary<string, Position>
        {
            { "AAPL", new Position { Symbol = "AAPL", Quantity = 50, AverageCost = 170 } }
        };
        _mockPortfolio.Setup(p => p.Positions).Returns(positions);

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient shares");
        response.Message.Should().Contain("50");
    }

    [Fact]
    public void ProcessOrder_SellWithoutAnyPosition_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("NVDA")
            .WithQuantity(10)
            .WithPrice(800)
            .AsSell()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("NVDA")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("NVDA")).Returns(875.40m);
        _mockPortfolio.Setup(p => p.HasSufficientShares("NVDA", 10)).Returns(false);
        _mockPortfolio.Setup(p => p.Positions).Returns(new Dictionary<string, Position>());

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient shares");
        response.Message.Should().Contain("0");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ProcessOrder_MultipleOrdersSameSymbol_UpdatesPortfolioCorrectly()
    {
        // Arrange
        var order1 = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(180)
            .Build();

        var order2 = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(5)
            .WithPrice(175)
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response1 = _handler.ProcessOrder(order1);
        var response2 = _handler.ProcessOrder(order2);

        // Assert
        response1.Status.Should().Be(OrderStatus.Executed);
        response2.Status.Should().Be(OrderStatus.Executed);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 10, 175), Times.Once);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 5, 175), Times.Once);
    }

    [Fact]
    public void ProcessOrder_BuyThenSellSameSymbol_CalculatesCorrectly()
    {
        // Arrange - Buy first
        var buyOrder = new OrderBuilder()
            .WithSymbol("MSFT")
            .WithQuantity(20)
            .WithPrice(400)
            .AsBuy()
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("MSFT")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("MSFT")).Returns(378.90m);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        var sellOrder = new OrderBuilder()
            .WithSymbol("MSFT")
            .WithQuantity(10)
            .WithPrice(370)
            .AsSell()
            .Build();

        _mockPortfolio.Setup(p => p.HasSufficientShares("MSFT", 10)).Returns(true);

        // Act
        var buyResponse = _handler.ProcessOrder(buyOrder);
        var sellResponse = _handler.ProcessOrder(sellOrder);

        // Assert
        buyResponse.Status.Should().Be(OrderStatus.Executed);
        sellResponse.Status.Should().Be(OrderStatus.Executed);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("MSFT", 20, 378.90m), Times.Once);
        _mockPortfolio.Verify(p => p.UpdateOnSell("MSFT", 10, 378.90m), Times.Once);
    }

    [Fact]
    public void ProcessOrder_DifferentSymbols_AllProcessedIndependently()
    {
        // Arrange
        var orderAAPL = new OrderBuilder().WithSymbol("AAPL").WithPrice(180).Build();
        var orderGOOGL = new OrderBuilder().WithSymbol("GOOGL").WithPrice(150).Build();
        var orderMSFT = new OrderBuilder().WithSymbol("MSFT").WithPrice(400).Build();

        _mockMarketData.Setup(m => m.IsValidSymbol(It.IsAny<string>())).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175.50m);
        _mockMarketData.Setup(m => m.GetPrice("GOOGL")).Returns(142.30m);
        _mockMarketData.Setup(m => m.GetPrice("MSFT")).Returns(378.90m);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response1 = _handler.ProcessOrder(orderAAPL);
        var response2 = _handler.ProcessOrder(orderGOOGL);
        var response3 = _handler.ProcessOrder(orderMSFT);

        // Assert
        response1.Status.Should().Be(OrderStatus.Executed);
        response2.Status.Should().Be(OrderStatus.Executed);
        response3.Status.Should().Be(OrderStatus.Executed);
        
        _mockPortfolio.Verify(p => p.UpdateOnBuy(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Exactly(3));
    }

    [Fact]
    public void ProcessOrder_MarketDataException_ReturnsRejectedWithError()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Throws(new Exception("Market data unavailable"));

        // Act
        var response = _handler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Failed to get market price");
    }

    [Fact]
    public void ProcessOrder_UniqueOrderIds_GeneratedForEachOrder()
    {
        // Arrange
        var order1 = new OrderBuilder().WithSymbol("AAPL").WithPrice(180).Build();
        var order2 = new OrderBuilder().WithSymbol("GOOGL").WithPrice(150).Build();

        _mockMarketData.Setup(m => m.IsValidSymbol(It.IsAny<string>())).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice(It.IsAny<string>())).Returns(100);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response1 = _handler.ProcessOrder(order1);
        var response2 = _handler.ProcessOrder(order2);

        // Assert
        response1.OrderId.Should().NotBeNullOrEmpty();
        response2.OrderId.Should().NotBeNullOrEmpty();
        response1.OrderId.Should().NotBe(response2.OrderId);
    }

    #endregion

    #region Concurrency

    [Fact]
    public void ProcessOrder_ConcurrentOrders_AllProcessedSequentially()
    {
        // Arrange
        var orders = Enumerable.Range(0, 10)
            .Select(i => new OrderBuilder()
                .WithSymbol("AAPL")
                .WithQuantity(1)
                .WithPrice(180)
                .Build())
            .ToList();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var responses = orders.Select(o => _handler.ProcessOrder(o)).ToList();

        // Assert
        responses.Should().AllSatisfy(r => r.Status.Should().Be(OrderStatus.Executed));
        responses.Should().HaveCount(10);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 1, 175), Times.Exactly(10));
    }

    [Fact]
    public void ProcessOrder_MixedBuySellOrders_ProcessedCorrectly()
    {
        // Arrange
        var buyOrder = new OrderBuilder().WithSymbol("AAPL").WithPrice(180).AsBuy().Build();
        var sellOrder = new OrderBuilder().WithSymbol("AAPL").WithPrice(170).AsSell().Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", It.IsAny<int>())).Returns(true);

        // Act
        var buyResponse = _handler.ProcessOrder(buyOrder);
        var sellResponse = _handler.ProcessOrder(sellOrder);

        // Assert
        buyResponse.Status.Should().Be(OrderStatus.Executed);
        sellResponse.Status.Should().Be(OrderStatus.Executed);
    }

    #endregion
}
