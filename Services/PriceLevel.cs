using System.Collections.Generic;
using System.Linq;
using TradingEngine.DTOs;

namespace TradingEngine.Services;

internal class PriceLevel
{
    public decimal Price { get; }
    public List<OrderRequest> Orders { get; } = new();

    public PriceLevel(decimal price)
    {
        Price = price;
    }

    public void AddOrder(OrderRequest order) => Orders.Add(order);
    public OrderRequest? Peek() => Orders.Count > 0 ? Orders[0] : null;
    public void RemoveFirst() => Orders.RemoveAt(0);
    public int Count => Orders.Count;
}
