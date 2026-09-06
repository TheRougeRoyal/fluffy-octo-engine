using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.Services;
using TradingEngine.Models;
using TradingEngine.DTOs;

namespace TradingEngine.Services.Tests;

public class RiskManagementServiceTests
{
    private readonly Mock<ILogger<RiskManagementService>> _mockLogger = new();
    private readonly Mock<IPortfolioManager> _mockPortfolio = new();
    private readonly Mock<IMarketDataManager> _mockMarketData = new();
    private readonly IOptions<TradingServerConfig> _config;

    public RiskManagementServiceTests()
    {
        _config = Options.Create(new TradingServerConfig
        {
            MaxOrderValue = 1000000m,
            MaxPositionValue = 5000000m,
            MaxPortfolioExposure = 10000000m
        });
    }

    private RiskManagementService CreateService() =>
        new RiskManagementService(_mockLogger.Object, _mockPortfolio.Object, _mockMarketData.Object, _config);

    [Fact]
    public void ValidateOrder_MarketDataError_ReturnsRejected()
    {
        // Arrange
        var service = CreateService();
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = 150, Side = OrderSide.Buy };

        _mockPortfolio.Setup(p => p.Positions).Returns(new Dictionary<string, Position>
        {
            { "MSFT", new Position { Symbol = "MSFT", Quantity = 100 } }
        });
        _mockMarketData.Setup(m => m.GetPrice("MSFT")).Throws(new Exception("API Down"));
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(150);
        _mockPortfolio.Setup(p => p.HasSufficientCash(It.IsAny<decimal>())).Returns(true);

        // Act
        var result = service.ValidateOrder(order, 150);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("market data unavailable");
    }
}
