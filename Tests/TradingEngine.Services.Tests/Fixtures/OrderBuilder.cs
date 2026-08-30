using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services.Tests.Fixtures;

public class OrderBuilder
{
    private string _symbol = "AAPL";
    private int _quantity = 10;
    private decimal _price = 150m;
    private OrderSide _side = OrderSide.Buy;

    public OrderBuilder WithSymbol(string symbol)
    {
        _symbol = symbol;
        return this;
    }

    public OrderBuilder WithQuantity(int quantity)
    {
        _quantity = quantity;
        return this;
    }

    public OrderBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public OrderBuilder AsBuy()
    {
        _side = OrderSide.Buy;
        return this;
    }

    public OrderBuilder AsSell()
    {
        _side = OrderSide.Sell;
        return this;
    }

    public OrderRequest Build() => new OrderRequest
    {
        Symbol = _symbol,
        Quantity = _quantity,
        Price = _price,
        Side = _side
    };
}
