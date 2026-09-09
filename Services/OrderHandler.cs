using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Models.Quant;
using TradingEngine.Services.Quant;

namespace TradingEngine.Services;

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
    private readonly ConcurrentDictionary<string, object> _locks = new();
    private readonly ConcurrentDictionary<string, ClientOrderLock> _clientOrderLocks = new();
    private readonly object _clientOrderLocksGate = new();
    private int _orderCounter;

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

    public async Task<OrderResponse> ProcessOrderAsync(OrderRequest order)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = "error";

        try
        {
            var response = await ProcessOrderCoreAsync(order);
            outcome = response.Status == OrderStatus.Executed ? "executed" : "rejected";
            return response;
        }
        catch
        {
            outcome = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            TradingEngineInstrumentation.OrderProcessingDurationMs.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("symbol", order.Symbol),
                new KeyValuePair<string, object?>("outcome", outcome));
            _logger.LogInformation(
                "Order processing completed for {Symbol} with outcome {Outcome}. DurationMs: {DurationMs}",
                order.Symbol,
                outcome,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<OrderResponse> ProcessOrderCoreAsync(OrderRequest order)
    {
        var orderId = GenerateOrderId();

        _logger.LogInformation(
            "Processing order {OrderId}: {Side} {Quantity} {Symbol} @ ${Price:N2}",
            orderId, order.Side, order.Quantity, order.Symbol, order.Price);

        var validation = _validator.Validate(order);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Order {OrderId} rejected: {Reason}", orderId, validation.ErrorMessage);
            return Rejected(orderId, validation.ErrorMessage!);
        }

        var clientOrderId = string.IsNullOrWhiteSpace(order.OrderId) ? null : order.OrderId;
        if (clientOrderId is not null && await _persistenceService.TradeExistsByClientOrderIdAsync(clientOrderId))
        {
            return Rejected(orderId, "Duplicate order: this order was already processed");
        }

        ClientOrderLock? clientOrderLock = null;
        var clientOrderLockAcquired = false;
        if (clientOrderId is not null)
        {
            clientOrderLock = AcquireClientOrderLock(clientOrderId);
            await clientOrderLock.WaitAsync();
            clientOrderLockAcquired = true;
        }

        try
        {
            if (clientOrderId is not null &&
                await _persistenceService.TradeExistsByClientOrderIdAsync(clientOrderId))
            {
                return Rejected(orderId, "Duplicate order: this order was already processed");
            }

            decimal marketPrice;
            try
            {
                marketPrice = _marketDataManager.GetPrice(order.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get market price for {Symbol}", order.Symbol);
                return Rejected(orderId, $"Failed to get market price: {ex.Message}");
            }

            var riskCheck = _riskManagementService.ValidateOrder(order, marketPrice);
            if (!riskCheck.IsValid)
            {
                _logger.LogWarning("Order {OrderId} rejected by Risk Management: {Reason}", orderId, riskCheck.ErrorMessage);
                return Rejected(orderId, riskCheck.ErrorMessage);
            }

            // ponytail: skip quant check for equities; enable when options trading is implemented
            /*
            var quantResult = await PerformQuantCheck(order, marketPrice);
            if (!quantResult.Success)
            {
                _logger.LogWarning("Order {OrderId} rejected by Quant Model: {Reason}", orderId, quantResult.ErrorMessage);
                return Rejected(orderId, $"Quant Guardrail: {quantResult.ErrorMessage}");
            }
            */
            var quantResult = (Success: true, ErrorMessage: string.Empty, Greeks: new Greeks(0, 0, 0, 0, 0));

            (int Quantity, decimal Price, decimal CashBefore, decimal CashAfter)? persistenceData = null;
            lock (_locks.GetOrAdd(order.Symbol, _ => new object()))
            {
                var fills = new List<(decimal Price, int Quantity)>();

                if (order.OrderType == OrderType.Limit)
                {
                    var iterativeFills = _orderBook.MatchIteratively(order).ToList();
                    if (iterativeFills.Count > 0)
                    {
                        fills.AddRange(iterativeFills);
                    }
                    else
                    {
                        _orderBook.AddOrder(order);
                    }
                }
                else if (order.OrderType == OrderType.Market)
                {
                    var matchResult = _matchingEngine.TryMatch(order, marketPrice);
                    if (!matchResult.IsMatched)
                    {
                        _logger.LogWarning("Market order {OrderId} rejected: {Reason}", orderId, matchResult.Reason);
                        return Rejected(orderId, matchResult.Reason);
                    }

                    fills.Add((marketPrice, order.Quantity));
                }

                if (fills.Count == 0)
                {
                    return new OrderResponse
                    {
                        OrderId = orderId,
                        Status = OrderStatus.Pending,
                        Message = "Order added to book"
                    };
                }

                try
                {
                    decimal totalFilledQty = 0;
                    decimal weightedAvgPrice = 0;
                    decimal cashBefore = _portfolioManager.CashBalance;

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

                    var finalPrice = weightedAvgPrice / totalFilledQty;
                    decimal cashAfter = _portfolioManager.CashBalance;

                    _logger.LogInformation(
                        "Order {OrderId} executed successfully. Total Qty: {Qty} @ Avg Price: ${Price:N2}",
                        orderId, totalFilledQty, finalPrice);

                    persistenceData = ((int)totalFilledQty, finalPrice, cashBefore, cashAfter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute order {OrderId}", orderId);
                    return Rejected(orderId, $"Execution failed: {ex.Message}");
                }
            }

            if (persistenceData is null)
            {
                return Rejected(orderId, "Execution failed: trade persistence data was not created");
            }

            try
            {
                await (_persistenceService.OnTradeExecutedAsync(
                    orderId,
                    clientOrderId,
                    order.Symbol,
                    persistenceData.Value.Quantity,
                    persistenceData.Value.Price,
                    order.Side,
                    persistenceData.Value.CashBefore,
                    persistenceData.Value.CashAfter,
                    quantResult.Greeks) ?? Task.CompletedTask);

                return new OrderResponse
                {
                    OrderId = orderId,
                    Status = OrderStatus.Executed,
                    ExecutedPrice = persistenceData.Value.Price,
                    ExecutedQuantity = persistenceData.Value.Quantity,
                    Message = $"Order executed successfully at avg price ${persistenceData.Value.Price:N2}"
                };
            }
            catch (Exception ex) when (IsDuplicateClientOrderException(ex))
            {
                _logger.LogWarning(ex, "Duplicate client order {ClientOrderId} rejected", clientOrderId);
                return Rejected(orderId, "Duplicate order: this order was already processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist order {OrderId}", orderId);
                return Rejected(orderId, $"Execution failed: {ex.Message}");
            }
        }
        finally
        {
            if (clientOrderId is not null && clientOrderLock is not null)
            {
                ReleaseClientOrderLock(clientOrderId, clientOrderLock, clientOrderLockAcquired);
            }
        }
    }

    private async Task<(bool Success, string ErrorMessage, Greeks Greeks)> PerformQuantCheck(
        OrderRequest order,
        decimal marketPrice)
    {
        try
        {
            var request = new PdeRequest(
                Spot: (double)marketPrice,
                Strike: (double)marketPrice,
                Maturity: 0.25,
                Rate: 0.05,
                Volatility: 0.2,
                OptionType: order.Side == OrderSide.Buy ? "call" : "put");

            var response = await _pdeModel.GetFairValueAsync(request);
            if (!response.Success)
            {
                return (false, response.ErrorMessage, new Greeks(0, 0, 0, 0, 0));
            }

            return (true, string.Empty, response.Greeks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quant check failed. Defaulting to allow for system availability.");
            return (true, string.Empty, new Greeks(0, 0, 0, 0, 0));
        }
    }

    private static OrderResponse Rejected(string orderId, string? message) =>
        new()
        {
            OrderId = orderId,
            Status = OrderStatus.Rejected,
            Message = message
        };

    private static bool IsDuplicateClientOrderException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            if (current is DbUpdateException && current.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                return true;
            }
        }

        return false;
    }

    private ClientOrderLock AcquireClientOrderLock(string clientOrderId)
    {
        lock (_clientOrderLocksGate)
        {
            if (!_clientOrderLocks.TryGetValue(clientOrderId, out var clientOrderLock))
            {
                clientOrderLock = new ClientOrderLock();
                _clientOrderLocks.TryAdd(clientOrderId, clientOrderLock);
            }

            clientOrderLock.RefCount++;
            return clientOrderLock;
        }
    }

    private void ReleaseClientOrderLock(
        string clientOrderId,
        ClientOrderLock clientOrderLock,
        bool semaphoreAcquired)
    {
        lock (_clientOrderLocksGate)
        {
            if (semaphoreAcquired)
            {
                clientOrderLock.Release();
            }

            clientOrderLock.RefCount--;
            if (clientOrderLock.RefCount == 0)
            {
                _clientOrderLocks.TryRemove(
                    new KeyValuePair<string, ClientOrderLock>(clientOrderId, clientOrderLock));
                clientOrderLock.Dispose();
            }
        }
    }

    private sealed class ClientOrderLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public int RefCount { get; set; }

        public Task WaitAsync() => _semaphore.WaitAsync();

        public void Release() => _semaphore.Release();

        public void Dispose() => _semaphore.Dispose();
    }

    private string GenerateOrderId() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _orderCounter):D6}";
}
