using Xunit;
using Moq;
using FluentAssertions;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using TradingEngine.Services.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using TradingEngine.Services.Quant;
using TradingEngine.Models.Quant;

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

        var validator = new OrderValidator(_mockMarketData.Object);
        var matchingEngine = new MatchingEngine(_mockPortfolio.Object, new Mock<ILimitOrderBook>().Object);
        var tradeExecutor = new TradeExecutor(new Mock<ILogger<TradeExecutor>>().Object, _mockPortfolio.Object);
        var persistence = new Mock<IPersistenceService>().Object;
        var pdeModel = new Mock<IPdeModel>();
        pdeModel.Setup(p => p.GetFairValueAsync(It.IsAny<PdeRequest>()))
            .ReturnsAsync(new PdeResponse(true, 100, 100, 0, new Greeks(0,0,0,0,0), string.Empty));
        var orderBook = new Mock<ILimitOrderBook>();
        var risk = new Mock<IRiskManagementService>();
        risk.Setup(r => r.ValidateOrder(It.IsAny<OrderRequest>(), It.IsAny<decimal>()))
            .Returns((true, string.Empty));

        _handler = new OrderHandler(
            _mockLogger.Object,
            validator,
            matchingEngine,
            tradeExecutor,
            _mockMarketData.Object,
            persistence,
            pdeModel.Object,
            _mockPortfolio.Object,
            orderBook.Object,
            risk.Object);
    }

    #region Valid Order Scenarios

    [Fact]
    public async Task ProcessOrder_BuyOrderAboveMarketPrice_ExecutesSuccessfully()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedPrice.Should().Be(175);
        response.ExecutedQuantity.Should().Be(10);
        response.OrderId.Should().NotBeNullOrEmpty();
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 10, 175), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SellOrderBelowMarketPrice_ExecutesSuccessfully()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedPrice.Should().Be(175);
        response.ExecutedQuantity.Should().Be(5);
        _mockPortfolio.Verify(p => p.UpdateOnSell("AAPL", 5, 175), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_BuyWithExactMarketPrice_ExecutesAtMarketPrice()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.ExecutedPrice.Should().Be(150);
        response.Status.Should().Be(OrderStatus.Executed);
    }

    [Fact]
    public async Task ProcessOrder_SellWithExactMarketPrice_ExecutesAtMarketPrice()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.ExecutedPrice.Should().Be(378.90m);
        response.Status.Should().Be(OrderStatus.Executed);
    }

    [Fact]
    public async Task ProcessOrder_LargeBuyOrder_ExecutesCorrectly()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedQuantity.Should().Be(1000);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("TSLA", 1000, 248.75m), Times.Once);
    }

    #endregion

    #region Validation Failures

    [Fact]
    public async Task ProcessOrder_InvalidSymbol_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("INVALID")
            .WithQuantity(10)
            .WithPrice(100)
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("INVALID")).Returns(false);

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Invalid symbol");
        response.ExecutedQuantity.Should().Be(0);
        response.ExecutedPrice.Should().Be(0);
    }

    [Fact]
    public async Task ProcessOrder_ZeroQuantity_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(0)
            .WithPrice(100)
            .Build();

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrder_NegativeQuantity_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(-10)
            .WithPrice(100)
            .Build();

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrder_NegativePrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(-50)
            .Build();

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Price must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrder_ZeroPrice_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(10)
            .WithPrice(0)
            .Build();

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Price must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrder_EmptySymbol_ReturnsRejected()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("")
            .WithQuantity(10)
            .WithPrice(100)
            .Build();

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Symbol cannot be empty");
    }

    [Fact]
    public async Task ProcessOrder_NullSymbol_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Symbol cannot be empty");
    }

    #endregion

    #region Matching Failures

    [Fact]
    public async Task ProcessOrder_BuyBelowMarketPrice_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("below market price");
    }

    [Fact]
    public async Task ProcessOrder_SellAboveMarketPrice_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("above market price");
    }

    [Fact]
    public async Task ProcessOrder_InsufficientCash_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient cash");
        response.Message.Should().Contain("1000");
    }

    [Fact]
    public async Task ProcessOrder_InsufficientShares_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient shares");
        response.Message.Should().Contain("50");
    }

    [Fact]
    public async Task ProcessOrder_SellWithoutAnyPosition_ReturnsRejected()
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
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient shares");
        response.Message.Should().Contain("0");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ProcessOrder_MultipleOrdersSameSymbol_UpdatesPortfolioCorrectly()
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
        var response1 = await _handler.ProcessOrderAsync(order1);
        var response2 = await _handler.ProcessOrderAsync(order2);

        // Assert
        response1.Status.Should().Be(OrderStatus.Executed);
        response2.Status.Should().Be(OrderStatus.Executed);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 10, 175), Times.Once);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 5, 175), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_BuyThenSellSameSymbol_CalculatesCorrectly()
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
        var buyResponse = await _handler.ProcessOrderAsync(buyOrder);
        var sellResponse = await _handler.ProcessOrderAsync(sellOrder);

        // Assert
        buyResponse.Status.Should().Be(OrderStatus.Executed);
        sellResponse.Status.Should().Be(OrderStatus.Executed);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("MSFT", 20, 378.90m), Times.Once);
        _mockPortfolio.Verify(p => p.UpdateOnSell("MSFT", 10, 378.90m), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_DifferentSymbols_AllProcessedIndependently()
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
        var response1 = await _handler.ProcessOrderAsync(orderAAPL);
        var response2 = await _handler.ProcessOrderAsync(orderGOOGL);
        var response3 = await _handler.ProcessOrderAsync(orderMSFT);

        // Assert
        response1.Status.Should().Be(OrderStatus.Executed);
        response2.Status.Should().Be(OrderStatus.Executed);
        response3.Status.Should().Be(OrderStatus.Executed);

        _mockPortfolio.Verify(p => p.UpdateOnBuy(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessOrder_MarketDataException_ReturnsRejectedWithError()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Throws(new Exception("Market data unavailable"));

        // Act
        var response = await _handler.ProcessOrderAsync(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Failed to get market price");
    }

    [Fact]
    public async Task ProcessOrder_UniqueOrderIds_GeneratedForEachOrder()
    {
        // Arrange
        var order1 = new OrderBuilder().WithSymbol("AAPL").WithPrice(180).Build();
        var order2 = new OrderBuilder().WithSymbol("GOOGL").WithPrice(150).Build();

        _mockMarketData.Setup(m => m.IsValidSymbol(It.IsAny<string>())).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice(It.IsAny<string>())).Returns(100);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var response1 = await _handler.ProcessOrderAsync(order1);
        var response2 = await _handler.ProcessOrderAsync(order2);

        // Assert
        response1.OrderId.Should().NotBeNullOrEmpty();
        response2.OrderId.Should().NotBeNullOrEmpty();
        response1.OrderId.Should().NotBe(response2.OrderId);
    }

    #endregion

    #region Concurrency

    [Fact]
    public async Task ProcessOrder_ConcurrentOrders_AllProcessedSequentially()
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
        var responses = await Task.WhenAll(orders.Select(o => _handler.ProcessOrderAsync(o)));

        // Assert
        responses.Should().AllSatisfy(r => r.Status.Should().Be(OrderStatus.Executed));
        responses.Should().HaveCount(10);
        _mockPortfolio.Verify(p => p.UpdateOnBuy("AAPL", 1, 175), Times.Exactly(10));
    }

    [Fact]
    public async Task ProcessOrder_MixedBuySellOrders_ProcessedCorrectly()
    {
        // Arrange
        var buyOrder = new OrderBuilder().WithSymbol("AAPL").WithPrice(180).AsBuy().Build();
        var sellOrder = new OrderBuilder().WithSymbol("AAPL").WithPrice(170).AsSell().Build();

        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(175);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);
        _mockPortfolio.Setup(p => p.HasSufficientShares("AAPL", It.IsAny<int>())).Returns(true);

        // Act
        var buyResponse = await _handler.ProcessOrderAsync(buyOrder);
        var sellResponse = await _handler.ProcessOrderAsync(sellOrder);

        // Assert
        buyResponse.Status.Should().Be(OrderStatus.Executed);
        sellResponse.Status.Should().Be(OrderStatus.Executed);
    }

    #endregion
}
