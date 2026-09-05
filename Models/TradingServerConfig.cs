namespace TradingEngine.Models;

public class TradingServerConfig
{
    public int Port { get; set; }
    public decimal InitialCashBalance { get; set; }
    public List<string> TradeableSymbols { get; set; } = new();
    public string PdeBinaryPath { get; set; } = "QuantCore/bin/pricing_api";
    public string FirebaseProjectId { get; set; } = string.Empty;
    public string FirebaseServiceAccountJson { get; set; } = string.Empty;
    // API key used as fallback credential when Firebase auth is not configured.
    // Set via TradingServer__ApiKey environment variable. Empty string disables fallback auth entirely.
    public string ApiKey { get; set; } = string.Empty;

    // Risk limits — tunable per environment without redeployment
    public decimal MaxOrderValue { get; set; } = 1_000_000m;
    public decimal MaxPositionValue { get; set; } = 10_000_000m;
    public decimal MaxPortfolioExposure { get; set; } = 50_000_000m;
}
