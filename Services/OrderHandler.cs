using Microsoft.Extensions.Logging;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;
using TradingEngine.Models.Quant;
using TradingEngine.Services.Quant;

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
    private readonly IPdeModel _pdeModel;
    private readonly IPortfolioManager _portfolioManager;
    private readonly ILimitOrderBook _orderBook;
    private readonly object _lock = new();
    private int _orderCounter = 0;

    public OrderHandler(
        ILogger<OrderHandler> logger,
        IPortfolioManager portfolioManager,
        IMarketDataManager marketDataManager,
        IPersistenceService persistenceService,
        IPdeModel pdeModel,
        ILimitOrderBook orderBook)
    {
        _logger = logger;
        _marketDataManager = marketDataManager;
        _portfolioManager = portfolioManager;
        _persistenceService = persistenceService;
        _pdeModel = pdeModel;
        _orderBook = orderBook;

        // Create dependencies with concrete implementations
        _validator = new OrderValidator(marketDataManager);
        _matchingEngine = new MatchingEngine(portfolioManager, orderBook);
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
        IPdeModel pdeModel,
        IPortfolioManager portfolioManager,
        ILimitOrderBook orderBook)
    {
        _logger = logger;
        _validator = validator;
        _matchingEngine = matchingEngine;
        _tradeExecutor = tradeExecutor;
        _marketDataManager = marketDataManager;
        _persistenceService = persistenceService;
        _pdeModel = pdeModel;
        _portfolioManager = portfolioManager;
        _orderBook = orderBook;
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

            // Step 2b: Quantitative Fair Value Check
            var quantResult = PerformQuantCheck(order, marketPrice).GetAwaiter().GetResult();
            if (!quantResult.Success)
            {
                _logger.LogWarning("Order {OrderId} rejected by Quant Model: {Reason}", orderId, quantResult.ErrorMessage);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = $"Quant Guardrail: {quantResult.ErrorMessage}"
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

                _ = _persistenceService.OnTradeExecutedAsync(
                    orderId,
                    order.Symbol,
                    order.Quantity,
                    marketPrice,
                    order.Side,
                    cashBefore,
                    cashAfter,
                    quantResult.Greeks);

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

    private async Task<(bool Success, string ErrorMessage, Greeks Greeks)> PerformQuantCheck(OrderRequest order, decimal marketPrice)
    {
        try
        {
            var request = new PdeRequest(
                Spot: (double)marketPrice,
                Strike: (double)marketPrice,
                Maturity: 0.25, // 3 months
                Rate: 0.05,     // 5% risk-free rate
                Volatility: 0.2, // 20% implied volatility
                OptionType: order.Side == OrderSide.Buy ? "call" : "put"
            );

            var response = await _pdeModel.GetFairValueAsync(request);

            if (!response.Success)
            {
                return (false, response.ErrorMessage, new Greeks(0,0,0,0,0));
            }

            decimal priceDiff = Math.Abs(marketPrice - response.PdePrice) / response.PdePrice;
            if (priceDiff > 0.05m)
            {
                return (false, $"Price deviation too high ({priceDiff:P2} vs 5% threshold). Fair Value: {response.PdePrice:C2}", response.Greeks);
            }

            return (true, string.Empty, response.Greeks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quant check failed. Defaulting to allow for system availability.");
            return (true, string.Empty, new Greeks(0,0,0,0,0));
        }
    }

    private string GenerateOrderId()
    {
        _orderCounter++;
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{_orderCounter:D6}";
    }
}
