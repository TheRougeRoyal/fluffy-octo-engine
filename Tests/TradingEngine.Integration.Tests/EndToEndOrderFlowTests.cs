using Xunit;
using FluentAssertions;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingEngine.Integration.Tests;

public class EndToEndOrderFlowTests
{
    private readonly IMarketDataManager _marketData;
    private readonly IPortfolioManager _portfolio;
    private readonly IOrderHandler _orderHandler;

    public EndToEndOrderFlowTests()
    {
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        _marketData = new MarketDataManager(mockLogger1.Object, config);
        _portfolio = new PortfolioManager(mockLogger2.Object, config);
        _orderHandler = new OrderHandler(mockLogger3.Object, _portfolio, _marketData);
    }

    [Fact]
    public void FullOrderFlow_BuyOrder_CreatesPositionUpdatesPortfolio()
    {
        // Arrange
        var initialCash = _portfolio.GetBuyingPower();
        var order = new OrderRequest 
        { 
            Symbol = "AAPL", 
            Quantity = 10, 
            Price = 200, 
            Side = OrderSide.Buy 
        };

        // Act
        var response = _orderHandler.ProcessOrder(order);

        // Assert
        response.Status.Should().Be(OrderStatus.Executed);
        response.ExecutedPrice.Should().Be(175.50m);
        _portfolio.Positions.Should().ContainKey("AAPL");
        _portfolio.Positions["AAPL"].Quantity.Should().Be(10);
        _portfolio.GetBuyingPower().Should().BeLessThan(initialCash);
    }

    [Fact]
    public void FullOrderFlow_SellPartialPosition_CorrectlyRecalculates()
    {
        // Arrange
        var buyOrder = new OrderRequest 
        { 
            Symbol = "GOOGL", 
            Quantity = 20, 
            Price = 200, 
            Side = OrderSide.Buy 
        };
        var buyResponse = _orderHandler.ProcessOrder(buyOrder);
        var initialCashAfterBuy = _portfolio.GetBuyingPower();

        var sellOrder = new OrderRequest 
        { 
            Symbol = "GOOGL", 
            Quantity = 10, 
            Price = 140, 
            Side = OrderSide.Sell 
        };

        // Act
        var sellResponse = _orderHandler.ProcessOrder(sellOrder);

        // Assert
        sellResponse.Status.Should().Be(OrderStatus.Executed);
        _portfolio.Positions["GOOGL"].Quantity.Should().Be(10);
        _portfolio.GetBuyingPower().Should().BeGreaterThan(initialCashAfterBuy);
    }

