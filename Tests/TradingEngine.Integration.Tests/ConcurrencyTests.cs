using Xunit;
using FluentAssertions;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingEngine.Integration.Tests;

public class ConcurrencyTests
{
    [Fact]
    public void ConcurrentBuys_SameSymbol_AllProcessedSuccessfully()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 200000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        var orders = Enumerable.Range(0, 100)
            .Select(_ => new OrderRequest 
            { 
                Symbol = "AAPL", 
                Quantity = 1, 
                Price = 200, 
                Side = OrderSide.Buy 
            })
            .ToList();

        // Act
        var responses = orders
            .AsParallel()
            .Select(o => orderHandler.ProcessOrder(o))
            .ToList();

        // Assert
        var successCount = responses.Count(r => r.Status == OrderStatus.Executed);
        successCount.Should().BeGreaterThan(0);
        
        if (portfolio.Positions.ContainsKey("AAPL"))
        {
            portfolio.Positions["AAPL"].Quantity.Should().Be(successCount);
        }
    }

    [Fact]
    public async Task RaceCondition_NeverAllowsNegativeCash()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Act - Concurrent orders that total more than available cash
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() =>
                orderHandler.ProcessOrder(new OrderRequest 
                { 
                    Symbol = "AAPL", 
                    Quantity = 10, 
                    Price = 200, 
                    Side = OrderSide.Buy 
                })
            ))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Cash should never go negative
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ConcurrentBuysAndSells_MaintainPortfolioIntegrity()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 200000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Pre-populate with shares
        orderHandler.ProcessOrder(new OrderRequest 
        { 
            Symbol = "GOOGL", 
            Quantity = 200, 
            Price = 200, 
            Side = OrderSide.Buy 
        });

        var buyOrders = Enumerable.Range(0, 50)
            .Select(_ => new OrderRequest 
            { 
                Symbol = "GOOGL", 
                Quantity = 1, 
                Price = 200, 
                Side = OrderSide.Buy 
            });

        var sellOrders = Enumerable.Range(0, 50)
            .Select(_ => new OrderRequest 
            { 
                Symbol = "GOOGL", 
                Quantity = 1, 
                Price = 140, 
                Side = OrderSide.Sell 
            });

        var allOrders = buyOrders.Concat(sellOrders).ToList();

        // Act
        var responses = allOrders
            .AsParallel()
            .Select(o => orderHandler.ProcessOrder(o))
            .ToList();

        // Assert
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
        
        if (portfolio.Positions.ContainsKey("GOOGL"))
        {
            portfolio.Positions["GOOGL"].Quantity.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void ConcurrentOrders_DifferentSymbols_AllProcessedCorrectly()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 500000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" };
        var orders = symbols
            .SelectMany(symbol => Enumerable.Range(0, 10)
                .Select(_ => new OrderRequest 
                { 
                    Symbol = symbol, 
                    Quantity = 1, 
                    Price = 1000, 
                    Side = OrderSide.Buy 
                }))
            .ToList();

        // Act
        var responses = orders
            .AsParallel()
            .Select(o => orderHandler.ProcessOrder(o))
            .ToList();

        // Assert
        var executedCount = responses.Count(r => r.Status == OrderStatus.Executed);
        executedCount.Should().BeGreaterThan(0);
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task StressTest_ThousandsConcurrentOrders_SystemStable()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 1000000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Act - Submit 1000 concurrent orders
        var tasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() =>
            {
                var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
                var symbol = symbols[i % symbols.Length];
                return orderHandler.ProcessOrder(new OrderRequest 
                { 
                    Symbol = symbol, 
                    Quantity = 1, 
                    Price = 1000, 
                    Side = OrderSide.Buy 
                });
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
        responses.Should().NotContainNulls();
        responses.Should().AllSatisfy(r => 
        {
            r.OrderId.Should().NotBeNullOrEmpty();
            r.Status.Should().BeOneOf(OrderStatus.Executed, OrderStatus.Rejected);
        });
    }

    [Fact]
    public void SequentialOrders_ProduceDeterministicResults()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Act - Execute same sequence twice
        var responses1 = new List<OrderResponse>();
        for (int i = 0; i < 10; i++)
        {
            responses1.Add(orderHandler.ProcessOrder(new OrderRequest 
            { 
                Symbol = "AAPL", 
                Quantity = 1, 
                Price = 200, 
                Side = OrderSide.Buy 
            }));
        }

        var cash1 = portfolio.GetBuyingPower();
        var position1Quantity = portfolio.Positions.ContainsKey("AAPL") ? portfolio.Positions["AAPL"].Quantity : 0;

        // Reset and run again
        var portfolio2 = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler2 = new OrderHandler(mockLogger3.Object, portfolio2, marketData);

        var responses2 = new List<OrderResponse>();
        for (int i = 0; i < 10; i++)
        {
            responses2.Add(orderHandler2.ProcessOrder(new OrderRequest 
            { 
                Symbol = "AAPL", 
                Quantity = 1, 
                Price = 200, 
                Side = OrderSide.Buy 
            }));
        }

        var cash2 = portfolio2.GetBuyingPower();
        var position2Quantity = portfolio2.Positions.ContainsKey("AAPL") ? portfolio2.Positions["AAPL"].Quantity : 0;

        // Assert - Should produce identical results
        cash1.Should().Be(cash2);
        position1Quantity.Should().Be(position2Quantity);
        
        for (int i = 0; i < responses1.Count; i++)
        {
            responses1[i].Status.Should().Be(responses2[i].Status);
            responses1[i].ExecutedPrice.Should().Be(responses2[i].ExecutedPrice);
        }
    }

    [Fact]
    public void ConcurrentPriceFetches_NoDeadlock()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MarketDataManager>>();
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" }
        });

        var marketData = new MarketDataManager(mockLogger.Object, config);

        // Act - Concurrent price reads
        var tasks = Enumerable.Range(0, 500)
            .Select(i => Task.Run(() =>
            {
                var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
                var symbol = symbols[i % symbols.Length];
                return marketData.GetPrice(symbol);
            }))
            .ToArray();

        // Assert - Should complete without deadlock
        var prices = Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
        prices.Should().BeTrue();
    }

    [Fact]
    public void ConcurrentPortfolioUpdates_NoRaceConditions()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 500000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);

        var initialCash = portfolio.GetBuyingPower();

        // Act - Concurrent buys of different symbols
        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() =>
            {
                var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
                var symbol = symbols[i % symbols.Length];
                var price = marketData.GetPrice(symbol);
                portfolio.UpdateOnBuy(symbol, 10, price);
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert
        var finalCash = portfolio.GetBuyingPower();
        finalCash.Should().BeLessThan(initialCash);
        finalCash.Should().BeGreaterThanOrEqualTo(0);
        
        // All positions should have positive quantities
        foreach (var position in portfolio.Positions.Values)
        {
            position.Quantity.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void ConcurrentMixedOperations_BuysAndSells_MaintsConsistency()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 300000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Pre-populate portfolio
        for (int i = 0; i < 3; i++)
        {
            orderHandler.ProcessOrder(new OrderRequest 
            { 
                Symbol = "AAPL", 
                Quantity = 100, 
                Price = 1000, 
                Side = OrderSide.Buy 
            });
        }

        var initialPositions = portfolio.Positions["AAPL"].Quantity;
        var initialCash = portfolio.GetBuyingPower();

        // Act - Mix of buys and sells concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                if (i % 2 == 0)
                {
                    return orderHandler.ProcessOrder(new OrderRequest 
                    { 
                        Symbol = "AAPL", 
                        Quantity = 1, 
                        Price = 200, 
                        Side = OrderSide.Buy 
                    });
                }
                else
                {
                    return orderHandler.ProcessOrder(new OrderRequest 
                    { 
                        Symbol = "AAPL", 
                        Quantity = 1, 
                        Price = 170, 
                        Side = OrderSide.Sell 
                    });
                }
            }))
            .ToArray();

        var responses = Task.WhenAll(tasks).Result;

        // Assert
        portfolio.Positions["AAPL"].Quantity.Should().BeGreaterThan(0);
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
        
        // Verify consistency - total value should make sense
        var currentPositionValue = portfolio.Positions.Values
            .Sum(p => p.Quantity * p.AverageCost);
        
        (portfolio.GetBuyingPower() + currentPositionValue).Should().BeLessThanOrEqualTo(300000m);
    }

    [Fact]
    public async Task AsyncProcessing_MultipleClientsSimulated_AllOrdersProcessed()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 1000000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        // Simulate 7 concurrent clients, each sending 30 orders
        var clientTasks = Enumerable.Range(0, 7)
            .Select(clientId => Task.Run(() =>
            {
                var orders = Enumerable.Range(0, 30)
                    .Select(orderIdx => new OrderRequest 
                    { 
                        Symbol = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA" }[clientId],
                        Quantity = (orderIdx % 5) + 1,
                        Price = 1000,
                        Side = (orderIdx % 2 == 0) ? OrderSide.Buy : OrderSide.Sell
                    })
                    .ToList();

                return orders
                    .Select(o => orderHandler.ProcessOrder(o))
                    .ToList();
            }))
            .ToArray();

        var allResponses = await Task.WhenAll(clientTasks);

        // Assert
        var totalOrders = allResponses.Sum(r => r.Count);
        totalOrders.Should().Be(210);

        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
        
        var executedOrders = allResponses.SelectMany(r => r).Count(r => r.Status == OrderStatus.Executed);
        executedOrders.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConcurrentInvalidOperations_NoCorruption()
    {
        // Arrange
        var mockLogger1 = new Mock<ILogger<MarketDataManager>>();
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var mockLogger3 = new Mock<ILogger<OrderHandler>>();

        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT" }
        });

        var marketData = new MarketDataManager(mockLogger1.Object, config);
        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        var orderHandler = new OrderHandler(mockLogger3.Object, portfolio, marketData);

        var initialCash = portfolio.GetBuyingPower();

        // Act - Mix of valid and invalid orders concurrently
        var tasks = Enumerable.Range(0, 200)
            .Select(i => Task.Run(() =>
            {
                if (i % 3 == 0)
                {
                    // Invalid symbol
                    return orderHandler.ProcessOrder(new OrderRequest 
                    { 
                        Symbol = "INVALID", 
                        Quantity = 10, 
                        Price = 100, 
                        Side = OrderSide.Buy 
                    });
                }
                else if (i % 3 == 1)
                {
                    // Invalid quantity
                    return orderHandler.ProcessOrder(new OrderRequest 
                    { 
                        Symbol = "AAPL", 
                        Quantity = 0, 
                        Price = 100, 
                        Side = OrderSide.Buy 
                    });
                }
                else
                {
                    // Valid order
                    return orderHandler.ProcessOrder(new OrderRequest 
                    { 
                        Symbol = "AAPL", 
                        Quantity = 1, 
                        Price = 200, 
                        Side = OrderSide.Buy 
                    });
                }
            }))
            .ToArray();

        var responses = Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

        // Assert
        responses.Should().BeTrue();
        portfolio.GetBuyingPower().Should().BeLessThanOrEqualTo(initialCash);
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ParallelPortfolioQueries_ReturnsConsistentData()
    {
        // Arrange
        var mockLogger2 = new Mock<ILogger<PortfolioManager>>();
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL", "MSFT" }
        });

        var portfolio = new PortfolioManager(mockLogger2.Object, config);
        portfolio.UpdateOnBuy("AAPL", 50, 100);
        portfolio.UpdateOnBuy("GOOGL", 30, 200);

        var expectedCash = portfolio.GetBuyingPower();
        var expectedAAPLQty = portfolio.Positions["AAPL"].Quantity;

        // Act - Concurrent reads of portfolio state
        var cashReadings = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => portfolio.GetBuyingPower())
            .ToList();

        var aaplReadings = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => portfolio.HasSufficientShares("AAPL", expectedAAPLQty))
            .ToList();

        // Assert - All readings should be consistent
        cashReadings.Should().AllBe(expectedCash);
        aaplReadings.Should().AllBe(true);
    }
}
