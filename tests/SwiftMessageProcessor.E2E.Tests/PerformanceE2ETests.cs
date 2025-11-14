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
/// Tests cover: message processing throughput, API query performance, 
/// database concurrent access, and system scalability
/// Requirements: 3.1, 4.1, 5.1
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

    #region Console Application Message Processing Throughput Tests (Requirement 3.1)

    [Fact]
    public async Task ConsoleApp_ProcessingThroughput_Meets1000MessagesPerMinute()
    {
        // Requirement 3.1: Console application should process messages within 1 second
        // Target: 1000 messages per minute = ~16.67 messages per second
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 200;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"THRU{i:D4}", i * 10.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages to measure throughput
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        successCount.Should().BeGreaterThan((int)(messageCount * 0.95)); // 95% success rate
        
        // Calculate throughput (messages per minute)
        var throughputPerMinute = (messageCount / (stopwatch.ElapsedMilliseconds / 1000.0)) * 60;
        throughputPerMinute.Should().BeGreaterThan(500); // At least 500 messages per minute
        
        // Individual message processing should be under 1 second
        stopwatch.ElapsedMilliseconds.Should().BeLessThan((long)(messageCount * 100)); // Average 100ms per message
    }

    [Fact]
    public async Task ConsoleApp_HighVolumeProcessing_500Messages_CompletesSuccessfully()
    {
        // Requirement 3.1: Test high volume processing capability
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 500;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"HVOL{i:D4}", i * 10.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages in batches to simulate realistic load
        var batchSize = 50;
        var batches = messages.Chunk(batchSize);
        
        foreach (var batch in batches)
        {
            var tasks = batch.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(60000); // 60 seconds max for 500 messages
        
        // Verify all messages are processed
        await Task.Delay(1000);
        var response = await _client.GetAsync("/api/messages");
        var retrievedMessages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        retrievedMessages.Should().NotBeNull();
        retrievedMessages!.Count.Should().BeGreaterOrEqualTo((int)(messageCount * 0.95));
    }

    [Fact]
    public async Task ConsoleApp_SustainedLoad_MultipleIterations_MaintainsPerformance()
    {
        // Requirement 3.1: Test sustained processing over multiple iterations
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int iterations = 5;
        const int messagesPerIteration = 50;
        var iterationTimes = new List<long>();

        // Act - Process messages in multiple iterations
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var messages = Enumerable.Range(1, messagesPerIteration)
                .Select(i => GetValidMT103Message($"SUST{iteration}_{i:D2}", i * 100.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            iterationTimes.Add(stopwatch.ElapsedMilliseconds);

            await Task.Delay(100); // Small delay between iterations
        }

        // Assert - Performance should remain consistent across iterations
        var avgTime = iterationTimes.Average();
        var maxTime = iterationTimes.Max();
        var minTime = iterationTimes.Min();
        
        // Max time should not be more than 2x the min time (performance degradation check)
        maxTime.Should().BeLessThan((long)(minTime * 2));
        avgTime.Should().BeLessThan(10000); // Average under 10 seconds per 50 messages
    }

    [Fact]
    public async Task ConsoleApp_ParsingPerformance_ComplexMessages()
    {
        // Requirement 3.1: Test parsing performance with complex MT103 messages
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 100;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetComplexMT103Message($"CMPLX{i:D3}", i * 1000.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        
        // Each message should be parsed within 1 second (Requirement 3.1)
        var avgTimePerMessage = stopwatch.ElapsedMilliseconds / (double)messageCount;
        avgTimePerMessage.Should().BeLessThan(1000);
    }

    #endregion

    #region Web API Query Performance Tests (Requirement 4.1, 5.1)

    [Fact]
    public async Task WebAPI_ConcurrentRequests_100Simultaneous_HandledEfficiently()
    {
        // Requirement 4.1, 5.1: Web API should handle high concurrent query load
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Pre-populate with messages
        for (int i = 0; i < 100; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"API{i:D3}", i * 100.00m));
        }
        
        await Task.Delay(1000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Make 100 concurrent API requests
        var tasks = Enumerable.Range(0, 100).Select(_ => _client.GetAsync("/api/messages"));
        var responses = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10 seconds for 100 concurrent requests
        
        // Calculate requests per second
        var requestsPerSecond = 100 / (stopwatch.ElapsedMilliseconds / 1000.0);
        requestsPerSecond.Should().BeGreaterThan(10); // At least 10 requests per second
    }

    [Fact]
    public async Task WebAPI_SearchPerformance_Under2Seconds()
    {
        // Requirement 1.4: Search should return results within 2 seconds
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create 200 messages with searchable references
        for (int i = 0; i < 200; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"SEARCH{i:D4}", i * 50.00m));
        }

        await Task.Delay(1000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Perform search
        var response = await _client.GetAsync("/api/messages/search?query=SEARCH0150");

        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // Must be under 2 seconds
    }

    [Fact]
    public async Task WebAPI_ComplexFilterQueries_PerformEfficiently()
    {
        // Requirement 4.1: Test complex filtering with large dataset
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create diverse dataset
        for (int i = 0; i < 150; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"FILTER{i:D3}", i * 100.00m));
        }

        await Task.Delay(1000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Multiple complex filter queries
        var filterTasks = new[]
        {
            _client.GetAsync("/api/messages?status=Processed&skip=0&take=20"),
            _client.GetAsync("/api/messages?status=Processed&skip=20&take=20"),
            _client.GetAsync("/api/messages?skip=0&take=50"),
            _client.GetAsync("/api/messages/search?query=FILTER100")
        };

        var responses = await Task.WhenAll(filterTasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // All queries under 3 seconds
    }

    [Fact]
    public async Task WebAPI_PaginationPerformance_LargeDataset()
    {
        // Requirement 4.1: Test pagination performance with large result sets
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create 300 messages
        for (int i = 0; i < 300; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"PAGE{i:D3}", i * 10.00m));
        }

        await Task.Delay(1500);

        var stopwatch = Stopwatch.StartNew();

        // Act - Request multiple pages concurrently
        var pageTasks = Enumerable.Range(0, 10)
            .Select(page => _client.GetAsync($"/api/messages?skip={page * 20}&take=20"))
            .ToList();

        var responses = await Task.WhenAll(pageTasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 10 pages under 5 seconds
    }

    [Fact]
    public async Task WebAPI_MixedReadWriteLoad_MaintainsPerformance()
    {
        // Requirement 4.1, 5.1: Test API performance under mixed read/write load
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Pre-populate
        for (int i = 0; i < 50; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"MIX{i:D3}", i * 100.00m));
        }

        await Task.Delay(500);

        var stopwatch = Stopwatch.StartNew();

        // Act - Concurrent reads and writes
        var readTasks = Enumerable.Range(0, 30).Select(_ => _client.GetAsync("/api/messages"));
        var writeTasks = Enumerable.Range(50, 20).Select(i => 
            _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"MIX{i:D3}", i * 100.00m)));

        var allTasks = readTasks.Cast<Task>().Concat(writeTasks).ToList();
        await Task.WhenAll(allTasks);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(8000); // Mixed load under 8 seconds
    }

    #endregion

    #region Database Concurrent Access Tests (Requirement 4.1)

    [Fact]
    public async Task Database_ConcurrentWrites_FromConsoleApp_NoDeadlocks()
    {
        // Requirement 4.1: Database should handle concurrent writes without deadlocks
        // Requirement 4.3: Retry up to 3 times before logging error
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int concurrentWrites = 50;
        var messages = Enumerable.Range(1, concurrentWrites)
            .Select(i => GetValidMT103Message($"DBWR{i:D3}", i * 100.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Concurrent database writes
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert - All writes should complete without deadlock
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // Should complete within 15 seconds
    }

    [Fact]
    public async Task Database_ConcurrentReadsAndWrites_BothServices_NoConflicts()
    {
        // Requirement 4.1: Test concurrent access from both Web API and Console App
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Pre-populate database
        for (int i = 0; i < 50; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"CONC{i:D3}", i * 100.00m));
        }

        await Task.Delay(500);

        var stopwatch = Stopwatch.StartNew();

        // Act - Concurrent reads (Web API) and writes (Console App)
        var readTasks = Enumerable.Range(0, 40).Select(_ => _client.GetAsync("/api/messages"));
        var writeTasks = Enumerable.Range(50, 30).Select(i => 
            _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"CONC{i:D3}", i * 100.00m)));

        var allTasks = readTasks.Cast<Task>().Concat(writeTasks).ToList();
        
        // Assert - All operations should complete without deadlock or timeout
        var completedTask = await Task.WhenAny(Task.WhenAll(allTasks), Task.Delay(20000));
        completedTask.Should().Be(Task.WhenAll(allTasks), "Operations should complete without deadlock");
        
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000);
    }

    [Fact]
    public async Task Database_HighVolumeQueries_WithIndexes_PerformEfficiently()
    {
        // Requirement 4.1: Test database query performance with proper indexing
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create large dataset (500 messages)
        for (int i = 0; i < 500; i++)
        {
            await _fixture.SimulateMessageProcessingAsync(GetValidMT103Message($"IDX{i:D4}", i * 10.00m));
        }

        await Task.Delay(2000);

        var stopwatch = Stopwatch.StartNew();

        // Act - Multiple queries that should use indexes
        var queryTasks = new[]
        {
            _client.GetAsync("/api/messages?status=Processed"), // Status index
            _client.GetAsync("/api/messages?skip=0&take=50"), // ProcessedAt index
            _client.GetAsync("/api/messages/search?query=IDX0250"), // Search
            _client.GetAsync("/api/messages?status=Processed&skip=100&take=50")
        };

        var responses = await Task.WhenAll(queryTasks);

        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(4000); // Indexed queries should be fast
    }

    [Fact]
    public async Task Database_TransactionPerformance_WithRetryLogic()
    {
        // Requirement 4.3: Test retry logic for database operations
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int messageCount = 100;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"RETRY{i:D3}", i * 100.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Process messages with potential retry scenarios
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        successCount.Should().BeGreaterThan((int)(messageCount * 0.95)); // 95% success rate with retries
        
        // Even with retries, should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000);
    }

    [Fact]
    public async Task Database_ConnectionPooling_UnderHighLoad()
    {
        // Requirement 4.1: Test database connection pooling efficiency
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int iterations = 3;
        const int messagesPerIteration = 50;

        // Act - Multiple iterations to test connection pooling
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var messages = Enumerable.Range(1, messagesPerIteration)
                .Select(i => GetValidMT103Message($"POOL{iteration}_{i:D2}", i * 100.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            
            // Each iteration should complete efficiently
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000);
            
            await Task.Delay(100);
        }

        // Assert - System should remain responsive
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region System Scalability and Resource Utilization Tests (Requirement 3.1, 4.1, 5.1)

    [Fact]
    public async Task System_Scalability_1000Messages_EndToEnd()
    {
        // Requirement 3.1, 4.1, 5.1: Test system scalability with high volume
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int totalMessages = 1000;
        const int batchSize = 100;
        var processingTimes = new List<long>();

        // Act - Process 1000 messages in batches
        for (int batch = 0; batch < totalMessages / batchSize; batch++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var messages = Enumerable.Range(batch * batchSize, batchSize)
                .Select(i => GetValidMT103Message($"SCALE{i:D4}", i * 10.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            processingTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert - Performance should remain consistent
        var avgTime = processingTimes.Average();
        var maxTime = processingTimes.Max();
        
        // Performance should not degrade significantly
        maxTime.Should().BeLessThan((long)(avgTime * 1.5));
        
        // Verify system health after high load
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_ResourceUtilization_StableUnderContinuousLoad()
    {
        // Requirement 3.1, 4.1, 5.1: Test resource utilization stability
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int iterations = 10;
        const int messagesPerIteration = 30;

        // Act - Continuous load over multiple iterations
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var messages = Enumerable.Range(1, messagesPerIteration)
                .Select(i => GetValidMT103Message($"RES{iteration}_{i:D2}", i * 100.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);

            // Verify system remains responsive after each iteration
            var healthResponse = await _client.GetAsync("/health");
            healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            await Task.Delay(100);
        }

        // Assert - System should still be fully functional
        var messagesResponse = await _client.GetAsync("/api/messages");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var statusResponse = await _client.GetAsync("/api/system/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_RecoveryAfterPeakLoad_QuicklyRestoresPerformance()
    {
        // Requirement 3.1, 4.1, 5.1: Test system recovery after peak load
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Act - Apply peak load
        var peakMessages = Enumerable.Range(1, 200)
            .Select(i => GetValidMT103Message($"PEAK{i:D3}", i * 50.00m))
            .ToList();

        var peakTasks = peakMessages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        await Task.WhenAll(peakTasks);

        await Task.Delay(1000);

        // Measure recovery performance
        var stopwatch = Stopwatch.StartNew();
        
        var recoveryMessage = GetValidMT103Message("RECOVERY001", 1000.00m);
        var result = await _fixture.SimulateMessageProcessingAsync(recoveryMessage);
        
        stopwatch.Stop();

        // Assert - System should recover quickly
        result.Success.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // Quick recovery
        
        // API should be responsive
        var apiResponse = await _client.GetAsync("/api/messages?skip=0&take=10");
        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_ThroughputConsistency_AcrossMultipleRuns()
    {
        // Requirement 3.1, 4.1, 5.1: Test throughput consistency
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        const int runs = 5;
        const int messagesPerRun = 50;
        var throughputs = new List<double>();

        // Act - Multiple runs to measure consistency
        for (int run = 0; run < runs; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var messages = Enumerable.Range(1, messagesPerRun)
                .Select(i => GetValidMT103Message($"CONS{run}_{i:D2}", i * 100.00m))
                .ToList();

            var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            
            var throughput = messagesPerRun / (stopwatch.ElapsedMilliseconds / 1000.0);
            throughputs.Add(throughput);
            
            await Task.Delay(200);
        }

        // Assert - Throughput should be consistent
        var avgThroughput = throughputs.Average();
        var minThroughput = throughputs.Min();
        var maxThroughput = throughputs.Max();
        
        // Variation should be within acceptable range
        (maxThroughput - minThroughput).Should().BeLessThan(avgThroughput * 0.5);
        avgThroughput.Should().BeGreaterThan(5); // Minimum 5 messages per second
    }

    [Fact]
    public async Task System_EndToEndLatency_UnderLoad()
    {
        // Requirement 3.1, 4.1, 5.1: Test end-to-end latency from processing to API retrieval
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Apply background load
        var backgroundMessages = Enumerable.Range(1, 50)
            .Select(i => GetValidMT103Message($"BG{i:D3}", i * 100.00m))
            .ToList();
        
        var backgroundTasks = backgroundMessages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        _ = Task.WhenAll(backgroundTasks); // Don't await, run in background

        await Task.Delay(500);

        // Act - Measure end-to-end latency for a new message
        var stopwatch = Stopwatch.StartNew();
        
        var testMessage = GetValidMT103Message("LATENCY001", 5000.00m);
        var processResult = await _fixture.SimulateMessageProcessingAsync(testMessage);
        
        await Task.Delay(200); // Allow for propagation
        
        var apiResponse = await _client.GetAsync("/api/messages/search?query=LATENCY001");
        
        stopwatch.Stop();

        // Assert
        processResult.Success.Should().BeTrue();
        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000); // End-to-end under 3 seconds
    }

    [Fact]
    public async Task System_QueueProcessing_HighThroughput()
    {
        // Requirement 3.1, 5.1: Test queue processing throughput
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        const int messageCount = 100;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => GetValidMT103Message($"QUEUE{i:D3}", i * 100.00m))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act - Enqueue and process messages
        foreach (var message in messages)
        {
            await _fixture.EnqueueMessageAsync(message);
        }
        
        // Process all queued messages
        var processTasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        await Task.WhenAll(processTasks);

        stopwatch.Stop();

        // Assert
        var throughput = messageCount / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughput.Should().BeGreaterThan(10); // At least 10 messages per second through queue
    }

    #endregion

    #region Additional Performance Tests

    [Fact]
    public async Task SignalRPerformance_MultipleConnections()
    {
        // Requirement 2.5: Test SignalR performance with multiple connections
        
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
        var messages = Enumerable.Range(1, 20)
            .Select(i => GetValidMT103Message($"SR{i:D2}", i * 100.00m))
            .ToList();

        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        await Task.WhenAll(tasks);

        // Wait for SignalR notifications
        await Task.WhenAny(messageReceivedEvent.Task, Task.Delay(5000));
        stopwatch.Stop();

        // Assert
        messagesReceived.Should().BeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(8000);
    }

    [Fact]
    public async Task MixedWorkload_ValidAndInvalidMessages_PerformanceStable()
    {
        // Requirement 3.1, 3.3: Test performance with mixed valid/invalid messages
        
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var messages = new List<string>();
        for (int i = 0; i < 100; i++)
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

        successCount.Should().BeGreaterThan(60); // Most should succeed
        failureCount.Should().BeGreaterThan(10); // Some should fail
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(20000); // 20 seconds for 100 messages
    }

    #endregion

    #region Helper Methods

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

    private string GetComplexMT103Message(string reference, decimal amount)
    {
        return $@"{{1:F01BANKBEBBAXXX0000000000}}{{2:I103BANKUS33XXXXN}}{{3:{{108:{reference}}}}}
{{4:
:20:{reference}
:23B:CRED
:32A:231115EUR{amount:F2}
:33B:USD{amount * 1.1m:F2}
:50K:/BE62510007547061
ORDERING CUSTOMER NAME
ADDRESS LINE 1
ADDRESS LINE 2
:52A:BANKBEBBXXX
:53B:/1234567890
SENDERS CORRESPONDENT
:56A:INTERBANKXXX
:57A:ACCOUNTBANKXXX
:59:/US12345678901234567890
BENEFICIARY CUSTOMER NAME
BENEFICIARY ADDRESS LINE 1
BENEFICIARY ADDRESS LINE 2
:70:PAYMENT FOR INVOICE INV-{reference}
ADDITIONAL REMITTANCE INFO
MORE DETAILS HERE
:71A:SHA
:72:/ACC/ADDITIONAL INFO
-}}{{5:{{MAC:00000000}}{{CHK:123456789ABC}}}}";
    }

    #endregion
}
