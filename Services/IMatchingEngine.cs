using TradingEngine.DTOs;

namespace TradingEngine.Services;

/// <summary>
/// Responsible for matching orders against market prices.
/// Follows Single Responsibility Principle - matching logic only.
/// </summary>
public interface IMatchingEngine
{
    /// <summary>
    /// Attempts to match an order against current market conditions.
    /// </summary>
    /// <param name="order">The order to match</param>
    /// <param name="marketPrice">Current market price for the symbol</param>
    /// <returns>Tuple of (IsMatched, Reason if not matched)</returns>
    (bool IsMatched, string Reason) TryMatch(OrderRequest order, decimal marketPrice);
}
