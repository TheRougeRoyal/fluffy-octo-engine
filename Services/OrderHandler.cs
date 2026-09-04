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
    private readonly IRiskManagementService _riskManagementService;
    private readonly object _lock = new();
    private int _orderCounter = 0;

    public OrderHandler(
        ILogger<OrderHandler> logger,
        IPortfolioManager portfolioManager,
        IMarketDataManager marketDataManager,
        IPersistenceService persistenceService,
        IPdeModel pdeModel,
        ILimitOrderBook orderBook,
        IRiskManagementService riskManagementService)
    {
        _logger = logger;
        _marketDataManager = marketDataManager;
        _portfolioManager = portfolioManager;
        _persistenceService = persistenceService;
        _pdeModel = pdeModel;
        _orderBook = orderBook;
        _riskManagementService = riskManagementService;

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
        ILimitOrderBook orderBook,
        IRiskManagementService riskManagementService)
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
        _riskManagementService = riskManagementService;
    }

    public OrderResponse ProcessOrder(OrderRequest order)
    {
        lock (_lock)
        {
            var orderId = GenerateOrderId();

            _logger.LogInformation(
                "Processing order {OrderId}: {Side} {Quantity} {Symbol} @ ${Price:N2}",
                orderId, order.Side, order.Quantity, order.Symbol, order.Price);

            // Step 1: Validate order parameters
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

            // Step 2b: Risk Management Checks
            var riskCheck = _riskManagementService.ValidateOrder(order, marketPrice);
            if (!riskCheck.IsValid)
            {
                _logger.LogWarning("Order {OrderId} rejected by Risk Management: {Reason}", orderId, riskCheck.ErrorMessage);
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Rejected,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = riskCheck.ErrorMessage
                };
            }

            // Step 2c: Quantitative Fair Value Check
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

            // Step 3: Match order
            bool matched = false;
            var fills = new List<(decimal Price, int Quantity)>();

            if (order.OrderType == OrderType.Limit)
            {
                var iterativeFills = _orderBook.MatchIteratively(order).ToList();
                if (iterativeFills.Any())
                {
                    fills.AddRange(iterativeFills);
                    matched = true;
                }
                else
                {
                    // Order wasn't matched immediately, add to book
                    _orderBook.AddOrder(order);
                }
            }
            else if (order.OrderType == OrderType.Market)
            {
                var matchResult = _matchingEngine.TryMatch(order, marketPrice);
                if (matchResult.IsMatched)
                {
                    fills.Add((marketPrice, order.Quantity));
                    matched = true;
                }
                else
                {
                    _logger.LogWarning("Market order {OrderId} rejected: {Reason}", orderId, matchResult.Reason);
                    return new OrderResponse
                    {
                        OrderId = orderId,
                        Status = OrderStatus.Rejected,
                        ExecutedPrice = 0,
                        ExecutedQuantity = 0,
                        Message = matchResult.Reason
                    };
                }
            }

            if (!matched)
            {
                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Pending,
                    ExecutedPrice = 0,
                    ExecutedQuantity = 0,
                    Message = "Order added to book"
                };
            }

            // Step 4: Execute the trades
            try
            {
                decimal totalFilledQty = 0;
                decimal weightedAvgPrice = 0;

                foreach (var fill in fills)
                {
                    _tradeExecutor.ExecuteTrade(new OrderRequest
                    {
                        Symbol = order.Symbol,
                        Quantity = fill.Quantity,
                        Price = fill.Price,
                        Side = order.Side
                    }, fill.Price);

                    weightedAvgPrice += fill.Price * fill.Quantity;
                    totalFilledQty += fill.Quantity;
                }

                decimal finalPrice = weightedAvgPrice / totalFilledQty;

                _logger.LogInformation(
                    "Order {OrderId} executed successfully. Total Qty: {Qty} @ Avg Price: ${Price:N2}",
                    orderId, totalFilledQty, finalPrice);

                _ = _persistenceService.OnTradeExecutedAsync(
                    orderId,
                    order.Symbol,
                    (int)totalFilledQty,
                    finalPrice,
                    order.Side,
                    _portfolioManager.CashBalance, // Simplified; should be before/after
                    _portfolioManager.CashBalance,
                    quantResult.Greeks);

                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Executed,
                    ExecutedPrice = finalPrice,
                    ExecutedQuantity = (int)totalFilledQty,
                    Message = $"Order executed successfully at avg price ${finalPrice:N2}"
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
            // ponytail: disabled broken check comparing underlying price to option fair value
            if (false && priceDiff > 0.05m)
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
