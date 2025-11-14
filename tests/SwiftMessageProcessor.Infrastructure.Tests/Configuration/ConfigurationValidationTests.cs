using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Extensions;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Configuration;

public class ConfigurationValidationTests
{
    [Fact]
    public void DatabaseOptionsValidator_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Provider = "SQLite",
            ConnectionString = "Data Source=test.db"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void DatabaseOptionsValidator_EmptyProvider_ReturnsFailed()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Provider = "",
            ConnectionString = "Data Source=test.db"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("provider must be specified", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabaseOptionsValidator_UnsupportedProvider_ReturnsFailed()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Provider = "MySQL",
            ConnectionString = "Server=localhost;Database=test"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("not supported", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueueOptionsValidator_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new QueueOptionsValidator();
        var options = new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "input",
                CompletedQueue = "completed",
                DeadLetterQueue = "dlq"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void QueueOptionsValidator_EmptyQueueNames_ReturnsFailed()
    {
        // Arrange
        var validator = new QueueOptionsValidator();
        var options = new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "",
                CompletedQueue = "",
                DeadLetterQueue = ""
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void ProcessingOptionsValidator_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new ProcessingOptionsValidator();
        var options = new ProcessingOptions
        {
            MaxConcurrentMessages = 10,
            MessageProcessingTimeoutSeconds = 60,
            RetryAttempts = 3,
            RetryDelaySeconds = 5,
            QueuePollingIntervalMilliseconds = 1000
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ProcessingOptionsValidator_InvalidValues_ReturnsFailed()
    {
        // Arrange
        var validator = new ProcessingOptionsValidator();
        var options = new ProcessingOptions
        {
            MaxConcurrentMessages = 0,
            MessageProcessingTimeoutSeconds = 0,
            RetryAttempts = -1,
            RetryDelaySeconds = 0,
            QueuePollingIntervalMilliseconds = 50
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void TestModeOptionsValidator_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new TestModeOptionsValidator();
        var options = new TestModeOptions
        {
            Enabled = true,
            GenerationInterval = TimeSpan.FromSeconds(10),
            ValidMessagePercentage = 80,
            BatchSize = 1
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void TestModeOptionsValidator_InvalidPercentage_ReturnsFailed()
    {
        // Arrange
        var validator = new TestModeOptionsValidator();
        var options = new TestModeOptions
        {
            Enabled = true,
            GenerationInterval = TimeSpan.FromSeconds(10),
            ValidMessagePercentage = 150,
            BatchSize = 1
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void CommunicationOptionsValidator_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new CommunicationOptionsValidator();
        var options = new CommunicationOptions
        {
            CommunicationDirectory = "/tmp/ipc",
            StatusUpdateIntervalSeconds = 5,
            CommandTimeoutSeconds = 30
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }
}
