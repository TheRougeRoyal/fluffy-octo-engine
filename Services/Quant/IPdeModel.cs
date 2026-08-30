using TradingEngine.Models.Quant;
using System.Threading.Tasks;

namespace TradingEngine.Services.Quant;

public interface IPdeModel
{
    Task<PdeResponse> GetFairValueAsync(PdeRequest request);
}
