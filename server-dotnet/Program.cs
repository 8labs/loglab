using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSignalR();
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
app.MapHub<LogHub>("/ws/{sessionId}");

// Configure HTTPS
app.Urls.Add("https://localhost:5001");
app.Urls.Add("http://localhost:5000");

app.Run();

// Hub for handling WebSocket connections
public class LogHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly ILogger<LogHub> _logger;
    private static readonly ConcurrentDictionary<string, SessionChannels> _sessions = new();

    public LogHub(ILogger<LogHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.GetHttpContext()?.Request.RouteValues["sessionId"]?.ToString();
        if (string.IsNullOrEmpty(sessionId))
        {
            Context.Abort();
            return;
        }

        // Create session if it doesn't exist
        var channels = _sessions.GetOrAdd(sessionId, _ => new SessionChannels());
        
        // Add connection to group
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client connected to session {SessionId}", sessionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionId = Context.GetHttpContext()?.Request.RouteValues["sessionId"]?.ToString();
        if (!string.IsNullOrEmpty(sessionId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            _logger.LogInformation("Client disconnected from session {SessionId}", sessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string message)
    {
        var sessionId = Context.GetHttpContext()?.Request.RouteValues["sessionId"]?.ToString();
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var channels))
        {
            return;
        }

        try
        {
            // Try to parse as chat message
            var chatMessage = JsonSerializer.Deserialize<ChatMessage>(message);
            if (chatMessage != null)
            {
                // Store chat message
                channels.ChatMessages.Enqueue(chatMessage);
                // Broadcast chat message to all clients in the session
                await Clients.Group(sessionId).SendAsync("ReceiveChatMessage", chatMessage);
            }
            else
            {
                // Store pipe data
                channels.PipeMessages.Enqueue(message);
                // Broadcast pipe data to all clients in the session
                _logger.LogInformation("[Session {SessionId}] Received pipe data: {Message}", sessionId, message);
                await Clients.Group(sessionId).SendAsync("ReceivePipeData", message);
            }
        }
        catch (JsonException)
        {
            // If not a valid JSON, treat as pipe data
            channels.PipeMessages.Enqueue(message);
            _logger.LogInformation("[Session {SessionId}] Received pipe data: {Message}", sessionId, message);
            await Clients.Group(sessionId).SendAsync("ReceivePipeData", message);
        }
    }
}

// Data structures
public class SessionChannels
{
    public ConcurrentQueue<string> PipeMessages { get; } = new();
    public ConcurrentQueue<ChatMessage> ChatMessages { get; } = new();
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