using Microsoft.Extensions.Logging;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for categorized error logging
/// </summary>
public class ErrorLoggingService
{
    private readonly ILogger<ErrorLoggingService> _logger;
    private readonly MetricsCollectionService _metricsService;

    public ErrorLoggingService(
        ILogger<ErrorLoggingService> logger,
        MetricsCollectionService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Logs a parsing error
    /// </summary>
    public void LogParsingError(string messageId, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.parsing", 1, new Dictionary<string, string>
        {
            { "category", "parsing" }
        });

        _logger.LogError(exception,
            "Parsing error for message {MessageId}: {ErrorDetails}",
            messageId, errorDetails);
    }

    /// <summary>
    /// Logs a validation error
    /// </summary>
    public void LogValidationError(string messageId, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.validation", 1, new Dictionary<string, string>
        {
            { "category", "validation" }
        });

        _logger.LogWarning(exception,
            "Validation error for message {MessageId}: {ErrorDetails}",
            messageId, errorDetails);
    }

    /// <summary>
    /// Logs a database error
    /// </summary>
    public void LogDatabaseError(string operation, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.database", 1, new Dictionary<string, string>
        {
            { "category", "database" },
            { "operation", operation }
        });

        _logger.LogError(exception,
            "Database error during {Operation}: {ErrorDetails}",
            operation, errorDetails);
    }

    /// <summary>
    /// Logs a queue error
    /// </summary>
    public void LogQueueError(string operation, string queueName, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.queue", 1, new Dictionary<string, string>
        {
            { "category", "queue" },
            { "operation", operation },
            { "queue", queueName }
        });

        _logger.LogError(exception,
            "Queue error during {Operation} on {QueueName}: {ErrorDetails}",
            operation, queueName, errorDetails);
    }

    /// <summary>
    /// Logs a system error
    /// </summary>
    public void LogSystemError(string component, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.system", 1, new Dictionary<string, string>
        {
            { "category", "system" },
            { "component", component }
        });

        _logger.LogCritical(exception,
            "System error in {Component}: {ErrorDetails}",
            component, errorDetails);
    }

    /// <summary>
    /// Logs a communication error
    /// </summary>
    public void LogCommunicationError(string operation, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.communication", 1, new Dictionary<string, string>
        {
            { "category", "communication" },
            { "operation", operation }
        });

        _logger.LogError(exception,
            "Communication error during {Operation}: {ErrorDetails}",
            operation, errorDetails);
    }

    /// <summary>
    /// Logs a processing error
    /// </summary>
    public void LogProcessingError(string messageId, string stage, string errorDetails, Exception? exception = null)
    {
        _metricsService.IncrementCounter("errors.processing", 1, new Dictionary<string, string>
        {
            { "category", "processing" },
            { "stage", stage }
        });

        _logger.LogError(exception,
            "Processing error for message {MessageId} at stage {Stage}: {ErrorDetails}",
            messageId, stage, errorDetails);
    }

    /// <summary>
    /// Gets error statistics
    /// </summary>
    public ErrorStatistics GetErrorStatistics()
    {
        var snapshot = _metricsService.GetSnapshot();
        var errorCounters = snapshot.Counters.Where(c => c.Name.StartsWith("errors.")).ToList();

        return new ErrorStatistics
        {
            TotalErrors = errorCounters.Sum(c => c.Value),
            ParsingErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.parsing")?.Value ?? 0,
            ValidationErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.validation")?.Value ?? 0,
            DatabaseErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.database")?.Value ?? 0,
            QueueErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.queue")?.Value ?? 0,
            SystemErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.system")?.Value ?? 0,
            CommunicationErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.communication")?.Value ?? 0,
            ProcessingErrors = errorCounters.FirstOrDefault(c => c.Name == "errors.processing")?.Value ?? 0,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Error statistics
/// </summary>
public class ErrorStatistics
{
    public long TotalErrors { get; set; }
    public long ParsingErrors { get; set; }
    public long ValidationErrors { get; set; }
    public long DatabaseErrors { get; set; }
    public long QueueErrors { get; set; }
    public long SystemErrors { get; set; }
    public long CommunicationErrors { get; set; }
    public long ProcessingErrors { get; set; }
    public DateTime Timestamp { get; set; }
}
