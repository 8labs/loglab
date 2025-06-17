using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(["http://localhost:5173", "https://loglab.8labs.com"]) // Vue dev server
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors();

// Enable WebSocket support
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

// Store active sessions
var sessions = new ConcurrentDictionary<string, SessionChannels>();

// REST API endpoint for creating a new session
app.MapGet("/api/session", () =>
{
    var sessionId = Guid.NewGuid().ToString();
    var channels = new SessionChannels();
    sessions.TryAdd(sessionId, channels);
    return Results.Json(new { session_id = sessionId });
});

// WebSocket endpoint for streaming logs and chat
app.Map("/ws/{sessionId}", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var sessionId = context.Request.RouteValues["sessionId"]?.ToString();
    if (string.IsNullOrEmpty(sessionId))
    {
        context.Response.StatusCode = 400;
        return;
    }

    // Create session if it doesn't exist
    var channels = sessions.GetOrAdd(sessionId, _ => new SessionChannels());

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    // Add the client to the session's connected clients
    channels.ConnectedClients.Add(webSocket);
    
    try 
    {
        // Send message history to the new client
        await SendMessageHistory(webSocket, channels);
        await HandleWebSocketConnection(webSocket, sessionId, channels);
    }
    finally
    {
        // Remove the client when done
        channels.ConnectedClients.Remove(webSocket);
        
        // If this was the last client, clear the history
        if (channels.ConnectedClients.Count == 0)
        {
            channels.ClearHistory();
        }
    }
});

// Configure HTTPS
app.Urls.Add("https://localhost:6001");

app.Run();

async Task HandleWebSocketConnection(WebSocket webSocket, string sessionId, SessionChannels channels)
{
    var buffer = new byte[1024 * 4];
    var receiveResult = await webSocket.ReceiveAsync(
        new ArraySegment<byte>(buffer), CancellationToken.None);

    while (!receiveResult.CloseStatus.HasValue)
    {
        if (receiveResult.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            
            try
            {
                // Try to parse as chat message
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(message);
                if (chatMessage != null)
                {
                    // Store chat message
                    channels.ChatMessages.Enqueue(chatMessage);
                    // Broadcast chat message to all clients in the session
                    var chatJson = JsonSerializer.Serialize(chatMessage);
                    await BroadcastMessage(sessionId, chatJson);
                }
                else
                {
                    // Create and store log message
                    var logMessage = new LogMessage { Content = message };
                    channels.PipeMessages.Enqueue(logMessage);
                    // Broadcast log message to all clients in the session
                    var logJson = JsonSerializer.Serialize(logMessage);
                    await BroadcastMessage(sessionId, $"{logJson}");
                }
            }
            catch (JsonException)
            {
                // If not a valid JSON, treat as pipe data
                var logMessage = new LogMessage { Content = message };
                channels.PipeMessages.Enqueue(logMessage);
                var logJson = JsonSerializer.Serialize(logMessage);
                await BroadcastMessage(sessionId, $"{logJson}");
            }
        }

        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
    }

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}

async Task BroadcastMessage(string sessionId, string message)
{
    if (sessions.TryGetValue(sessionId, out var channels))
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        foreach (var client in channels.ConnectedClients)
        {
            if (client.State == WebSocketState.Open)
            {
                await client.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
}

async Task SendMessageHistory(WebSocket webSocket, SessionChannels channels)
{
    // Send pipe message history
    foreach (var message in channels.PipeMessages)
    {
        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes($"{messageJson}");
        await webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    // Send chat message history
    foreach (var message in channels.ChatMessages)
    {
        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        await webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }
}

// Data structures
public class SessionChannels
{
    public ConcurrentQueue<LogMessage> PipeMessages { get; } = new();
    public ConcurrentQueue<ChatMessage> ChatMessages { get; } = new();
    public HashSet<WebSocket> ConnectedClients { get; } = new();

    public void ClearHistory()
    {
        while (PipeMessages.TryDequeue(out _)) { }
        while (ChatMessages.TryDequeue(out _)) { }
    }
}

public class ChatMessage
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "msg";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("highlightInfo")]
    public HighlightInfo? HighlightInfo { get; set; }
}

public class HighlightInfo
{
    [JsonPropertyName("logMessageId")]
    public string LogMessageId { get; set; } = string.Empty;

    [JsonPropertyName("startPosition")]
    public int StartPosition { get; set; }

    [JsonPropertyName("endPosition")]
    public int EndPosition { get; set; }

    [JsonPropertyName("highlightId")]
    public string HighlightId { get; set; } = string.Empty;
}

public class LogMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
} 