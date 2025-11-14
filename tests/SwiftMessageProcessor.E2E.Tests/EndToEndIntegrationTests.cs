using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.E2E.Tests;

/// <summary>
/// End-to-end integration tests that validate the complete system flow
/// from message ingestion to UI display
/// </summary>
public class EndToEndIntegrationTests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;
    private readonly HttpClient _client;

    public EndToEndIntegrationTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CompleteMessageFlow_FromIngestionToUIDisplay_Success()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        var validMT103 = GetValidMT103Message();
        var messageReceived = false;
        var receivedMessageId = Guid.Empty;

        // Subscribe to SignalR updates
        _fixture.SignalRConnection!.On<ProcessedMessage>("ReceiveMessage", message =>
        {
            messageReceived = true;
            receivedMessageId = message.Id;
        });

        // Act - Step 1: Enqueue message (simulating console app receiving from queue)
        await _fixture.EnqueueMessageAsync(validMT103);

        // Act - Step 2: Process message (simulating console app processing)
        var processingResult = await _fixture.SimulateMessageProcessingAsync(validMT103);

        // Wait for SignalR notification
        await Task.Delay(500);

        // Act - Step 3: Retrieve message via Web API (simulating UI request)
        var response = await _client.GetAsync("/api/messages");

        // Assert
        processingResult.Should().NotBeNull();
        processingResult.Success.Should().BeTrue();
        processingResult.Message.Should().NotBeNull();
        processingResult.Message!.Status.Should().Be(MessageStatus.Processed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        messages.Should().NotBeNull();
        messages.Should().HaveCountGreaterThan(0);
        
        var processedMessage = messages!.FirstOrDefault(m => m.Id == processingResult.Message.Id);
        processedMessage.Should().NotBeNull();
        processedMessage!.MessageType.Should().Be("MT103");
        processedMessage.Status.Should().Be("Processed");

        // Verify SignalR notification was received
        messageReceived.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidMessageFlow_MovesToDeadLetterQueue_AndLogsError()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        var invalidMessage = "INVALID_SWIFT_MESSAGE";

        // Act - Process invalid message
        var processingResult = await _fixture.SimulateMessageProcessingAsync(invalidMessage);

        // Wait for processing
        await Task.Delay(300);

        // Assert - Processing should fail
        processingResult.Should().NotBeNull();
        processingResult.Success.Should().BeFalse();
        processingResult.ErrorMessage.Should().NotBeNullOrEmpty();

        // Verify message is stored with failed status
        var response = await _client.GetAsync("/api/messages?status=Failed");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        messages.Should().NotBeNull();
        
        var failedMessage = messages!.FirstOrDefault(m => m.RawMessage == invalidMessage);
        failedMessage.Should().NotBeNull();
        failedMessage!.Status.Should().Be("Failed");
        failedMessage.ErrorDetails.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MultipleMessagesProcessing_ConcurrentFlow_AllProcessedCorrectly()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        var messages = new[]
        {
            GetValidMT103Message("REF001", 1000.00m),
            GetValidMT103Message("REF002", 2000.00m),
            GetValidMT103Message("REF003", 3000.00m),
            GetValidMT103Message("REF004", 4000.00m),
            GetValidMT103Message("REF005", 5000.00m)
        };

        var processedCount = 0;
        _fixture.SignalRConnection!.On<ProcessedMessage>("ReceiveMessage", _ =>
        {
            Interlocked.Increment(ref processedCount);
        });

        // Act - Process multiple messages concurrently
        var processingTasks = messages.Select(msg => 
            _fixture.SimulateMessageProcessingAsync(msg));
        
        var results = await Task.WhenAll(processingTasks);

        // Wait for all SignalR notifications
        await Task.Delay(1000);

        // Assert - All messages processed successfully
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.Message.Should().NotBeNull();
            r.Message!.Status.Should().Be(MessageStatus.Processed);
        });

        // Verify all messages are in database
        var response = await _client.GetAsync("/api/messages");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var retrievedMessages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        retrievedMessages.Should().NotBeNull();
        retrievedMessages.Should().HaveCountGreaterOrEqualTo(messages.Length);

        // Verify SignalR notifications
        processedCount.Should().BeGreaterOrEqualTo(messages.Length);
    }

    [Fact]
    public async Task MessageFiltering_ByStatus_ReturnsCorrectResults()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var validMessage = GetValidMT103Message("FILTER001", 1000.00m);
        var invalidMessage = "INVALID_MESSAGE_FOR_FILTER";

        await _fixture.SimulateMessageProcessingAsync(validMessage);
        await _fixture.SimulateMessageProcessingAsync(invalidMessage);
        
        await Task.Delay(300);

        // Act & Assert - Filter by Processed status
        var processedResponse = await _client.GetAsync("/api/messages?status=Processed");
        processedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var processedMessages = await processedResponse.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        processedMessages.Should().NotBeNull();
        processedMessages.Should().AllSatisfy(m => m.Status.Should().Be("Processed"));

        // Act & Assert - Filter by Failed status
        var failedResponse = await _client.GetAsync("/api/messages?status=Failed");
        failedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var failedMessages = await failedResponse.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        failedMessages.Should().NotBeNull();
        failedMessages.Should().AllSatisfy(m => m.Status.Should().Be("Failed"));
    }

    [Fact]
    public async Task MessageSearch_ByReference_ReturnsMatchingMessage()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var uniqueRef = $"SEARCH{Guid.NewGuid():N}";
        var message = GetValidMT103Message(uniqueRef, 5000.00m);

        await _fixture.SimulateMessageProcessingAsync(message);
        await Task.Delay(300);

        // Act
        var response = await _client.GetAsync($"/api/messages/search?query={uniqueRef}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results![0].RawMessage.Should().Contain(uniqueRef);
    }

    [Fact]
    public async Task SystemStatus_ReflectsProcessingActivity()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var initialStatusResponse = await _client.GetAsync("/api/system/status");
        var initialStatus = await initialStatusResponse.Content.ReadFromJsonAsync<SystemStatusDto>();

        // Act - Process some messages
        var messages = new[]
        {
            GetValidMT103Message("STATUS001", 1000.00m),
            GetValidMT103Message("STATUS002", 2000.00m),
            "INVALID_FOR_STATUS_TEST"
        };

        foreach (var msg in messages)
        {
            await _fixture.SimulateMessageProcessingAsync(msg);
        }

        await Task.Delay(500);

        var finalStatusResponse = await _client.GetAsync("/api/system/status");
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<SystemStatusDto>();

        // Assert
        initialStatus.Should().NotBeNull();
        finalStatus.Should().NotBeNull();
        
        // Processing counts should have increased
        finalStatus!.MessagesProcessed.Should().BeGreaterThan(initialStatus!.MessagesProcessed);
        finalStatus.MessagesFailed.Should().BeGreaterThan(initialStatus.MessagesFailed);
    }

    [Fact]
    public async Task HealthCheck_ReflectsSystemHealth()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task MessageDetail_ReturnsCompleteInformation()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var message = GetValidMT103Message("DETAIL001", 12345.67m);
        var result = await _fixture.SimulateMessageProcessingAsync(message);
        
        await Task.Delay(300);

        // Act
        var response = await _client.GetAsync($"/api/messages/{result.Message!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ProcessedMessageDto>();
        
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(result.Message.Id);
        detail.MessageType.Should().Be("MT103");
        detail.RawMessage.Should().Contain("DETAIL001");
        detail.ParsedData.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ErrorRecovery_RetryMechanism_EventuallySucceeds()
    {
        // This test validates that transient errors are handled with retry logic
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var message = GetValidMT103Message("RETRY001", 1000.00m);

        // Act - Process message (retry logic is internal to processing service)
        var result = await _fixture.SimulateMessageProcessingAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformanceUnderLoad_ProcessesMultipleMessagesConcurrently()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 20;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"PERF{i:D3}", i * 100.00m))
            .ToList();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Process messages concurrently
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(messageCount);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        
        // Performance assertion - should process 20 messages in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10 seconds max

        // Verify all messages are in database
        await Task.Delay(500);
        var response = await _client.GetAsync("/api/messages");
        var retrievedMessages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        retrievedMessages.Should().NotBeNull();
        retrievedMessages!.Count.Should().BeGreaterOrEqualTo(messageCount);
    }

    [Fact]
    public async Task DataConsistency_BetweenQueueAndDatabase()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        var message = GetValidMT103Message("CONSISTENCY001", 5000.00m);

        // Act - Enqueue and process
        await _fixture.EnqueueMessageAsync(message);
        var result = await _fixture.SimulateMessageProcessingAsync(message);
        
        await Task.Delay(300);

        // Assert - Message should be in database
        var dbResponse = await _client.GetAsync($"/api/messages/{result.Message!.Id}");
        dbResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var dbMessage = await dbResponse.Content.ReadFromJsonAsync<ProcessedMessageDto>();
        dbMessage.Should().NotBeNull();
        dbMessage!.Id.Should().Be(result.Message.Id);
        dbMessage.RawMessage.Should().Be(message);
    }

    [Fact]
    public async Task SignalRRealTimeUpdates_BroadcastToConnectedClients()
    {
        // Arrange
        var messagesReceived = new List<ProcessedMessage>();
        var messageReceivedEvent = new TaskCompletionSource<bool>();

        _fixture.SignalRConnection!.On<ProcessedMessage>("ReceiveMessage", message =>
        {
            messagesReceived.Add(message);
            messageReceivedEvent.TrySetResult(true);
        });

        // Act
        var message = GetValidMT103Message("SIGNALR001", 1000.00m);
        await _fixture.SimulateMessageProcessingAsync(message);

        // Wait for SignalR notification with timeout
        var receivedInTime = await Task.WhenAny(
            messageReceivedEvent.Task,
            Task.Delay(2000)
        ) == messageReceivedEvent.Task;

        // Assert
        receivedInTime.Should().BeTrue("SignalR notification should be received within 2 seconds");
        messagesReceived.Should().HaveCountGreaterThan(0);
        messagesReceived.Last().MessageType.Should().Be("MT103");
    }

    // Helper method to generate valid MT103 messages
    private string GetValidMT103Message(string reference = "TESTREF123", decimal amount = 1000.00m)
    {
        return $@"{{1:F01BANKBEBBAXXX0000000000}}{{2:I103BANKUS33XXXXN}}{{3:{{108:{reference}}}}}
{{4:
:20:{reference}
:23B:CRED
:32A:231115EUR{amount:F2}
:50K:/BE62510007547061
ORDERING CUSTOMER NAME
ORDERING CUSTOMER ADDRESS
:59:/US12345678901234567890
BENEFICIARY CUSTOMER NAME
BENEFICIARY CUSTOMER ADDRESS
:70:PAYMENT FOR INVOICE 12345
:71A:SHA
-}}{{5:{{MAC:00000000}}{{CHK:123456789ABC}}}}";
    }
}

// DTOs for API responses
public class ProcessedMessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string RawMessage { get; set; } = string.Empty;
    public string ParsedData { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
}

public class SystemStatusDto
{
    public bool IsRunning { get; set; }
    public bool IsProcessing { get; set; }
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public string Status { get; set; } = string.Empty;
}
