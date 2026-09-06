using TradingEngine.DTOs;

namespace TradingEngine.Services;

public interface IOrderHandler
{
    Task<OrderResponse> ProcessOrderAsync(OrderRequest order);
}
