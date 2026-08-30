using TradingEngine.DTOs;

namespace TradingEngine.Services;

/// <summary>
/// Responsible for validating order parameters.
/// Follows Single Responsibility Principle - validation only.
/// </summary>
public interface IOrderValidator
{
    /// <summary>
    /// Validates an order request.
    /// </summary>
    /// <param name="order">The order to validate</param>
    /// <returns>Tuple of (IsValid, ErrorMessage)</returns>
    (bool IsValid, string? ErrorMessage) Validate(OrderRequest order);
}
