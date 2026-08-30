using TradingEngine.DTOs;

namespace TradingEngine.Services;

public interface IOrderHandler
{
    OrderResponse ProcessOrder(OrderRequest order);
}
