using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for Application Performance Monitoring (APM) integration
/// Provides metrics collection and distributed tracing capabilities
/// </summary>
public class ApplicationPerformanceMonitoringService : IDisposable
{
    private readonly ILogger<ApplicationPerformanceMonitoringService> _logger;
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    
    // Counters
    private readonly Counter<long> _messagesProcessedCounter;
    private readonly Counter<long> _messagesFailedCounter;
    private readonly Counter<long> _apiRequestsCounter;
    private readonly Counter<long> _apiErrorsCounter;
    private readonly Counter<long> _databaseOperationsCounter;
    private readonly Counter<long> _queueOperationsCounter;
    private readonly Counter<long> _interserviceCallsCounter;
    
    // Histograms
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Histogram<double> _apiRequestDuration;
    private readonly Histogram<double> _databaseQueryDuration;
    private readonly Histogram<double> _queueOperationDuration;
    
    // Gauges (using ObservableGauge)
    private long _activeConnections;
    private long _queueDepthInput;
    private long _queueDepthCompleted;
    private long _queueDepthDeadLetter;
    private long _testModeEnabled;
    
    public ApplicationPerformanceMonitoringService(
        ILogger<ApplicationPerformanceMonitoringService> logger,
        string serviceName = "SwiftMessageProcessor")
    {
        _logger = logger;
        _meter = new Meter(serviceName, "1.0.0");
        _activitySource = new ActivitySource(serviceName);
        
        // Initialize counters
        _messagesProcessedCounter = _meter.CreateCounter<long>(
            "swift_console_messages_processed_total",
            description: "Total number of messages successfully processed");
            
        _messagesFailedCounter = _meter.CreateCounter<long>(
            "swift_console_messages_failed_total",
            description: "Total number of messages that failed processing");
            
        _apiRequestsCounter = _meter.CreateCounter<long>(
            "swift_api_requests_total",
            description: "Total number of API requests");
            
        _apiErrorsCounter = _meter.CreateCounter<long>(
            "swift_api_errors_total",
            description: "Total number of API errors");
            
        _databaseOperationsCounter = _meter.CreateCounter<long>(
            "swift_database_operations_total",
            description: "Total number of database operations");
            
        _queueOperationsCounter = _meter.CreateCounter<long>(
            "swift_queue_operations_total",
            description: "Total number of queue operations");
            
        _interserviceCallsCounter = _meter.CreateCounter<long>(
            "swift_interservice_calls_total",
            description: "Total number of inter-service communication calls");
        
        // Initialize histograms
        _messageProcessingDuration = _meter.CreateHistogram<double>(
            "swift_message_processing_duration_seconds",
            unit: "seconds",
            description: "Duration of message processing operations");
            
        _apiRequestDuration = _meter.CreateHistogram<double>(
            "swift_api_request_duration_seconds",
            unit: "seconds",
            description: "Duration of API requests");
            
        _databaseQueryDuration = _meter.CreateHistogram<double>(
            "swift_database_query_duration_seconds",
            unit: "seconds",
            description: "Duration of database queries");
            
        _queueOperationDuration = _meter.CreateHistogram<double>(
            "swift_queue_operation_duration_seconds",
            unit: "seconds",
            description: "Duration of queue operations");
        
        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "swift_signalr_active_connections",
            () => _activeConnections,
            description: "Number of active SignalR connections");
            
        _meter.CreateObservableGauge(
            "swift_queue_depth",
            () => new[]
            {
                new Measurement<long>(_queueDepthInput, new KeyValuePair<string, object?>("queue", "input")),
                new Measurement<long>(_queueDepthCompleted, new KeyValuePair<string, object?>("queue", "completed")),
                new Measurement<long>(_queueDepthDeadLetter, new KeyValuePair<string, object?>("queue", "dead_letter"))
            },
            description: "Current depth of message queues");
            
