using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class ErrorLoggingServiceTests
{
    [Fact]
    public void LogParsingError_IncrementsParsingErrorCounter()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        errorService.LogParsingError("MSG123", "Invalid format");
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(1, statistics.ParsingErrors);
        Assert.Equal(1, statistics.TotalErrors);
    }

    [Fact]
    public void LogValidationError_IncrementsValidationErrorCounter()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        errorService.LogValidationError("MSG123", "Missing required field");
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(1, statistics.ValidationErrors);
        Assert.Equal(1, statistics.TotalErrors);
    }

    [Fact]
    public void LogDatabaseError_IncrementsDatabaseErrorCounter()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        errorService.LogDatabaseError("SaveMessage", "Connection timeout");
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(1, statistics.DatabaseErrors);
        Assert.Equal(1, statistics.TotalErrors);
    }

    [Fact]
    public void LogQueueError_IncrementsQueueErrorCounter()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        errorService.LogQueueError("ReceiveMessage", "input-queue", "Queue not found");
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(1, statistics.QueueErrors);
        Assert.Equal(1, statistics.TotalErrors);
    }

    [Fact]
    public void GetErrorStatistics_MultipleErrors_ReturnsCorrectTotals()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        errorService.LogParsingError("MSG1", "Error 1");
        errorService.LogParsingError("MSG2", "Error 2");
        errorService.LogValidationError("MSG3", "Error 3");
        errorService.LogDatabaseError("Save", "Error 4");
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(4, statistics.TotalErrors);
        Assert.Equal(2, statistics.ParsingErrors);
        Assert.Equal(1, statistics.ValidationErrors);
        Assert.Equal(1, statistics.DatabaseErrors);
    }

    [Fact]
    public void GetErrorStatistics_NoErrors_ReturnsZeros()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ErrorLoggingService>>();
        var metricsLogger = Substitute.For<ILogger<MetricsCollectionService>>();
        var metricsService = new MetricsCollectionService(metricsLogger);
        var errorService = new ErrorLoggingService(logger, metricsService);

        // Act
        var statistics = errorService.GetErrorStatistics();

        // Assert
        Assert.Equal(0, statistics.TotalErrors);
        Assert.Equal(0, statistics.ParsingErrors);
        Assert.Equal(0, statistics.ValidationErrors);
    }
}
