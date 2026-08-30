namespace TradingEngine.Models;

public class TradingServerConfig
{
    public int Port { get; set; }
    public decimal InitialCashBalance { get; set; }
    public List<string> TradeableSymbols { get; set; } = new();
}
