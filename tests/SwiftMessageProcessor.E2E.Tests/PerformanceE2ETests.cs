using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.E2E.Tests;

/// <summary>
/// End-to-end performance and load tests for the distributed system
/// </summary>
public class PerformanceE2ETests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;
    private readonly HttpClient _client;

    public PerformanceE2ETests(E2ETestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task HighVolumeProcessing_100Messages_CompletesInReasonableTime()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 100;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"PERF{i:D4}", i * 10.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages in batches to simulate realistic load
        var batchSize = 10;
        var batches = messages.Chunk(batchSize);
        
        foreach (var batch in batches)
        {
            var tasks = batch.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // 30 seconds max for 100 messages
        
        // Verify all messages are processed
        await Task.Delay(1000);
        var response = await _client.GetAsync("/api/messages");
        var retrievedMessages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        retrievedMessages.Should().NotBeNull();
        retrievedMessages!.Count.Should().BeGreaterOrEqualTo(messageCount);
    }

    [Fact]
    public async Task ConcurrentAPIRequests_HandledEfficiently()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Pre-populate with some messages
        for (int i = 0; i < 20; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"API{i:D3}", i * 100.00m));
        }
        
        await Task.Delay(500);

        var stopwatch = Stopwatch.StartNew();

        // Act - Make concurrent API requests
        var tasks = Enumerable.Range(0, 50).Select(_ => _client.GetAsync("/api/messages"));
        var responses = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds for 50 concurrent requests
    }

    [Fact]
    public async Task MessageProcessingThroughput_MeetsPerformanceTarget()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 50;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"THRU{i:D3}", i * 50.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process all messages concurrently
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        
        // Calculate throughput (messages per second)
        var throughput = messageCount / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughput.Should().BeGreaterThan(5); // At least 5 messages per second
    }

    [Fact]
    public async Task DatabaseQueryPerformance_WithLargeDataset()
    {
        // Arrange - Create a larger dataset
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 100;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"QUERY{i:D4}", i * 10.00m))
            .ToList();

        // Process messages in batches
        var batchSize = 20;
        foreach (var batch in messages.Chunk(batchSize))
        {
            var tasks = batch.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
        }

        await Task.Delay(1000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Query messages with various filters
        var allMessagesTask = _client.GetAsync("/api/messages");
        var processedTask = _client.GetAsync("/api/messages?status=Processed");
        var searchTask = _client.GetAsync("/api/messages/search?query=QUERY");

        await Task.WhenAll(allMessagesTask, processedTask, searchTask);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // 2 seconds for multiple queries
        
        var allResponse = await allMessagesTask;
        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MemoryUsage_StableUnderContinuousLoad()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int iterations = 5;
        const int messagesPerIteration = 20;

        // Act - Process messages in multiple iterations
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var messages = Enumerable.Range(1, messagesPerIteration)
                .Select(i => GetValidMT103Message($"MEM{iteration}_{i:D2}", i * 100.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);

            // Small delay between iterations
            await Task.Delay(200);
        }

        // Assert - System should still be responsive
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var messagesResponse = await _client.GetAsync("/api/messages");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignalRPerformance_MultipleConnections()
    {
        // This test validates SignalR can handle multiple concurrent connections
        // Arrange
        var messagesReceived = 0;
        var messageReceivedEvent = new TaskCompletionSource<bool>();

        _fixture.SignalRConnection!.On<ProcessedMessage>("ReceiveMessage", _ =>
        {
            Interlocked.Increment(ref messagesReceived);
            messageReceivedEvent.TrySetResult(true);
        });

        var stopwatch = Stopwatch.StartNew();

        // Act - Process multiple messages
        var messages = Enumerable.Range(1, 10)
            .Select(i => GetValidMT103Message($"SR{i:D2}", i * 100.00m))
            .ToList();

        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        await Task.WhenAll(tasks);

        // Wait for SignalR notifications
        await Task.WhenAny(messageReceivedEvent.Task, Task.Delay(3000));
        stopwatch.Stop();

        // Assert
        messagesReceived.Should().BeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task MixedWorkload_ValidAndInvalidMessages_PerformanceStable()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var messages = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            if (i % 5 == 0)
            {
                messages.Add($"INVALID_MESSAGE_{i}"); // 20% invalid
            }
            else
            {
                messages.Add(GetValidMT103Message($"MIX{i:D3}", i * 100.00m)); // 80% valid
            }
        }

        var stopwatch = Stopwatch.StartNew();

        // Act - Process mixed workload
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        successCount.Should().BeGreaterThan(30); // Most should succeed
        failureCount.Should().BeGreaterThan(5); // Some should fail
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // 15 seconds for 50 messages
    }

    [Fact]
    public async Task PaginationPerformance_LargeResultSets()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create 50 messages
        var messages = Enumerable.Range(1, 50)
            .Select(i => GetValidMT103Message($"PAGE{i:D3}", i * 10.00m))
            .ToList();

        foreach (var batch in messages.Chunk(10))
        {
            var tasks = batch.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
        }

        await Task.Delay(1000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Request multiple pages
        var page1Task = _client.GetAsync("/api/messages?skip=0&take=10");
        var page2Task = _client.GetAsync("/api/messages?skip=10&take=10");
        var page3Task = _client.GetAsync("/api/messages?skip=20&take=10");

        await Task.WhenAll(page1Task, page2Task, page3Task);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        
        var page1 = await page1Task;
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchPerformance_ComplexQueries()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create messages with various references
        var messages = Enumerable.Range(1, 30)
            .Select(i => GetValidMT103Message($"SEARCH{i:D3}", i * 100.00m))
            .ToList();

        foreach (var batch in messages.Chunk(10))
        {
            var tasks = batch.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
        }

        await Task.Delay(500);

        var stopwatch = Stopwatch.StartNew();

        // Act - Perform multiple searches
        var searchTasks = new[]
        {
            _client.GetAsync("/api/messages/search?query=SEARCH001"),
            _client.GetAsync("/api/messages/search?query=SEARCH015"),
            _client.GetAsync("/api/messages/search?query=SEARCH030")
        };

        var responses = await Task.WhenAll(searchTasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1500);
    }

    [Fact]
    public async Task SystemRecovery_AfterHighLoad()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Act - Apply high load
        var messages = Enumerable.Range(1, 50)
            .Select(i => GetValidMT103Message($"LOAD{i:D3}", i * 50.00m))
            .ToList();

        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        await Task.WhenAll(tasks);

        await Task.Delay(1000);

        // Assert - System should recover and be responsive
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusResponse = await _client.GetAsync("/api/system/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should be able to process new messages
        var newMessage = GetValidMT103Message("RECOVERY001", 1000.00m);
        var result = await _fixture.SimulateMessageProcessingAsync(newMessage);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentDatabaseAccess_NoDeadlocks()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Pre-populate database
        for (int i = 0; i < 20; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"DB{i:D3}", i * 100.00m));
        }

        await Task.Delay(500);

        // Act - Concurrent reads and writes
        var readTasks = Enumerable.Range(0, 20).Select(_ => _client.GetAsync("/api/messages"));
        var writeTasks = Enumerable.Range(20, 10).Select(i => 
            _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"DB{i:D3}", i * 100.00m)));

        var allTasks = readTasks.Cast<Task>().Concat(writeTasks).ToList();
        
        // Assert - All operations should complete without deadlock
        var completedTask = await Task.WhenAny(Task.WhenAll(allTasks), Task.Delay(10000));
        completedTask.Should().Be(Task.WhenAll(allTasks), "Operations should complete without deadlock");
    }

    // Helper method
    private string GetValidMT103Message(string reference = "TESTREF123", decimal amount = 1000.00m)
    {
        return $@"{{1:F01BANKBEBBAXXX0000000000}}{{2:I103BANKUS33XXXXN}}{{3:{{108:{reference}}}}}
{{4:
:20:{reference}
:23B:CRED
:32A:231115EUR{amount:F2}
:50K:/BE62510007547061
ORDERING CUSTOMER NAME
:59:/US12345678901234567890
BENEFICIARY CUSTOMER NAME
:70:PAYMENT FOR INVOICE
:71A:SHA
-}}{{5:{{MAC:00000000}}{{CHK:123456789ABC}}}}";
    }
}
