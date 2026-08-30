using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingEngine.Data;
using TradingEngine.Data.Models;
using TradingEngine.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace TradingEngine.Services.Tests.Repositories;

public class PerformanceMetricsRepositoryTests : IAsyncLifetime
{
    private TradingDbContext _context = null!;
    private PerformanceMetricsRepository _repository = null!;
    private Mock<ITradeRepository> _mockTradeRepo = null!;
    private Mock<IPortfolioSnapshotRepository> _mockSnapshotRepo = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TradingDbContext(options);
        _mockTradeRepo = new Mock<ITradeRepository>();
        _mockSnapshotRepo = new Mock<IPortfolioSnapshotRepository>();
        var mockLogger = new Mock<ILogger<PerformanceMetricsRepository>>();

        _repository = new PerformanceMetricsRepository(_context, mockLogger.Object, _mockTradeRepo.Object, _mockSnapshotRepo.Object);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveMetricsAsync_PersistsMetrics()
    {
        // Arrange
        var metrics = new PerformanceMetricsEntity
        {
            CalculatedAt = DateTime.UtcNow,
            PeriodStart = DateTime.UtcNow.AddDays(-1),
            PeriodEnd = DateTime.UtcNow,
            TotalReturn = 0.05m
        };

        // Act
        var id = await _repository.SaveMetricsAsync(metrics);

        // Assert
        id.Should().BeGreaterThan(0);
        var saved = await _repository.GetLatestMetricsAsync();
        saved.Should().NotBeNull();
        saved!.TotalReturn.Should().Be(0.05m);
    }

    [Fact]
    public async Task CalculateMetricsAsync_CalculatesCorrectValues()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow;

        var trades = new List<TradeEntity>
        {
            new() { ExecutionPrice = 150 }, // Win
            new() { ExecutionPrice = 50 }   // Loss
        };
        _mockTradeRepo.Setup(r => r.GetTradesAsync(from, to)).ReturnsAsync(trades);

        var snapshots = new List<PortfolioSnapshotEntity>
        {
            new() { SnapshotTime = from, TotalPortfolioValue = 10000 },
            new() { SnapshotTime = to, TotalPortfolioValue = 11000 }
        };
        _mockSnapshotRepo.Setup(r => r.GetSnapshotHistoryAsync(from, to)).ReturnsAsync(snapshots);

        // Act
        var result = await _repository.CalculateMetricsAsync(from, to);

        // Assert
        result.TotalReturn.Should().Be(0.1m); // (11000-10000)/10000
        result.WinRate.Should().Be(0.5m);
    }
}
