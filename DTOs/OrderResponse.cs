using TradingEngine.Models;

namespace TradingEngine.DTOs;

public class OrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal ExecutedPrice { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ExecutedQuantity { get; set; }
}
