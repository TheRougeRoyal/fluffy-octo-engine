using System.Collections.Concurrent;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class LimitOrderBook : ILimitOrderBook
{
    // ponytail: Using SortedList for simplicity; in production a more robust LOB structure is needed.
    private readonly ConcurrentDictionary<string, OrderBook> _books = new();

    public void AddOrder(OrderRequest order)
    {
        var book = _books.GetOrAdd(order.Symbol, _ => new OrderBook());
        book.AddOrder(order);
    }

    public decimal GetBestBid(string symbol) => 
        _books.TryGetValue(symbol, out var book) ? book.BestBid : 0;

    public decimal GetBestAsk(string symbol) => 
        _books.TryGetValue(symbol, out var book) ? book.BestAsk : decimal.MaxValue;

    public bool TryMatch(OrderRequest order, out decimal fillPrice, out int fillQuantity)
    {
        fillPrice = 0;
        fillQuantity = 0;

        if (!_books.TryGetValue(order.Symbol, out var book)) return false;

        return book.TryMatch(order, out fillPrice, out fillQuantity);
    }

    private class OrderBook
    {
        private readonly object _lock = new();
        private readonly List<OrderRequest> _bids = new();
        private readonly List<OrderRequest> _asks = new();

        public decimal BestBid => _lock is { } ? GetBest(_bids, true) : 0;
        public decimal BestAsk => _lock is { } ? GetBest(_asks, false) : decimal.MaxValue;

        public void AddOrder(OrderRequest order)
        {
            lock (_lock)
            {
                if (order.Side == OrderSide.Buy)
                {
                    _bids.Add(order);
                    _bids.Sort((a, b) => b.Price.CompareTo(a.Price));
                }
                else
                {
                    _asks.Add(order);
                    _asks.Sort((a, b) => a.Price.CompareTo(b.Price));
                }
            }
        }

        public bool TryMatch(OrderRequest order, out decimal fillPrice, out int fillQuantity)
        {
            lock (_lock)
            {
                fillPrice = 0;
                fillQuantity = 0;

                if (order.Side == OrderSide.Buy)
                {
                    if (_asks.Count > 0 && _asks[0].Price <= order.Price)
                    {
                        var match = _asks[0];
                        fillPrice = match.Price;
                        fillQuantity = Math.Min(order.Quantity, match.Quantity);
                        
                        match.Quantity -= fillQuantity;
                        if (match.Quantity == 0) _asks.RemoveAt(0);
                        return true;
                    }
                }
                else
                {
                    if (_bids.Count > 0 && _bids[0].Price >= order.Price)
                    {
                        var match = _bids[0];
                        fillPrice = match.Price;
                        fillQuantity = Math.Min(order.Quantity, match.Quantity);
                        
                        match.Quantity -= fillQuantity;
                        if (match.Quantity == 0) _bids.RemoveAt(0);
                        return true;
                    }
                }
                return false;
            }
        }

        private decimal GetBest(List<OrderRequest> orders, bool max)
        {
            lock (_lock)
            {
                if (orders.Count == 0) return max ? 0 : decimal.MaxValue;
                return orders[0].Price;
            }
        }
    }
}
