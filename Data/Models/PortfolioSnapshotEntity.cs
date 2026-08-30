using System;
using System.Collections.Generic;

namespace TradingEngine.Data.Models;

public class PortfolioSnapshotEntity
{
    public int Id { get; set; }
    public DateTime SnapshotTime { get; set; }
    public decimal CashBalance { get; set; }
    public decimal TotalPortfolioValue { get; set; }
    public List<PositionSnapshotEntity> Positions { get; set; } = new();
}
