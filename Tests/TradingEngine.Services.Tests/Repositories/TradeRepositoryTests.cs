using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingEngine.Data;
using TradingEngine.Data.Models;
using TradingEngine.Data.Repositories;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace TradingEngine.Services.Tests.Repositories;

public class TradeRepositoryTests : IAsyncLifetime
{
    private TradingDbContext _context = null!;
    private TradeRepository _repository = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TradingDbContext(options);
        var mockLogger = new Mock<ILogger<TradeRepository>>();
        _repository = new TradeRepository(_context, mockLogger.Object);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveTradeAsync_ValidTrade_ReturnIdAndPersists()
    {
        // Arrange
        var trade = new TradeEntity
        {
            OrderId = "ORD-001",
            Symbol = "AAPL",
            Quantity = 10,
            ExecutionPrice = 150,
            Side = "Buy",
            ExecutedAt = DateTime.UtcNow,
            CashBeforeTransaction = 2000,
            CashAfterTransaction = 1500
        };

        // Act
        var id = await _repository.SaveTradeAsync(trade);

        // Assert
        id.Should().BeGreaterThan(0);
        var saved = await _repository.GetTradeByIdAsync(id);
        saved.Should().NotBeNull();
        saved!.OrderId.Should().Be("ORD-001");
    }

    [Fact]
    public async Task GetTradesAsync_WithDateRange_ReturnsTrades()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);
        var trade = new TradeEntity
        {
            OrderId = "ORD-002",
            Symbol = "GOOGL",
            Quantity = 5,
            ExecutionPrice = 2800,
            Side = "Buy",
            ExecutedAt = DateTime.UtcNow,
            CashBeforeTransaction = 15000,
            CashAfterTransaction = 1000
        };
        await _repository.SaveTradeAsync(trade);

        // Act
        var trades = await _repository.GetTradesAsync(from, to);

        // Assert
        trades.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTradesBySymbolAsync_FiltersBySymbol()
    {
        // Arrange
        var trade1 = new TradeEntity { OrderId = "O1", Symbol = "AAPL", Quantity = 1, ExecutionPrice = 100, Side = "Buy", ExecutedAt = DateTime.UtcNow };
        var trade2 = new TradeEntity { OrderId = "O2", Symbol = "GOOGL", Quantity = 1, ExecutionPrice = 200, Side = "Buy", ExecutedAt = DateTime.UtcNow };
        await _repository.SaveTradeAsync(trade1);
        await _repository.SaveTradeAsync(trade2);

        // Act
        var aapl = await _repository.GetTradesBySymbolAsync("AAPL");

        // Assert
        aapl.Should().HaveCount(1);
        aapl.First().OrderId.Should().Be("O1");
    }

    [Fact]
    public async Task CalculateTotalVolumeAsync_CalculatesCorrectly()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);
        var trade = new TradeEntity
        {
            OrderId = "O1",
            Symbol = "AAPL",
            Quantity = 10,
            ExecutionPrice = 150,
            Side = "Buy",
            ExecutedAt = DateTime.UtcNow
        };
        await _repository.SaveTradeAsync(trade);

        // Act
        var volume = await _repository.CalculateTotalVolumeAsync(from, to);

        // Assert
        volume.Should().Be(1500); // 10 * 150
    }
}
