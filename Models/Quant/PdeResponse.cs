using System.Text.Json.Serialization;

namespace TradingEngine.Models.Quant;

public record PdeResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("pdePrice")] decimal PdePrice,
    [property: JsonPropertyName("analyticPrice")] decimal AnalyticPrice,
    [property: JsonPropertyName("error")] decimal Error,
    [property: JsonPropertyName("greeks")] Greeks Greeks,
    [property: JsonPropertyName("error")] string ErrorMessage = ""
);