        _meter.CreateObservableGauge(
            "swift_test_mode_enabled",
            () => _testModeEnabled,
            description: "Whether test mode is currently enabled (1) or disabled (0)");
    }
    
    #region Message Processing Metrics
    
    public void RecordMessageProcessed(string messageType, double durationSeconds)
    {
        _messagesProcessedCounter.Add(1, new KeyValuePair<string, object?>("message_type", messageType));
        _messageProcessingDuration.Record(durationSeconds, new KeyValuePair<string, object?>("message_type", messageType));
    }
    
    public void RecordMessageFailed(string messageType, string errorType)
    {
        _messagesFailedCounter.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
    
    public Activity? StartMessageProcessingActivity(string messageId, string messageType)
    {
        var activity = _activitySource.StartActivity("ProcessMessage", ActivityKind.Internal);
        activity?.SetTag("message.id", messageId);
        activity?.SetTag("message.type", messageType);
        return activity;
    }
    
    #endregion
    
    #region API Metrics
    
    public void RecordApiRequest(string endpoint, string method, int statusCode, double durationSeconds)
    {
        _apiRequestsCounter.Add(1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("status_code", statusCode));
            
        _apiRequestDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method));
            
        if (statusCode >= 400)
        {
            _apiErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("status_code", statusCode));
        }
    }
    
    public Activity? StartApiRequestActivity(string endpoint, string method)
    {
        var activity = _activitySource.StartActivity("ApiRequest", ActivityKind.Server);
        activity?.SetTag("http.endpoint", endpoint);
        activity?.SetTag("http.method", method);
        return activity;
    }
    
    #endregion
    
    #region Database Metrics
    
    public void RecordDatabaseOperation(string operation, string table, double durationSeconds, bool success)
    {
        _databaseOperationsCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("table", table),
            new KeyValuePair<string, object?>("success", success));
            
        _databaseQueryDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("table", table));
    }
    
    public Activity? StartDatabaseActivity(string operation, string table)
    {
        var activity = _activitySource.StartActivity("DatabaseOperation", ActivityKind.Client);
        activity?.SetTag("db.operation", operation);
        activity?.SetTag("db.table", table);
        return activity;
    }
    
    #endregion
    
    #region Queue Metrics
    
    public void RecordQueueOperation(string operation, string queueName, double durationSeconds, bool success)
    {
        _queueOperationsCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("queue", queueName),
            new KeyValuePair<string, object?>("success", success));
            
        _queueOperationDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("queue", queueName));
    }
    
    public void UpdateQueueDepth(string queueName, long depth)
    {
        switch (queueName.ToLowerInvariant())
        {
            case "input":
                Interlocked.Exchange(ref _queueDepthInput, depth);
                break;
            case "completed":
                Interlocked.Exchange(ref _queueDepthCompleted, depth);
                break;
            case "dead_letter":
            case "deadletter":
                Interlocked.Exchange(ref _queueDepthDeadLetter, depth);
                break;
        }
    }
    
    #endregion
    
    #region Inter-Service Communication Metrics
    
    public void RecordInterServiceCall(string targetService, string operation, double durationSeconds, bool success)
    {
        _interserviceCallsCounter.Add(1,
            new KeyValuePair<string, object?>("target_service", targetService),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success));
    }
    
    public Activity? StartInterServiceActivity(string targetService, string operation)
    {
        var activity = _activitySource.StartActivity("InterServiceCall", ActivityKind.Client);
        activity?.SetTag("service.target", targetService);
        activity?.SetTag("service.operation", operation);
        return activity;
    }
    
    #endregion
    
    #region SignalR Metrics
    
    public void UpdateActiveConnections(long count)
    {
        Interlocked.Exchange(ref _activeConnections, count);
    }
    
    #endregion
    
    #region Test Mode Metrics
    
    public void SetTestModeEnabled(bool enabled)
    {
        Interlocked.Exchange(ref _testModeEnabled, enabled ? 1 : 0);
    }
    
    #endregion
    
    public void Dispose()
    {
        _meter?.Dispose();
        _activitySource?.Dispose();
    }
}
