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
        policy.WithOrigins("http://localhost:5173") // Vue dev server
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
        await HandleWebSocketConnection(webSocket, sessionId, channels);
    }
    finally
    {
        // Remove the client when done
        channels.ConnectedClients.Remove(webSocket);
    }
});

// Configure HTTPS
app.Urls.Add("https://localhost:5001");
app.Urls.Add("http://localhost:5000");

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
                    // Store pipe data
                    channels.PipeMessages.Enqueue(message);
                    // Broadcast pipe data to all clients in the session with "pipe:" prefix
                    await BroadcastMessage(sessionId, $"pipe:{message}");
                }
            }
            catch (JsonException)
            {
                // If not a valid JSON, treat as pipe data
                channels.PipeMessages.Enqueue(message);
                await BroadcastMessage(sessionId, $"pipe:{message}");
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

// Data structures
public class SessionChannels
{
    public ConcurrentQueue<string> PipeMessages { get; } = new();
    public ConcurrentQueue<ChatMessage> ChatMessages { get; } = new();
    public HashSet<WebSocket> ConnectedClients { get; } = new();
}

public class ChatMessage
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = string.Empty;
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
} 