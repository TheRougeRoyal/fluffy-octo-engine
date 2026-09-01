using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class DescendingComparer : IComparer<decimal>
{
    public int Compare(decimal x, decimal y) => y.CompareTo(x);
}

public class LimitOrderBook : ILimitOrderBook
{
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

        lock (book._lock)
        {
            if (order.Side == OrderSide.Buy)
            {
                if (book._asks.Count == 0) return false;
                var bestAsk = book._asks.First();
                if (bestAsk.Key <= order.Price)
                {
                    var level = bestAsk.Value;
                    var match = level.Peek();
                    if (match != null)
                    {
                        fillPrice = match.Price;
                        fillQuantity = Math.Min(order.Quantity, match.Quantity);
                        return true;
                    }
                }
            }
            else
            {
                if (book._bids.Count == 0) return false;
                var bestBid = book._bids.First();
                if (bestBid.Key >= order.Price)
                {
                    var level = bestBid.Value;
                    var match = level.Peek();
                    if (match != null)
                    {
                        fillPrice = match.Price;
                        fillQuantity = Math.Min(order.Quantity, match.Quantity);
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public IEnumerable<(decimal Price, int Quantity)> MatchIteratively(OrderRequest order)
    {
        if (!_books.TryGetValue(order.Symbol, out var book)) yield break;

        int remainingQty = order.Quantity;

        lock (book._lock)
        {
            var matches = book.Match(order, ref remainingQty);
            foreach (var match in matches)
            {
                yield return match;
            }
        }
    }

    public void CancelOrder(string orderId, string symbol)
    {
        if (_books.TryGetValue(symbol, out var book))
        {
            lock (book._lock)
            {
                book.Cancel(orderId);
            }
        }
    }

    private class OrderBook
    {
        public readonly object _lock = new();
        public readonly SortedDictionary<decimal, PriceLevel> _bids = new(new DescendingComparer());
        public readonly SortedDictionary<decimal, PriceLevel> _asks = new();

        public decimal BestBid => GetBest(_bids);
        public decimal BestAsk => GetBest(_asks);

        public void AddOrder(OrderRequest order)
        {
            var book = order.Side == OrderSide.Buy ? _bids : _asks;
            if (!book.TryGetValue(order.Price, out var level))
            {
                level = new PriceLevel(order.Price);
                book.Add(order.Price, level);
            }
            level.AddOrder(order);
        }

        public List<(decimal Price, int Quantity)> Match(OrderRequest order, ref int remainingQty)
        {
            var results = new List<(decimal Price, int Quantity)>();
            var book = order.Side == OrderSide.Buy ? _asks : _bids;
            var isBuy = order.Side == OrderSide.Buy;

            while (remainingQty > 0 && book.Count > 0)
            {
                var bestEntry = book.First();
                decimal price = bestEntry.Key;
                var level = bestEntry.Value;

                if (isBuy ? price > order.Price : price < order.Price) break;

                while (remainingQty > 0 && level.Count > 0)
                {
                    var match = level.Peek();
                    if (match == null) break;

                    int fillQty = Math.Min(remainingQty, match.Quantity);
                    results.Add((price, fillQty));

                    remainingQty -= fillQty;
                    match.Quantity -= fillQty;

                    if (match.Quantity == 0)
                    {
                        level.RemoveFirst();
                    }

                    if (remainingQty == 0) break;
                }

                if (level.Count == 0) book.Remove(price);
            }
            return results;
        }

        public void Cancel(string orderId)
        {
            foreach (var level in _bids.Values.Concat(_asks.Values))
            {
                var order = level.Orders.FirstOrDefault(o => o.OrderId == orderId);
                if (order != null)
                {
                    level.Orders.Remove(order);
                    return;
                }
            }
        }

        private decimal GetBest(SortedDictionary<decimal, PriceLevel> book)
        {
            if (book.Count == 0) return 0;
            return book.Keys.First();
        }
    }
}
