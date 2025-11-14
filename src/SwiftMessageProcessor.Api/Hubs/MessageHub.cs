using Microsoft.AspNetCore.SignalR;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Api.Hubs;

/// <summary>
/// SignalR hub for real-time message processing updates
/// </summary>
public class MessageHub : Hub
{
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(ILogger<MessageHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to subscribe to specific message types
    /// </summary>
    public async Task SubscribeToMessageType(string messageType)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"MessageType_{messageType}");
        _logger.LogInformation("Client {ConnectionId} subscribed to message type: {MessageType}", 
            Context.ConnectionId, messageType);
    }

    /// <summary>
    /// Allows clients to unsubscribe from specific message types
    /// </summary>
    public async Task UnsubscribeFromMessageType(string messageType)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"MessageType_{messageType}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from message type: {MessageType}", 
            Context.ConnectionId, messageType);
    }
}

/// <summary>
/// Service for broadcasting messages through SignalR
/// </summary>
public interface IMessageHubService
{
    Task BroadcastMessageProcessedAsync(ProcessedMessage message);
    Task BroadcastMessageFailedAsync(ProcessedMessage message);
    Task BroadcastSystemStatusAsync(ProcessStatus status);
    Task BroadcastProcessingMetricsAsync(ProcessingMetrics metrics);
}

/// <summary>
/// Implementation of message hub service for broadcasting updates
/// </summary>
public class MessageHubService : IMessageHubService
{
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<MessageHubService> _logger;

    public MessageHubService(IHubContext<MessageHub> hubContext, ILogger<MessageHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastMessageProcessedAsync(ProcessedMessage message)
    {
        try
        {
            _logger.LogInformation("Broadcasting message processed: {MessageId}", message.Id);

            var notification = new
            {
                Type = "MessageProcessed",
                MessageId = message.Id,
                MessageType = message.MessageType,
                Status = message.Status.ToString(),
                ProcessedAt = message.ProcessedAt,
                Timestamp = DateTime.UtcNow
            };

            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveMessageUpdate", notification);

            // Broadcast to clients subscribed to this message type
            await _hubContext.Clients.Group($"MessageType_{message.MessageType}")
                .SendAsync("ReceiveMessageUpdate", notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message processed notification");
        }
    }

    public async Task BroadcastMessageFailedAsync(ProcessedMessage message)
    {
        try
        {
            _logger.LogInformation("Broadcasting message failed: {MessageId}", message.Id);

            var notification = new
            {
                Type = "MessageFailed",
                MessageId = message.Id,
                MessageType = message.MessageType,
                Status = message.Status.ToString(),
                ErrorDetails = message.ErrorDetails,
                ProcessedAt = message.ProcessedAt,
                Timestamp = DateTime.UtcNow
            };

            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveMessageUpdate", notification);

            // Broadcast to clients subscribed to this message type
            await _hubContext.Clients.Group($"MessageType_{message.MessageType}")
                .SendAsync("ReceiveMessageUpdate", notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message failed notification");
        }
    }

    public async Task BroadcastSystemStatusAsync(ProcessStatus status)
    {
        try
        {
            _logger.LogDebug("Broadcasting system status update");

            var notification = new
            {
                Type = "SystemStatus",
                IsRunning = status.IsRunning,
                IsProcessing = status.IsProcessing,
                MessagesProcessed = status.MessagesProcessed,
                MessagesFailed = status.MessagesFailed,
                MessagesPending = status.MessagesPending,
                LastProcessedAt = status.LastProcessedAt,
                Status = status.Status,
                TestModeEnabled = status.TestModeEnabled,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("ReceiveSystemStatus", notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system status");
        }
    }

    public async Task BroadcastProcessingMetricsAsync(ProcessingMetrics metrics)
    {
        try
        {
            _logger.LogDebug("Broadcasting processing metrics");

            var notification = new
            {
                Type = "ProcessingMetrics",
                TotalProcessed = metrics.TotalMessagesProcessed,
                TotalFailed = metrics.TotalMessagesFailed,
                AverageProcessingTimeMs = metrics.AverageProcessingTimeMs,
                MessagesPerMinute = metrics.MessagesPerMinute,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("ReceiveMetrics", notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast processing metrics");
        }
    }
}
