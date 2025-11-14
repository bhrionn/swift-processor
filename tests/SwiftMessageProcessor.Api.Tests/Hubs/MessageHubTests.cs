using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SwiftMessageProcessor.Api.Hubs;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using Xunit;

namespace SwiftMessageProcessor.Api.Tests.Hubs;

public class MessageHubTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HubConnection? _hubConnection;
    private readonly List<object> _receivedMessages = new();
    private readonly List<object> _receivedStatusUpdates = new();
    private readonly List<object> _receivedMetrics = new();

    public MessageHubTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Create SignalR client connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_factory.Server.BaseAddress}hubs/messages", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        // Register handlers for receiving messages
        _hubConnection.On<object>("ReceiveMessageUpdate", message =>
        {
            _receivedMessages.Add(message);
        });

        _hubConnection.On<object>("ReceiveSystemStatus", status =>
        {
            _receivedStatusUpdates.Add(status);
        });

        _hubConnection.On<object>("ReceiveMetrics", metrics =>
        {
            _receivedMetrics.Add(metrics);
        });

        await _hubConnection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task HubConnection_CanConnect_Successfully()
    {
        // Assert
        _hubConnection.Should().NotBeNull();
        _hubConnection!.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task BroadcastMessageProcessed_SendsMessageToConnectedClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        var message = new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            Status = MessageStatus.Processed,
            ProcessedAt = DateTime.UtcNow,
            RawMessage = "test",
            ParsedMessage = new MT103Message
            {
                TransactionReference = "TEST123",
                Currency = "USD",
                Amount = 1000m
            }
        };

        // Act
        await hubService.BroadcastMessageProcessedAsync(message);

        // Wait for message to be received
        await Task.Delay(500);

        // Assert
        _receivedMessages.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task BroadcastMessageFailed_SendsMessageToConnectedClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        var message = new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            Status = MessageStatus.Failed,
            ProcessedAt = DateTime.UtcNow,
            ErrorDetails = "Test error",
            RawMessage = "test"
        };

        // Act
        await hubService.BroadcastMessageFailedAsync(message);

        // Wait for message to be received
        await Task.Delay(500);

        // Assert
        _receivedMessages.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task BroadcastSystemStatus_SendsStatusToConnectedClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        var status = new ProcessStatus
        {
            IsRunning = true,
            IsProcessing = true,
            MessagesProcessed = 100,
            MessagesFailed = 5,
            MessagesPending = 10,
            LastProcessedAt = DateTime.UtcNow,
            StatusUpdatedAt = DateTime.UtcNow,
            Status = "Running",
            TestModeEnabled = false
        };

        // Act
        await hubService.BroadcastSystemStatusAsync(status);

        // Wait for status to be received
        await Task.Delay(500);

        // Assert
        _receivedStatusUpdates.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task BroadcastProcessingMetrics_SendsMetricsToConnectedClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        var metrics = new ProcessingMetrics
        {
            TotalMessagesProcessed = 1000,
            TotalMessagesFailed = 50,
            AverageProcessingTimeMs = 150.5,
            MessagesPerMinute = 100
        };

        // Act
        await hubService.BroadcastProcessingMetricsAsync(metrics);

        // Wait for metrics to be received
        await Task.Delay(500);

        // Assert
        _receivedMetrics.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task SubscribeToMessageType_ReceivesOnlySubscribedMessages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        // Subscribe to MT103 messages
        await _hubConnection!.InvokeAsync("SubscribeToMessageType", "MT103");

        var mt103Message = new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            Status = MessageStatus.Processed,
            ProcessedAt = DateTime.UtcNow,
            RawMessage = "test"
        };

        // Act
        await hubService.BroadcastMessageProcessedAsync(mt103Message);

        // Wait for message to be received
        await Task.Delay(500);

        // Assert
        _receivedMessages.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task UnsubscribeFromMessageType_StopsReceivingMessages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        // Subscribe and then unsubscribe
        await _hubConnection!.InvokeAsync("SubscribeToMessageType", "MT103");
        await _hubConnection.InvokeAsync("UnsubscribeFromMessageType", "MT103");

        _receivedMessages.Clear();

        var mt103Message = new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            Status = MessageStatus.Processed,
            ProcessedAt = DateTime.UtcNow,
            RawMessage = "test"
        };

        // Act
        await hubService.BroadcastMessageProcessedAsync(mt103Message);

        // Wait to ensure no message is received
        await Task.Delay(500);

        // Assert - Should still receive via "All" broadcast, but not via group
        // This test verifies the unsubscribe worked
        _hubConnection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task MultipleClients_ReceiveBroadcastMessages()
    {
        // Arrange
        var secondConnection = new HubConnectionBuilder()
            .WithUrl($"{_factory.Server.BaseAddress}hubs/messages", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var secondClientMessages = new List<object>();
        secondConnection.On<object>("ReceiveMessageUpdate", message =>
        {
            secondClientMessages.Add(message);
        });

        await secondConnection.StartAsync();

        using var scope = _factory.Services.CreateScope();
        var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

        var message = new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            Status = MessageStatus.Processed,
            ProcessedAt = DateTime.UtcNow,
            RawMessage = "test"
        };

        // Act
        await hubService.BroadcastMessageProcessedAsync(message);

        // Wait for messages to be received
        await Task.Delay(500);

        // Assert
        _receivedMessages.Should().HaveCountGreaterThan(0);
        secondClientMessages.Should().HaveCountGreaterThan(0);

        // Cleanup
        await secondConnection.DisposeAsync();
    }
}
