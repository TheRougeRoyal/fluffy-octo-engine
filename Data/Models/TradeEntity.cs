using System;

namespace TradingEngine.Data.Models;

public class TradeEntity
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal ExecutionPrice { get; set; }
    public string Side { get; set; } = string.Empty; // "Buy" or "Sell"
    public DateTime ExecutedAt { get; set; }
    public decimal CashBeforeTransaction { get; set; }
    public decimal CashAfterTransaction { get; set; }

    // Quantitative Risk Metrics (The Greeks)
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
    public decimal Rho { get; set; }
}