    [Fact]
    public void FullOrderFlow_ComplexSequence_BuyThenSellThenBuyAgain_CorrectResults()
    {
        // Arrange & Act
        var buy1 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "MSFT", 
            Quantity = 50, 
            Price = 400, 
            Side = OrderSide.Buy 
        });
        
        var sell1 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "MSFT", 
            Quantity = 25, 
            Price = 370, 
            Side = OrderSide.Sell 
        });
        
        var buy2 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "MSFT", 
            Quantity = 30, 
            Price = 400, 
            Side = OrderSide.Buy 
        });

        // Assert
        buy1.Status.Should().Be(OrderStatus.Executed);
        sell1.Status.Should().Be(OrderStatus.Executed);
        buy2.Status.Should().Be(OrderStatus.Executed);
        
        _portfolio.Positions["MSFT"].Quantity.Should().Be(55);
    }

    [Fact]
    public void FullOrderFlow_BuyMultipleSymbols_TracksAllPositions()
    {
        // Act
        var response1 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "AAPL", 
            Quantity = 10, 
            Price = 200, 
            Side = OrderSide.Buy 
        });
        
        var response2 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "GOOGL", 
            Quantity = 5, 
            Price = 150, 
            Side = OrderSide.Buy 
        });
        
        var response3 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "TSLA", 
            Quantity = 20, 
            Price = 300, 
            Side = OrderSide.Buy 
        });

        // Assert
        response1.Status.Should().Be(OrderStatus.Executed);
        response2.Status.Should().Be(OrderStatus.Executed);
        response3.Status.Should().Be(OrderStatus.Executed);
        
        _portfolio.Positions.Should().HaveCount(3);
        _portfolio.Positions.Keys.Should().Contain("AAPL", "GOOGL", "TSLA");
    }

    [Fact]
    public void FullOrderFlow_SellFullPosition_RemovesFromPortfolio()
    {
        // Arrange
        _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "AMZN", 
            Quantity = 50, 
            Price = 200, 
            Side = OrderSide.Buy 
        });

        // Act
        var sellResponse = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "AMZN", 
            Quantity = 50, 
            Price = 170, 
            Side = OrderSide.Sell 
        });

        // Assert
        sellResponse.Status.Should().Be(OrderStatus.Executed);
        _portfolio.Positions.Should().NotContainKey("AMZN");
    }

    [Fact]
    public void FullOrderFlow_InsufficientCashRejection_DoesNotModifyPortfolio()
    {
        // Arrange
        var initialCash = _portfolio.GetBuyingPower();
        var initialPositionsCount = _portfolio.Positions.Count;

        // Act - Try to buy more than we can afford
        var response = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "NVDA", 
            Quantity = 200, 
            Price = 900, 
            Side = OrderSide.Buy 
        });

        // Assert
        response.Status.Should().Be(OrderStatus.Rejected);
        response.Message.Should().Contain("Insufficient cash");
        _portfolio.GetBuyingPower().Should().Be(initialCash);
        _portfolio.Positions.Should().HaveCount(initialPositionsCount);
    }

    [Fact]
    public void FullOrderFlow_ProfitableRoundTrip_IncreasesCash()
    {
        // Arrange
        var initialCash = _portfolio.GetBuyingPower();

        // Act - Buy low, sell high
        _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "META", 
            Quantity = 10, 
            Price = 500, 
            Side = OrderSide.Buy 
        });
        
        var finalResponse = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "META", 
            Quantity = 10, 
            Price = 480, 
            Side = OrderSide.Sell 
        });

        // Assert
        finalResponse.Status.Should().Be(OrderStatus.Executed);
        var profit = _portfolio.GetBuyingPower() - initialCash;
        profit.Should().BeGreaterThan(0); // Sold at higher market price than bought
    }

    [Fact]
    public void FullOrderFlow_InvalidOrderDoesNotAffectValidOrders()
    {
        // Arrange
        var validOrder1 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "AAPL", 
            Quantity = 10, 
            Price = 200, 
            Side = OrderSide.Buy 
        });
        
        var cashAfterValid = _portfolio.GetBuyingPower();

        // Act - Submit invalid order
        var invalidOrder = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "INVALID", 
            Quantity = 10, 
            Price = 100, 
            Side = OrderSide.Buy 
        });

        // Process another valid order
        var validOrder2 = _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "GOOGL", 
            Quantity = 5, 
            Price = 150, 
            Side = OrderSide.Buy 
        });

        // Assert
        invalidOrder.Status.Should().Be(OrderStatus.Rejected);
        validOrder1.Status.Should().Be(OrderStatus.Executed);
        validOrder2.Status.Should().Be(OrderStatus.Executed);
        _portfolio.Positions.Should().HaveCount(2);
    }

    [Fact]
    public void FullOrderFlow_AverageCostCalculation_WorksAcrossMultipleBuys()
    {
        // Act
        _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "TSLA", 
            Quantity = 10, 
            Price = 300, 
            Side = OrderSide.Buy 
        }); // Executes at 248.75

        _orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "TSLA", 
            Quantity = 10, 
            Price = 300, 
            Side = OrderSide.Buy 
        }); // Executes at 248.75

        // Assert
        _portfolio.Positions["TSLA"].Quantity.Should().Be(20);
        _portfolio.Positions["TSLA"].AverageCost.Should().Be(248.75m);
    }
}
