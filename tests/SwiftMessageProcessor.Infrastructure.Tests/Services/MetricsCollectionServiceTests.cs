using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class MetricsCollectionServiceTests
{
    [Fact]
    public void IncrementCounter_NewCounter_CreatesAndIncrementsCounter()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);

        // Act
        service.IncrementCounter("test.counter", 5);
        var snapshot = service.GetSnapshot();

        // Assert
        var counter = snapshot.Counters.FirstOrDefault(c => c.Name == "test.counter");
        Assert.NotNull(counter);
        Assert.Equal(5, counter.Value);
    }

    [Fact]
    public void IncrementCounter_ExistingCounter_IncrementsValue()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);

        // Act
        service.IncrementCounter("test.counter", 5);
        service.IncrementCounter("test.counter", 3);
        var snapshot = service.GetSnapshot();

        // Assert
        var counter = snapshot.Counters.FirstOrDefault(c => c.Name == "test.counter");
        Assert.NotNull(counter);
        Assert.Equal(8, counter.Value);
    }

    [Fact]
    public void RecordTiming_NewTimer_CreatesAndRecordsTiming()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);

        // Act
        service.RecordTiming("test.timer", 100.5);
        var snapshot = service.GetSnapshot();

        // Assert
        var timer = snapshot.Timers.FirstOrDefault(t => t.Name == "test.timer");
        Assert.NotNull(timer);
        Assert.Equal(1, timer.Count);
        Assert.Equal(100.5, timer.Average);
    }

    [Fact]
    public void RecordTiming_MultipleRecordings_CalculatesCorrectStatistics()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);

        // Act
        service.RecordTiming("test.timer", 100);
        service.RecordTiming("test.timer", 200);
        service.RecordTiming("test.timer", 300);
        var snapshot = service.GetSnapshot();

        // Assert
        var timer = snapshot.Timers.FirstOrDefault(t => t.Name == "test.timer");
        Assert.NotNull(timer);
        Assert.Equal(3, timer.Count);
        Assert.Equal(200, timer.Average);
        Assert.Equal(100, timer.Min);
        Assert.Equal(300, timer.Max);
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);
        service.IncrementCounter("test.counter", 5);
        service.RecordTiming("test.timer", 100);

        // Act
        service.Reset();
        var snapshot = service.GetSnapshot();

        // Assert
        Assert.Empty(snapshot.Counters);
        Assert.Empty(snapshot.Timers);
    }

    [Fact]
    public void GetSnapshot_ReturnsCurrentTimestamp()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MetricsCollectionService>>();
        var service = new MetricsCollectionService(logger);
        var beforeSnapshot = DateTime.UtcNow;

        // Act
        var snapshot = service.GetSnapshot();
        var afterSnapshot = DateTime.UtcNow;

        // Assert
        Assert.True(snapshot.Timestamp >= beforeSnapshot);
        Assert.True(snapshot.Timestamp <= afterSnapshot);
    }
}
