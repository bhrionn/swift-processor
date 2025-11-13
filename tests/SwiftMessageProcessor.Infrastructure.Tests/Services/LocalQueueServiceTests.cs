using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class LocalQueueServiceTests
{
    private readonly LocalQueueService _queueService;
    private readonly QueueOptions _queueOptions;

    public LocalQueueServiceTests()
    {
        _queueOptions = new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "test-input",
                CompletedQueue = "test-completed",
                DeadLetterQueue = "test-dlq"
            }
        };

        var logger = new LoggerFactory().CreateLogger<LocalQueueService>();
        var options = Options.Create(_queueOptions);
        _queueService = new LocalQueueService(logger, options);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldEnqueueMessage()
    {
        // Arrange
        const string queueName = "test-queue";
        const string message = "test message";

        // Act
        await _queueService.SendMessageAsync(queueName, message);

        // Assert
        var receivedMessage = await _queueService.ReceiveMessageAsync(queueName);
        receivedMessage.Should().Be(message);
    }

    [Fact]
    public async Task ReceiveMessageAsync_EmptyQueue_ShouldReturnNull()
    {
        // Arrange
        const string queueName = "empty-queue";

        // Act
        var result = await _queueService.ReceiveMessageAsync(queueName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SendMessageAsync_MultipleMessages_ShouldMaintainOrder()
    {
        // Arrange
        const string queueName = "order-test-queue";
        var messages = new[] { "message1", "message2", "message3" };

        // Act
        foreach (var message in messages)
        {
            await _queueService.SendMessageAsync(queueName, message);
        }

        // Assert
        for (int i = 0; i < messages.Length; i++)
        {
            var receivedMessage = await _queueService.ReceiveMessageAsync(queueName);
            receivedMessage.Should().Be(messages[i]);
        }
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnTrue()
    {
        // Act
        var isHealthy = await _queueService.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        const string queueName = "stats-test-queue";
        await _queueService.SendMessageAsync(queueName, "message1");
        await _queueService.SendMessageAsync(queueName, "message2");

        // Act
        var statistics = await _queueService.GetStatisticsAsync();

        // Assert
        statistics.Should().NotBeNull();
        statistics.MessagesInQueue.Should().Be(2);
        statistics.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterProcessingMessages_ShouldUpdateProcessedCount()
    {
        // Arrange
        const string queueName = "processed-stats-queue";
        await _queueService.SendMessageAsync(queueName, "message1");
        await _queueService.SendMessageAsync(queueName, "message2");

        // Act
        await _queueService.ReceiveMessageAsync(queueName);
        var statistics = await _queueService.GetStatisticsAsync();

        // Assert
        statistics.MessagesInQueue.Should().Be(1);
        statistics.MessagesProcessed.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SendMessageAsync_InvalidQueueName_ShouldThrowArgumentException(string queueName)
    {
        // Act & Assert
        await _queueService.Invoking(s => s.SendMessageAsync(queueName, "test message"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SendMessageAsync_InvalidMessage_ShouldThrowArgumentException(string message)
    {
        // Act & Assert
        await _queueService.Invoking(s => s.SendMessageAsync("test-queue", message))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ReceiveMessageAsync_InvalidQueueName_ShouldThrowArgumentException(string queueName)
    {
        // Act & Assert
        await _queueService.Invoking(s => s.ReceiveMessageAsync(queueName))
            .Should().ThrowAsync<ArgumentException>();
    }
}