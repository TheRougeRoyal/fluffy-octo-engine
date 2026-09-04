using System.Text.Json.Serialization;

namespace TradingEngine.Models.Quant;

public record PdeRequest(
    [property: JsonPropertyName("spot")] double Spot,
    [property: JsonPropertyName("strike")] double Strike,
    [property: JsonPropertyName("maturity")] double Maturity,
    [property: JsonPropertyName("rate")] double Rate,
    [property: JsonPropertyName("volatility")] double Volatility,
    [property: JsonPropertyName("optionType")] string OptionType,
    [property: JsonPropertyName("scheme")] string Scheme = "CN"
);
