using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class TradingServerService : BackgroundService
{
    private readonly ILogger<TradingServerService> _logger;
    private readonly IOrderHandler _orderHandler;
    private readonly IMarketDataManager _marketDataManager;
    private readonly TradingServerConfig _config;
    private TcpListener? _listener;

    public TradingServerService(
        ILogger<TradingServerService> logger,
        IOrderHandler orderHandler,
        IMarketDataManager marketDataManager,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _orderHandler = orderHandler;
        _marketDataManager = marketDataManager;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _config.Port);
            _listener.Start();
            
            _logger.LogInformation("====================================");
            _logger.LogInformation("Trading Server started on port {Port}", _config.Port);
            _logger.LogInformation("Waiting for client connections...");
            _logger.LogInformation("====================================");
            
            // Display market data
            DisplayMarketData();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Trading Server");
            throw;
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        _logger.LogInformation("Client connected: {Endpoint}", clientEndpoint);

        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                // Send welcome message
                await writer.WriteLineAsync("Trading Server Ready. Send JSON order requests.");
                
                string? line;
                while (!cancellationToken.IsCancellationRequested && 
                       (line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    _logger.LogInformation("Received from {Endpoint}: {Data}", clientEndpoint, line);

                    try
                    {
                        // Parse the order request
                        var orderRequest = JsonConvert.DeserializeObject<OrderRequest>(line);
                        
                        if (orderRequest == null)
                        {
                            var errorResponse = new OrderResponse
                            {
                                OrderId = "ERROR",
                                Status = OrderStatus.Rejected,
                                Message = "Invalid JSON format"
                            };
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(errorResponse));
                            continue;
                        }

                        // Process the order
                        var response = _orderHandler.ProcessOrder(orderRequest);
                        
                        // Send response back to client
                        var responseJson = JsonConvert.SerializeObject(response);
                        await writer.WriteLineAsync(responseJson);
                        
                        _logger.LogInformation("Response sent to {Endpoint}: {Response}", clientEndpoint, responseJson);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON parsing error");
                        var errorResponse = new OrderResponse
                        {
                            OrderId = "ERROR",
                            Status = OrderStatus.Rejected,
                            Message = $"JSON parsing error: {ex.Message}"
                        };
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(errorResponse));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order");
                        var errorResponse = new OrderResponse
                        {
                            OrderId = "ERROR",
                            Status = OrderStatus.Rejected,
                            Message = $"Processing error: {ex.Message}"
                        };
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(errorResponse));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {Endpoint}", clientEndpoint);
        }
        finally
        {
            _logger.LogInformation("Client disconnected: {Endpoint}", clientEndpoint);
        }
    }

    private void DisplayMarketData()
    {
        _logger.LogInformation("===== MARKET DATA =====");
        var prices = _marketDataManager.GetAllPrices();
        foreach (var kvp in prices.OrderBy(p => p.Key))
        {
            _logger.LogInformation("{Symbol}: ${Price:N2}", kvp.Key, kvp.Value);
        }
        _logger.LogInformation("=======================");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trading Server stopping...");
        _listener?.Stop();
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Trading Server stopped");
    }
}
