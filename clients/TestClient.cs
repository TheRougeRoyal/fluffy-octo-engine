using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine;

/// <summary>
/// Test client to send sample orders to the Trading Server
/// Run this in a separate terminal after starting the server
/// </summary>
class TestClient
{
    private const string ServerHost = "127.0.0.1";
    private const int ServerPort = 5000;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=================================");
        Console.WriteLine("Trading Engine Test Client");
        Console.WriteLine("=================================");
        Console.WriteLine();

        try
        {
            using var client = new TcpClient();
            Console.WriteLine($"Connecting to server at {ServerHost}:{ServerPort}...");
            await client.ConnectAsync(ServerHost, ServerPort);
            Console.WriteLine("Connected!");
            Console.WriteLine();

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Read welcome message
            var welcome = await reader.ReadLineAsync();
            Console.WriteLine($"Server: {welcome}");
            Console.WriteLine();

            // Test Case 1: Valid Buy Order
            Console.WriteLine("TEST 1: Buy 10 shares of AAPL at $180");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "AAPL",
                Quantity = 10,
                Price = 180.00m,
                Side = OrderSide.Buy
            });

            // Test Case 2: Another Valid Buy Order
            Console.WriteLine("\nTEST 2: Buy 5 shares of GOOGL at $150");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "GOOGL",
                Quantity = 5,
                Price = 150.00m,
                Side = OrderSide.Buy
            });

            // Test Case 3: Buy with insufficient price
            Console.WriteLine("\nTEST 3: Buy 10 shares of MSFT at $100 (below market - should reject)");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "MSFT",
                Quantity = 10,
                Price = 100.00m,
                Side = OrderSide.Buy
            });

            // Test Case 4: Valid Buy Order for TSLA
            Console.WriteLine("\nTEST 4: Buy 20 shares of TSLA at $250");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "TSLA",
                Quantity = 20,
                Price = 250.00m,
                Side = OrderSide.Buy
            });

            // Test Case 5: Valid Sell Order
            Console.WriteLine("\nTEST 5: Sell 5 shares of AAPL at $175");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "AAPL",
                Quantity = 5,
                Price = 175.00m,
                Side = OrderSide.Sell
            });

            // Test Case 6: Sell more than owned (should reject)
            Console.WriteLine("\nTEST 6: Sell 100 shares of GOOGL (insufficient shares - should reject)");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "GOOGL",
                Quantity = 100,
                Price = 140.00m,
                Side = OrderSide.Sell
            });

            // Test Case 7: Invalid symbol
            Console.WriteLine("\nTEST 7: Buy 10 shares of INVALID (invalid symbol - should reject)");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "INVALID",
                Quantity = 10,
                Price = 100.00m,
                Side = OrderSide.Buy
            });

            // Test Case 8: Zero quantity
            Console.WriteLine("\nTEST 8: Buy 0 shares of AAPL (invalid quantity - should reject)");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "AAPL",
                Quantity = 0,
                Price = 180.00m,
                Side = OrderSide.Buy
            });

            // Test Case 9: Negative price
            Console.WriteLine("\nTEST 9: Buy 10 shares of AAPL at -$50 (invalid price - should reject)");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "AAPL",
                Quantity = 10,
                Price = -50.00m,
                Side = OrderSide.Buy
            });

            // Test Case 10: Valid Sell of remaining TSLA
            Console.WriteLine("\nTEST 10: Sell 10 shares of TSLA at $245");
            await SendOrder(writer, reader, new OrderRequest
            {
                Symbol = "TSLA",
                Quantity = 10,
                Price = 245.00m,
                Side = OrderSide.Sell
            });

            Console.WriteLine("\n=================================");
            Console.WriteLine("All test cases completed!");
            Console.WriteLine("=================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Make sure the Trading Server is running!");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task SendOrder(StreamWriter writer, StreamReader reader, OrderRequest order)
    {
        try
        {
            // Serialize and send order
            var orderJson = JsonConvert.SerializeObject(order);
            Console.WriteLine($"Sending: {orderJson}");
            await writer.WriteLineAsync(orderJson);

            // Read response
            var responseJson = await reader.ReadLineAsync();
            if (responseJson != null)
            {
                var response = JsonConvert.DeserializeObject<OrderResponse>(responseJson);
                if (response != null)
                {
                    Console.WriteLine($"Response:");
                    Console.WriteLine($"  Order ID: {response.OrderId}");
                    Console.WriteLine($"  Status: {response.Status}");
                    Console.WriteLine($"  Executed Price: ${response.ExecutedPrice:N2}");
                    Console.WriteLine($"  Executed Quantity: {response.ExecutedQuantity}");
                    Console.WriteLine($"  Message: {response.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending order: {ex.Message}");
        }
    }
}
