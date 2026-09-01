using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class RiskManagementService : IRiskManagementService
{
    private readonly ILogger<RiskManagementService> _logger;
    private readonly IPortfolioManager _portfolioManager;
    private readonly TradingServerConfig _config;

    // ponytail: simple constants; move to config if dynamic tuning is needed
    private const decimal MaxOrderValue = 1_000_000m;
    private const decimal MaxPositionValue = 10_000_000m;
    private const decimal MaxPortfolioExposure = 50_000_000m;

    public RiskManagementService(
        ILogger<RiskManagementService> logger,
        IPortfolioManager portfolioManager,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _portfolioManager = portfolioManager;
        _config = config.Value;
    }

    public (bool IsValid, string ErrorMessage) ValidateOrder(OrderRequest order, decimal marketPrice)
    {
        var orderValue = order.Quantity * marketPrice;

        // 1. Max Order Size
        if (orderValue > MaxOrderValue)
        {
            return (false, $"Order value ${orderValue:N2} exceeds maximum allowed order size ${MaxOrderValue:N2}");
        }

        if (order.Side == OrderSide.Buy)
        {
            // 2. Cash validation
            if (!_portfolioManager.HasSufficientCash(orderValue))
            {
                return (false, $"Insufficient cash. Required: ${orderValue:N2}, Available: ${_portfolioManager.GetBuyingPower():N2}");
            }
        }
        else
        {
            // 3. Position validation
            if (!_portfolioManager.HasSufficientShares(order.Symbol, order.Quantity))
            {
                return (false, $"Insufficient shares of {order.Symbol} to sell {order.Quantity} shares.");
            }
        }

        // 4. Max Position Size
        var currentPositionValue = GetCurrentPositionValue(order.Symbol, marketPrice);
        if (currentPositionValue + orderValue > MaxPositionValue)
        {
            return (false, $"Order would exceed maximum position size for {order.Symbol} (${MaxPositionValue:N2})");
        }

        // 5. Max Portfolio Exposure
        if (GetTotalPortfolioValue(marketPrice) + orderValue > MaxPortfolioExposure)
        {
            return (false, $"Order would exceed maximum total portfolio exposure (${MaxPortfolioExposure:N2})");
        }

        return (true, string.Empty);
    }

    public decimal GetBuyingPower() => _portfolioManager.GetBuyingPower();

    private decimal GetCurrentPositionValue(string symbol, decimal marketPrice)
    {
        return _portfolioManager.Positions.TryGetValue(symbol, out var pos)
            ? pos.Quantity * marketPrice
            : 0;
    }

    private decimal GetTotalPortfolioValue(decimal currentMarketPrice)
    {
        // ponytail: simplified; uses a single market price for all assets for exposure check
        // In production, this would sum (position.Quantity * currentPrice[symbol])
        return _portfolioManager.Positions.Values.Sum(p => p.Quantity * currentMarketPrice);
    }
}
