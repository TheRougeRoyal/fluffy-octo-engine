using TradingEngine.Models;

namespace TradingEngine.Services;

public interface IRiskManagementService
{
    (bool IsValid, string ErrorMessage) ValidateOrder(OrderRequest order, decimal marketPrice);
    decimal GetBuyingPower();
}
