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

public class PortfolioSnapshotRepositoryTests : IAsyncLifetime
{
    private TradingDbContext _context = null!;
    private PortfolioSnapshotRepository _repository = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TradingDbContext(options);
        var mockLogger = new Mock<ILogger<PortfolioSnapshotRepository>>();
        _repository = new PortfolioSnapshotRepository(_context, mockLogger.Object);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveSnapshotAsync_PersistsSnapshot()
    {
        // Arrange
        var snapshot = new PortfolioSnapshotEntity
        {
            SnapshotTime = DateTime.UtcNow,
            CashBalance = 10000,
            TotalPortfolioValue = 15000,
            Positions = new List<PositionSnapshotEntity>
            {
                new() { Symbol = "AAPL", Quantity = 10, AverageCost = 140, CurrentPrice = 150 }
            }
        };

        // Act
        var id = await _repository.SaveSnapshotAsync(snapshot);

        // Assert
        id.Should().BeGreaterThan(0);
        var saved = await _repository.GetLatestSnapshotAsync();
        saved.Should().NotBeNull();
        saved!.TotalPortfolioValue.Should().Be(15000);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_ReturnsMostRecent()
    {
        // Arrange
        var s1 = new PortfolioSnapshotEntity { SnapshotTime = DateTime.UtcNow.AddDays(-1), TotalPortfolioValue = 10000 };
        var s2 = new PortfolioSnapshotEntity { SnapshotTime = DateTime.UtcNow, TotalPortfolioValue = 11000 };
        await _repository.SaveSnapshotAsync(s1);
        await _repository.SaveSnapshotAsync(s2);

        // Act
        var latest = await _repository.GetLatestSnapshotAsync();

        // Assert
        latest!.TotalPortfolioValue.Should().Be(11000);
    }

    [Fact]
    public async Task GetPortfolioValueAtDateAsync_ReturnsCorrectValue()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var s1 = new PortfolioSnapshotEntity { SnapshotTime = date.AddDays(-2), TotalPortfolioValue = 10000 };
        var s2 = new PortfolioSnapshotEntity { SnapshotTime = date.AddDays(-1), TotalPortfolioValue = 11000 };
        await _repository.SaveSnapshotAsync(s1);
        await _repository.SaveSnapshotAsync(s2);

        // Act
        var value = await _repository.GetPortfolioValueAtDateAsync(date);

        // Assert
        value.Should().Be(11000);
    }
}
