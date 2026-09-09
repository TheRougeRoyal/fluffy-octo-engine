using Xunit;
using System.Collections.Concurrent;
using FluentAssertions;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradingEngine.Models.Quant;
using TradingEngine.Services.Quant;

namespace TradingEngine.Integration.Tests;

public class ConcurrencyTests
{
    private static OrderHandler CreateOrderHandler(
        Mock<ILogger<OrderHandler>> logger,
        IPortfolioManager portfolio,
        IMarketDataManager marketData,
        Mock<IPdeModel>? pdeModel = null,
        Mock<ILimitOrderBook>? orderBook = null,
        Mock<IPersistenceService>? persistence = null,
        Mock<IMatchingEngine>? matchingEngine = null)
    {
        var suppliedOrderBook = orderBook is not null;
        orderBook ??= new Mock<ILimitOrderBook>();
        if (!suppliedOrderBook)
        {
            orderBook
                .Setup(book => book.MatchIteratively(It.IsAny<OrderRequest>()))
                .Returns(new[] { (100m, 1) });
        }
        var suppliedMatchingEngine = matchingEngine is not null;
        matchingEngine ??= new Mock<IMatchingEngine>();
        if (!suppliedMatchingEngine)
        {
            matchingEngine
                .Setup(engine => engine.TryMatch(It.IsAny<OrderRequest>(), It.IsAny<decimal>()))
                .Returns((true, "Match successful"));
        }
        if (pdeModel is null)
        {
            pdeModel = new Mock<IPdeModel>();
            pdeModel
                .Setup(p => p.GetFairValueAsync(It.IsAny<PdeRequest>()))
                .ReturnsAsync(new PdeResponse(true, 100, 100, 0, new Greeks(0, 0, 0, 0, 0), string.Empty));
        }
        var risk = new Mock<IRiskManagementService>();
        risk.Setup(r => r.ValidateOrder(It.IsAny<OrderRequest>(), It.IsAny<decimal>()))
            .Returns((true, string.Empty));
        persistence ??= new Mock<IPersistenceService>();

        return new OrderHandler(
            logger.Object,
            new OrderValidator(marketData),
            matchingEngine.Object,
            new TradeExecutor(new Mock<ILogger<TradeExecutor>>().Object, portfolio),
            marketData,
            persistence.Object,
            pdeModel.Object,
            portfolio,
            orderBook.Object,
            risk.Object);
    }

