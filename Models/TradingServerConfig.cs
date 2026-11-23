namespace TradingEngine.Models;

public class TradingServerConfig
{
    public int Port { get; set; }
    public decimal InitialCashBalance { get; set; }
    public List<string> TradeableSymbols { get; set; } = new();
    public string PdeBinaryPath { get; set; } = "QuantCore/bin/pricing_api";
    public string FirebaseProjectId { get; set; } = string.Empty;
    public string FirebaseServiceAccountJson { get; set; } = string.Empty;
    // Retained for backward-compatible configuration only. WebSocket authentication
    // never falls back to this value when Firebase is unavailable.
    public string ApiKey { get; set; } = string.Empty;

    // Risk limits — tunable per environment without redeployment
    public decimal MaxOrderValue { get; set; } = 1_000_000m;
    public decimal MaxPositionValue { get; set; } = 10_000_000m;
    public decimal MaxPortfolioExposure { get; set; } = 50_000_000m;
}
