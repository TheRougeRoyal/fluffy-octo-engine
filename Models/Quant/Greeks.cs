namespace TradingEngine.Models.Quant;

public record Greeks(
    decimal Delta,
    decimal Gamma,
    decimal Theta,
    decimal Vega,
    decimal Rho
);
