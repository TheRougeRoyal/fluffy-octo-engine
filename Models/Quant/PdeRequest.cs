namespace TradingEngine.Models.Quant;

public record PdeRequest(
    double Spot,
    double Strike,
    double Maturity,
    double Rate,
    double Volatility,
    string OptionType,
    string Scheme = "CN"
);
