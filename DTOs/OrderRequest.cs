using TradingEngine.Models;

namespace TradingEngine.DTOs;

public class OrderRequest
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OrderSide Side { get; set; }
    public OrderType OrderType { get; set; } = OrderType.Limit;
    public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
