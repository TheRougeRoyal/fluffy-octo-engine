using System.Net.WebSockets;
using System.Text;
using FirebaseAdmin.Auth;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingEngine.DTOs;
using TradingEngine.Models;

namespace TradingEngine.Services;

public class WebSocketOrderHandler
{
    private readonly ILogger<WebSocketOrderHandler> _logger;
    private readonly IOrderHandler _orderHandler;
    private readonly IMarketDataManager _marketDataManager;
    private readonly TradingServerConfig _config;
    private readonly IFirebaseAuthenticationService? _firebaseAuthentication;

    public WebSocketOrderHandler(
        ILogger<WebSocketOrderHandler> logger,
        IOrderHandler orderHandler,
        IMarketDataManager marketDataManager,
        IOptions<TradingServerConfig> config)
    {
        _logger = logger;
        _orderHandler = orderHandler;
        _marketDataManager = marketDataManager;
        _config = config.Value;
    }

    public WebSocketOrderHandler(
        ILogger<WebSocketOrderHandler> logger,
        IOrderHandler orderHandler,
        IMarketDataManager marketDataManager,
        IOptions<TradingServerConfig> config,
        IEnumerable<IFirebaseAuthenticationService> firebaseAuthentication)
        : this(logger, orderHandler, marketDataManager, config)
    {
        _firebaseAuthentication = firebaseAuthentication.FirstOrDefault();
        _logger.LogInformation(
            "Firebase WebSocket authentication is {Status}.",
            _firebaseAuthentication is null ? "disabled" : "enabled");
        if (_firebaseAuthentication is null)
        {
            _logger.LogWarning("Firebase authentication service is unavailable; legacy API-key authentication is active.");
        }
    }

    public async Task HandleAsync(WebSocket webSocket, IServiceProvider services)
    {
        var clientEndpoint = "WebSocket client";
        _logger.LogInformation("Client connected via WebSocket: {Endpoint}", clientEndpoint);

        try
        {
            // 1. Send welcome message
            await SendTextAsync(webSocket, "Trading Server Ready. Please send your Firebase ID token first.");

            // 2. Simple Auth Check
            string credential = await ReceiveTextAsync(webSocket);
            var clientId = await AuthenticateAsync(credential);
            if (clientId is null)
            {
                await SendTextAsync(webSocket, "Authentication failed. Connection closing.");
                return;
            }
            await SendTextAsync(webSocket, "Authenticated. You can now send JSON order requests.");

            // 3. Main loop
            while (webSocket.State == WebSocketState.Open)
            {
                string line = await ReceiveTextAsync(webSocket);
                if (string.IsNullOrWhiteSpace(line)) continue;

                _logger.LogInformation("Received from {Endpoint}: {Data}", clientEndpoint, line);

                try
                {
                    var orderRequest = JsonConvert.DeserializeObject<OrderRequest>(line);
                    if (orderRequest == null)
                    {
                        await SendResponse(webSocket, "ERROR", OrderStatus.Rejected, "Invalid JSON format");
                        continue;
                    }

                    orderRequest.ClientId = clientId;
                    var response = _orderHandler.ProcessOrder(orderRequest);
                    await SendTextAsync(webSocket, JsonConvert.SerializeObject(response));
                    _logger.LogInformation("Response sent to {Endpoint}", clientEndpoint);
                }
                catch (JsonException ex)
                {
                    await SendResponse(webSocket, "ERROR", OrderStatus.Rejected, $"JSON parsing error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    await SendResponse(webSocket, "ERROR", OrderStatus.Rejected, $"Processing error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket client {Endpoint}", clientEndpoint);
        }
        finally
        {
            _logger.LogInformation("Client disconnected: {Endpoint}", clientEndpoint);
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    _logger.LogDebug("WebSocket was already closed by the client.");
                }
            }
        }
    }

    private async Task<string?> AuthenticateAsync(string credential)
    {
        if (_firebaseAuthentication is not null)
        {
            try
            {
                var token = await _firebaseAuthentication.VerifyIdTokenAsync(credential);
                return token.Uid;
            }
            catch (FirebaseAuthException ex)
            {
                _logger.LogWarning(
                    "Firebase WebSocket authentication failed: {ErrorCode} - {Message}",
                    ex.ErrorCode, ex.Message);
                return null;
            }
        }

        return credential == "SECRET_API_KEY" ? "legacy-client" : null;
    }

    private async Task SendTextAsync(WebSocket socket, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task<string> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[1024 * 4];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private async Task SendResponse(WebSocket socket, string id, OrderStatus status, string message)
    {
        var response = new OrderResponse { OrderId = id, Status = status, Message = message };
        await SendTextAsync(socket, JsonConvert.SerializeObject(response));
    }
}
