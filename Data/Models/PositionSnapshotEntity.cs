namespace TradingEngine.Data.Models;

public class PositionSnapshotEntity
{
    public int Id { get; set; }
    public int PortfolioSnapshotId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
}
