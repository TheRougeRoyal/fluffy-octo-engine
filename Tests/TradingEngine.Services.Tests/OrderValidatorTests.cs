using Xunit;
using FluentAssertions;
using Moq;
using TradingEngine.DTOs;
using TradingEngine.Models;
using TradingEngine.Services;

namespace TradingEngine.Services.Tests;

public class OrderValidatorTests
{
    private readonly Mock<IMarketDataManager> _mockMarketData;
    private readonly OrderValidator _validator;

    public OrderValidatorTests()
    {
        _mockMarketData = new Mock<IMarketDataManager>();
        _validator = new OrderValidator(_mockMarketData.Object);
    }

    [Fact]
    public void Validate_ValidOrder_ReturnsTrue()
    {
        // Arrange
        var order = new OrderRequest 
        { 
            Symbol = "AAPL", 
            Quantity = 10, 
            Price = 150, 
            Side = OrderSide.Buy 
        };
        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);

        // Act
        var result = _validator.Validate(order);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_EmptySymbol_ReturnsFalse()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "", Quantity = 10, Price = 150, Side = OrderSide.Buy };

        // Act
        var result = _validator.Validate(order);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Symbol cannot be empty");
    }

    [Fact]
    public void Validate_InvalidSymbol_ReturnsFalse()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "INVALID", Quantity = 10, Price = 150, Side = OrderSide.Buy };
        _mockMarketData.Setup(m => m.IsValidSymbol("INVALID")).Returns(false);

        // Act
        var result = _validator.Validate(order);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid symbol");
    }

    [Fact]
    public void Validate_ZeroQuantity_ReturnsFalse()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 0, Price = 150, Side = OrderSide.Buy };
        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);

        // Act
        var result = _validator.Validate(order);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public void Validate_NegativePrice_ReturnsFalse()
    {
        // Arrange
        var order = new OrderRequest { Symbol = "AAPL", Quantity = 10, Price = -50, Side = OrderSide.Buy };
        _mockMarketData.Setup(m => m.IsValidSymbol("AAPL")).Returns(true);

        // Act
        var result = _validator.Validate(order);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Price must be greater than 0");
    }
}
