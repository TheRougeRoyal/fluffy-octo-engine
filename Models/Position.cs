namespace TradingEngine.Models;

public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal TotalCost => Quantity * AverageCost;
}
