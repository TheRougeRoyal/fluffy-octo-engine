using Microsoft.Extensions.Logging;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

/// <summary>
/// Orchestrates order processing by delegating to specialized services.
/// Follows Dependency Inversion Principle - depends on abstractions.
/// Follows Single Responsibility - orchestration only, not validation/matching/execution.
/// </summary>
public class OrderHandler : IOrderHandler
{
    private readonly ILogger<OrderHandler> _logger;
    private readonly IOrderValidator _validator;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ITradeExecutor _tradeExecutor;
    private readonly IMarketDataManager _marketDataManager;
    private readonly IPersistenceService _persistenceService;
    private readonly IPortfolioManager _portfolioManager;
    private readonly object _lock = new();
    private int _orderCounter = 0;

    public OrderHandler(
        ILogger<OrderHandler> logger,
        IPortfolioManager portfolioManager,
        IMarketDataManager marketDataManager,
        IPersistenceService persistenceService)
    {
        _logger = logger;
        _marketDataManager = marketDataManager;
        _portfolioManager = portfolioManager;
        _persistenceService = persistenceService;

        // Create dependencies with concrete implementations
        _validator = new OrderValidator(marketDataManager);
        _matchingEngine = new MatchingEngine(portfolioManager);
        _tradeExecutor = new TradeExecutor(new Logger<TradeExecutor>(new LoggerFactory()), portfolioManager);
    }

    /// <summary>
    /// Constructor for testing/DI - allows injection of all dependencies.
    /// </summary>
    public OrderHandler(
        ILogger<OrderHandler> logger,
        IOrderValidator validator,
        IMatchingEngine matchingEngine,
        ITradeExecutor tradeExecutor,
        IMarketDataManager marketDataManager,
        IPersistenceService persistenceService,
        IPortfolioManager portfolioManager)
    {
        _logger = logger;
        _validator = validator;
        _matchingEngine = matchingEngine;
        _tradeExecutor = tradeExecutor;
        _marketDataManager = marketDataManager;
        _persistenceService = persistenceService;
        _portfolioManager = portfolioManager;
    }

    public OrderResponse ProcessOrder(OrderRequest order)
    {
        lock (_lock)
        {
            var orderId = GenerateOrderId();
            
            _logger.LogInformation(
                "Processing order {OrderId}: {Side} {Quantity} {Symbol} @ ${Price:N2}",
                orderId, order.Side, order.Quantity, order.Symbol, order.Price);

            // Step 1: Validate order
            var validation = _validator.Validate(order);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Order {OrderId} rejected: {Reason}", orderId, validation.ErrorMessage);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = validation.ErrorMessage!
                };
            }

            // Step 2: Get current market price
            decimal marketPrice;
            try
            {
                marketPrice = _marketDataManager.GetPrice(order.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get market price for {Symbol}", order.Symbol);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = $"Failed to get market price: {ex.Message}"
                };
            }

            // Step 3: Match order against market conditions
            var matchResult = _matchingEngine.TryMatch(order, marketPrice);
            if (!matchResult.IsMatched)
            {
                _logger.LogWarning("Order {OrderId} rejected: {Reason}", orderId, matchResult.Reason);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = matchResult.Reason
                };
            }

            // Step 4: Execute the trade
            try
            {
                decimal cashBefore = _portfolioManager.CashBalance;
                _tradeExecutor.ExecuteTrade(order, marketPrice);
                decimal cashAfter = _portfolioManager.CashBalance;

                _logger.LogInformation(
                    "Order {OrderId} executed successfully at ${Price:N2}",
                    orderId, marketPrice);

                // Persist to database (fire and forget)
                _ = _persistenceService.OnTradeExecutedAsync(
                    orderId,
                    order.Symbol,
                    order.Quantity,
                    marketPrice,
                    order.Side,
                    cashBefore,
                    cashAfter);

                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Executed,
                    ExecutedPrice = marketPrice,
                    ExecutedQuantity = order.Quantity,
                    Message = $"Order executed successfully at ${marketPrice:N2}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute order {OrderId}", orderId);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = $"Execution failed: {ex.Message}"
                };
            }
        }
    }

    private string GenerateOrderId()
    {
        _orderCounter++;
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{_orderCounter:D6}";
    }
}