    [Fact]
    public async Task DuplicateClientOrderId_IsRejectedWithoutAdditionalMutation()
    {
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL" }
        });
        var marketData = new MarketDataManager(new Mock<ILogger<MarketDataManager>>().Object, config);
        var portfolio = new PortfolioManager(new Mock<ILogger<PortfolioManager>>().Object, config);
        var matchingEngine = new Mock<IMatchingEngine>();
        matchingEngine
            .Setup(engine => engine.TryMatch(It.IsAny<OrderRequest>(), It.IsAny<decimal>()))
            .Returns((true, "Match successful"));
        var persistedClientOrderIds = new ConcurrentDictionary<string, byte>();
        var persistence = new Mock<IPersistenceService>();
        persistence
            .Setup(service => service.TradeExistsByClientOrderIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string clientOrderId) => persistedClientOrderIds.ContainsKey(clientOrderId));
        persistence
            .Setup(service => service.OnTradeExecutedAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<OrderSide>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<Greeks>()))
            .Callback<string, string?, string, int, decimal, OrderSide, decimal, decimal, Greeks>(
                (_, clientOrderId, _, _, _, _, _, _, _) => persistedClientOrderIds.TryAdd(clientOrderId!, 0))
            .Returns(Task.CompletedTask);
        var handler = CreateOrderHandler(
            new Mock<ILogger<OrderHandler>>(),
            portfolio,
            marketData,
            persistence: persistence,
            matchingEngine: matchingEngine);
        var order = new OrderRequest
        {
            OrderId = "client-order-1",
            Symbol = "AAPL",
            Quantity = 1,
            Price = 100,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market
        };

        var firstResponse = await handler.ProcessOrderAsync(order);
        var secondResponse = await handler.ProcessOrderAsync(order);

        firstResponse.Status.Should().Be(OrderStatus.Executed);
        secondResponse.Status.Should().Be(OrderStatus.Rejected);
        secondResponse.Message.Should().Contain("Duplicate order");
        portfolio.Positions["AAPL"].Quantity.Should().Be(1);
        persistedClientOrderIds.Should().ContainSingle();
        matchingEngine.Verify(engine => engine.TryMatch(It.IsAny<OrderRequest>(), It.IsAny<decimal>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateClientOrderId_ConcurrentSubmissionsOnlyExecuteOnce()
    {
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL" }
        });
        var marketData = new MarketDataManager(new Mock<ILogger<MarketDataManager>>().Object, config);
        var portfolio = new PortfolioManager(new Mock<ILogger<PortfolioManager>>().Object, config);
        var matchingEngine = new Mock<IMatchingEngine>();
        matchingEngine
            .Setup(engine => engine.TryMatch(It.IsAny<OrderRequest>(), It.IsAny<decimal>()))
            .Returns((true, "Match successful"));
        var persistedClientOrderIds = new ConcurrentDictionary<string, byte>();
        var persistence = new Mock<IPersistenceService>();
        persistence
            .Setup(service => service.TradeExistsByClientOrderIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string clientOrderId) => persistedClientOrderIds.ContainsKey(clientOrderId));
        persistence
            .Setup(service => service.OnTradeExecutedAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<OrderSide>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<Greeks>()))
            .Callback<string, string?, string, int, decimal, OrderSide, decimal, decimal, Greeks>(
                (_, clientOrderId, _, _, _, _, _, _, _) => persistedClientOrderIds.TryAdd(clientOrderId!, 0))
            .Returns(Task.CompletedTask);
        var handler = CreateOrderHandler(
            new Mock<ILogger<OrderHandler>>(),
            portfolio,
            marketData,
            persistence: persistence,
            matchingEngine: matchingEngine);
        var order = new OrderRequest
        {
            OrderId = "client-order-concurrent",
            Symbol = "AAPL",
            Quantity = 1,
            Price = 100,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market
        };
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submissions = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return await handler.ProcessOrderAsync(order);
            }))
            .ToArray();

        start.SetResult(true);
        var responses = await Task.WhenAll(submissions);

        responses.Count(response => response.Status == OrderStatus.Executed).Should().Be(1);
        responses.Count(response => response.Status == OrderStatus.Rejected).Should().Be(1);
        portfolio.Positions["AAPL"].Quantity.Should().Be(1);
        persistedClientOrderIds.Should().ContainSingle();
        matchingEngine.Verify(engine => engine.TryMatch(It.IsAny<OrderRequest>(), It.IsAny<decimal>()), Times.Once);
    }

    [Fact]
    public async Task ConcurrentBuys_SameSymbol_AllProcessedSuccessfully()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

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
        var responses = await Task.WhenAll(orders.Select(o => orderHandler.ProcessOrderAsync(o)));

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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Act - Concurrent orders that total more than available cash
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(async () =>
                await orderHandler.ProcessOrderAsync(new OrderRequest
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
    public async Task ConcurrentBuysAndSells_MaintainPortfolioIntegrity()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Pre-populate with shares
        await orderHandler.ProcessOrderAsync(new OrderRequest
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
        var responses = await Task.WhenAll(allOrders.Select(o => orderHandler.ProcessOrderAsync(o)));

        // Assert
        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);

        if (portfolio.Positions.ContainsKey("GOOGL"))
        {
            portfolio.Positions["GOOGL"].Quantity.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task ConcurrentOrders_DifferentSymbols_AllProcessedCorrectly()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

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
        var responses = await Task.WhenAll(orders.Select(o => orderHandler.ProcessOrderAsync(o)));

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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Act - Submit 1000 concurrent orders
        var tasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(async () =>
            {
                var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
                var symbol = symbols[i % symbols.Length];
                return await orderHandler.ProcessOrderAsync(new OrderRequest
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
    public async Task SequentialOrders_ProduceDeterministicResults()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Act - Execute same sequence twice
        var responses1 = new List<OrderResponse>();
        for (int i = 0; i < 10; i++)
        {
            responses1.Add(await orderHandler.ProcessOrderAsync(new OrderRequest
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
        var orderHandler2 = CreateOrderHandler(mockLogger3, portfolio2, marketData);

        var responses2 = new List<OrderResponse>();
        for (int i = 0; i < 10; i++)
        {
            responses2.Add(await orderHandler2.ProcessOrderAsync(new OrderRequest
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
    public async Task PerSymbolLocks_AllowDifferentSymbolsInParallel_ButSerializeSameSymbol()
    {
        var config = Options.Create(new TradingServerConfig
        {
            InitialCashBalance = 100000m,
            Port = 5000,
            TradeableSymbols = new List<string> { "AAPL", "GOOGL" }
        });

        var marketData = new MarketDataManager(
            new Mock<ILogger<MarketDataManager>>().Object,
            config);
        var portfolio = new PortfolioManager(
            new Mock<ILogger<PortfolioManager>>().Object,
            config);
        var slowCallStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pdeCallCount = 0;
        var pdeModel = new Mock<IPdeModel>();
        pdeModel
            .Setup(p => p.GetFairValueAsync(It.IsAny<PdeRequest>()))
            .Returns((PdeRequest _) => Task.Run(async () =>
            {
                if (Interlocked.Increment(ref pdeCallCount) == 1)
                {
                    slowCallStarted.SetResult(true);
                    await Task.Delay(500);
                }

                return new PdeResponse(true, 100, 100, 0, new Greeks(0, 0, 0, 0, 0), string.Empty);
            }));
        var orderHandler = CreateOrderHandler(
            new Mock<ILogger<OrderHandler>>(),
            portfolio,
            marketData,
            pdeModel);

        var slowSymbolTask = orderHandler.ProcessOrderAsync(new OrderRequest
        {
            Symbol = "AAPL",
            Quantity = 1,
            Price = 100,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market
        });
        await slowCallStarted.Task;

        var fastSymbolTask = orderHandler.ProcessOrderAsync(new OrderRequest
        {
            Symbol = "GOOGL",
            Quantity = 1,
            Price = 100,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market
        });

        var firstCompleted = await Task.WhenAny(slowSymbolTask, fastSymbolTask);
        firstCompleted.Should().BeSameAs(fastSymbolTask);
        slowSymbolTask.IsCompleted.Should().BeFalse();
        (await fastSymbolTask).Status.Should().Be(OrderStatus.Executed);
        (await slowSymbolTask).Status.Should().Be(OrderStatus.Executed);

        var activeMatches = 0;
        var maximumActiveMatches = 0;
        var orderBook = new Mock<ILimitOrderBook>();
        orderBook
            .Setup(book => book.MatchIteratively(It.IsAny<OrderRequest>()))
            .Returns(() =>
            {
                var active = Interlocked.Increment(ref activeMatches);
                InterlockedMax(ref maximumActiveMatches, active);
                Thread.Sleep(100);
                Interlocked.Decrement(ref activeMatches);
                return new[] { (100m, 1) };
            });
        var sameSymbolPde = new Mock<IPdeModel>();
        sameSymbolPde
            .Setup(p => p.GetFairValueAsync(It.IsAny<PdeRequest>()))
            .Returns((PdeRequest _) => Task.Run(async () =>
            {
                await Task.Delay(100);
                return new PdeResponse(true, 100, 100, 0, new Greeks(0, 0, 0, 0, 0), string.Empty);
            }));
        var sameSymbolHandler = CreateOrderHandler(
            new Mock<ILogger<OrderHandler>>(),
            portfolio,
            marketData,
            sameSymbolPde,
            orderBook);

        var sameSymbolTasks = Enumerable.Range(0, 2)
            .Select(_ => sameSymbolHandler.ProcessOrderAsync(new OrderRequest
            {
                Symbol = "AAPL",
                Quantity = 1,
                Price = 100,
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit
            }))
            .ToArray();

        await Task.WhenAll(sameSymbolTasks);
        maximumActiveMatches.Should().Be(1);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref location);
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref location, value, current) != current);
    }

    [Fact]
    public async Task ConcurrentMixedOperations_BuysAndSells_MaintsConsistency()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Pre-populate portfolio
        for (int i = 0; i < 3; i++)
        {
            await orderHandler.ProcessOrderAsync(new OrderRequest
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
            .Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                {
                    return await orderHandler.ProcessOrderAsync(new OrderRequest
                    {
                        Symbol = "AAPL",
                        Quantity = 1,
                        Price = 200,
                        Side = OrderSide.Buy
                    });
                }
                else
                {
                    return await orderHandler.ProcessOrderAsync(new OrderRequest
                    {
                        Symbol = "AAPL",
                        Quantity = 1,
                        Price = 170,
                        Side = OrderSide.Sell
                    });
                }
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        // Simulate 7 concurrent clients, each sending 30 orders
        var clientTasks = Enumerable.Range(0, 7)
            .Select(clientId => Task.Run(async () =>
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

                return await Task.WhenAll(orders.Select(o => orderHandler.ProcessOrderAsync(o)));
            }))
            .ToArray();

        var allResponses = await Task.WhenAll(clientTasks);

        // Assert
        var totalOrders = allResponses.Sum(r => r.Length);
        totalOrders.Should().Be(210);

        portfolio.GetBuyingPower().Should().BeGreaterThanOrEqualTo(0);

        var executedOrders = allResponses.SelectMany(r => r).Count(r => r.Status == OrderStatus.Executed);
        executedOrders.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConcurrentInvalidOperations_NoCorruption()
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
        var orderHandler = CreateOrderHandler(mockLogger3, portfolio, marketData);

        var initialCash = portfolio.GetBuyingPower();

        // Act - Mix of valid and invalid orders concurrently
        var tasks = Enumerable.Range(0, 200)
            .Select(i => Task.Run(async () =>
            {
                if (i % 3 == 0)
                {
                    // Invalid symbol
                    return await orderHandler.ProcessOrderAsync(new OrderRequest
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
                    return await orderHandler.ProcessOrderAsync(new OrderRequest
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
                    return await orderHandler.ProcessOrderAsync(new OrderRequest
                    {
                        Symbol = "AAPL",
                        Quantity = 1,
                        Price = 200,
                        Side = OrderSide.Buy
                    });
                }
            }))
            .ToArray();

        var allTasks = Task.WhenAll(tasks);
        var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert
        completed.Should().BeSameAs(allTasks);
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
        cashReadings.Should().AllSatisfy(value => value.Should().Be(expectedCash));
        aaplReadings.Should().AllSatisfy(value => value.Should().BeTrue());
    }
}
