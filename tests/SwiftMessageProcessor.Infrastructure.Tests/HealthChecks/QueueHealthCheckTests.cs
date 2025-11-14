using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.HealthChecks;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.HealthChecks;

public class QueueHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_QueueHealthy_ReturnsHealthy()
    {
        // Arrange
        var queueService = Substitute.For<IQueueService>();
        queueService.IsHealthyAsync().Returns(true);
        queueService.GetStatisticsAsync().Returns(new QueueStatistics
        {
            MessagesInQueue = 10,
            MessagesProcessed = 100,
            MessagesFailed = 5,
            LastUpdated = DateTime.UtcNow
        });

        var logger = Substitute.For<ILogger<QueueHealthCheck>>();
        var healthCheck = new QueueHealthCheck(queueService, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("messagesInQueue"));
        Assert.True(result.Data.ContainsKey("messagesProcessed"));
    }

    [Fact]
    public async Task CheckHealthAsync_QueueUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        var queueService = Substitute.For<IQueueService>();
        queueService.IsHealthyAsync().Returns(false);

        var logger = Substitute.For<ILogger<QueueHealthCheck>>();
        var healthCheck = new QueueHealthCheck(queueService, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_HighFailureRate_ReturnsDegraded()
    {
        // Arrange
        var queueService = Substitute.For<IQueueService>();
        queueService.IsHealthyAsync().Returns(true);
        queueService.GetStatisticsAsync().Returns(new QueueStatistics
        {
            MessagesInQueue = 10,
            MessagesProcessed = 100,
            MessagesFailed = 150, // High failure count
            LastUpdated = DateTime.UtcNow
        });

        var logger = Substitute.For<ILogger<QueueHealthCheck>>();
        var healthCheck = new QueueHealthCheck(queueService, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_HighQueueDepth_ReturnsDegraded()
    {
        // Arrange
        var queueService = Substitute.For<IQueueService>();
        queueService.IsHealthyAsync().Returns(true);
        queueService.GetStatisticsAsync().Returns(new QueueStatistics
        {
            MessagesInQueue = 1500, // High queue depth
            MessagesProcessed = 100,
            MessagesFailed = 5,
            LastUpdated = DateTime.UtcNow
        });

        var logger = Substitute.For<ILogger<QueueHealthCheck>>();
        var healthCheck = new QueueHealthCheck(queueService, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Exception_ReturnsUnhealthy()
    {
        // Arrange
        var queueService = Substitute.For<IQueueService>();
        queueService.IsHealthyAsync().Returns(Task.FromException<bool>(new Exception("Queue connection failed")));

        var logger = Substitute.For<ILogger<QueueHealthCheck>>();
        var healthCheck = new QueueHealthCheck(queueService, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
