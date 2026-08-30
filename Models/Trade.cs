namespace TradingEngine.Models;

public class Trade
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OrderSide Side { get; set; }
    public DateTime Timestamp { get; set; }
}
