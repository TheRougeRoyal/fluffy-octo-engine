using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TradingEngine.Data.Models;
using TradingEngine.Data.Repositories;
using TradingEngine.Models;
using TradingEngine.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradingEngine.Services.Tests;

public class PersistenceServiceTests
{
    private readonly Mock<ITradeRepository> _mockTradeRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _mockSnapshotRepo = new();
    private readonly Mock<IPerformanceMetricsRepository> _mockMetricsRepo = new();
    private readonly Mock<IMarketDataManager> _mockMarketData = new();
    private readonly Mock<ILogger<PersistenceService>> _mockLogger = new();
    private readonly PersistenceService _service;

    public PersistenceServiceTests()
    {
        _service = new PersistenceService(
            _mockTradeRepo.Object,
            _mockSnapshotRepo.Object,
            _mockMetricsRepo.Object,
            _mockMarketData.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task OnTradeExecutedAsync_CallsTradeRepository()
    {
        // Act
        await _service.OnTradeExecutedAsync("ORD-1", "AAPL", 10, 150, OrderSide.Buy, 2000, 1500);

        // Assert
        _mockTradeRepo.Verify(r => r.SaveTradeAsync(It.Is<TradeEntity>(t => t.OrderId == "ORD-1")), Times.Once);
    }

    [Fact]
    public async Task SavePortfolioSnapshotAsync_CalculatesTotalValueAndSaves()
    {
        // Arrange
        var positions = new Dictionary<string, Position>
        {
            { "AAPL", new Position { Quantity = 10, AverageCost = 140 } }
        };
        _mockMarketData.Setup(m => m.GetPrice("AAPL")).Returns(160);

        // Act
        await _service.SavePortfolioSnapshotAsync(1000, positions);

        // Assert
        // Total Value = 1000 (cash) + 10 * 160 = 2600
        _mockSnapshotRepo.Verify(r => r.SaveSnapshotAsync(It.Is<PortfolioSnapshotEntity>(s => s.TotalPortfolioValue == 2600)), Times.Once);
    }

    [Fact]
    public async Task CalculateAndSaveMetricsAsync_CallsMetricsRepository()
    {
        // Arrange
        _mockMetricsRepo.Setup(r => r.CalculateMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PerformanceMetricsEntity());

        // Act
        await _service.CalculateAndSaveMetricsAsync();

        // Assert
        _mockMetricsRepo.Verify(r => r.CalculateMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        _mockMetricsRepo.Verify(r => r.SaveMetricsAsync(It.IsAny<PerformanceMetricsEntity>()), Times.Once);
    }
}
