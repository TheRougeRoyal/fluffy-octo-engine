using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class RiskManagementService : IRiskManagementService
{
    private readonly ILogger<RiskManagementService> _logger;
    private readonly IPortfolioManager _portfolioManager;
    private readonly IMarketDataManager _marketDataManager;
    private readonly TradingServerConfig _config;

    public RiskManagementService(
        ILogger<RiskManagementService> logger,
        IPortfolioManager portfolioManager,
        IMarketDataManager marketDataManager,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _portfolioManager = portfolioManager;
        _marketDataManager = marketDataManager;
        _config = config.Value;
    }

    public (bool IsValid, string ErrorMessage) ValidateOrder(OrderRequest order, decimal marketPrice)
    {
        var orderValue = order.Quantity * marketPrice;

        // 1. Max Order Size
        if (orderValue > _config.MaxOrderValue)
        {
            return (false, $"Order value ${orderValue:N2} exceeds maximum allowed order size ${_config.MaxOrderValue:N2}");
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
        if (currentPositionValue + orderValue > _config.MaxPositionValue)
        {
            return (false, $"Order would exceed maximum position size for {order.Symbol} (${_config.MaxPositionValue:N2})");
        }

        // 5. Max Portfolio Exposure
        if (GetTotalPortfolioValue() + orderValue > _config.MaxPortfolioExposure)
        {
            return (false, $"Order would exceed maximum total portfolio exposure (${_config.MaxPortfolioExposure:N2})");
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

    private decimal GetTotalPortfolioValue()
    {
        // Fetch current prices for all positions and compute total portfolio value
        decimal totalValue = 0m;
        foreach (var position in _portfolioManager.Positions.Values)
        {
            try
            {
                var price = _marketDataManager.GetPrice(position.Symbol);
                totalValue += position.Quantity * price;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not fetch price for {Symbol}, excluding from portfolio exposure: {Error}", 
                    position.Symbol, ex.Message);
            }
        }
        return totalValue;
    }
}
