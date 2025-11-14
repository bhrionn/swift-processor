# Monitoring and APM Integration Guide

This document describes how to integrate the Application Performance Monitoring (APM) and alerting system with your application code.

## Overview

The monitoring system provides:
- **Metrics Collection**: Counters, histograms, and gauges for application metrics
- **Distributed Tracing**: Activity tracking across service boundaries
- **Health Reporting**: Automated health checks and status reporting
- **Alerting**: Real-time alerts for critical system failures

## Using ApplicationPerformanceMonitoringService

### Dependency Injection

The `ApplicationPerformanceMonitoringService` is registered as a singleton and can be injected into any service:

```csharp
public class MessageProcessingService
{
    private readonly ApplicationPerformanceMonitoringService _apmService;
    
    public MessageProcessingService(ApplicationPerformanceMonitoringService apmService)
    {
        _apmService = apmService;
    }
}
```

### Recording Message Processing Metrics

```csharp
public async Task<ProcessingResult> ProcessMessageAsync(string rawMessage)
{
    var messageId = Guid.NewGuid();
    var stopwatch = Stopwatch.StartNew();
    
    // Start distributed tracing activity
    using var activity = _apmService.StartMessageProcessingActivity(
        messageId.ToString(), 
        "MT103");
    
    try
    {
        // Process message...
        var result = await ProcessInternalAsync(rawMessage);
        
        stopwatch.Stop();
        
        // Record successful processing
        _apmService.RecordMessageProcessed(
            "MT103", 
            stopwatch.Elapsed.TotalSeconds);
        
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        
        // Record failed processing
        _apmService.RecordMessageFailed(
            "MT103", 
            ex.GetType().Name);
        
        // Add error details to activity
        activity?.SetTag("error", true);
        activity?.SetTag("error.message", ex.Message);
        
        throw;
    }
}
```

### Recording API Metrics

API metrics are automatically collected by the `PrometheusMetricsMiddleware`, but you can also record custom metrics:

```csharp
public async Task<IActionResult> GetMessages([FromQuery] MessageFilter filter)
{
    var stopwatch = Stopwatch.StartNew();
    
    using var activity = _apmService.StartApiRequestActivity(
        "/api/messages", 
        "GET");
    
    try
    {
        var messages = await _messageService.GetMessagesAsync(filter);
        
        stopwatch.Stop();
        _apmService.RecordApiRequest(
            "/api/messages", 
            "GET", 
            200, 
            stopwatch.Elapsed.TotalSeconds);
        
        return Ok(messages);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _apmService.RecordApiRequest(
            "/api/messages", 
            "GET", 
            500, 
            stopwatch.Elapsed.TotalSeconds);
        
        throw;
    }
}
```

### Recording Database Operations

```csharp
public async Task<ProcessedMessage?> GetByIdAsync(Guid id)
{
    var stopwatch = Stopwatch.StartNew();
    
    using var activity = _apmService.StartDatabaseActivity(
        "SELECT", 
        "Messages");
    
    try
    {
        var message = await _context.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
        
        stopwatch.Stop();
        _apmService.RecordDatabaseOperation(
            "SELECT", 
            "Messages", 
            stopwatch.Elapsed.TotalSeconds, 
            success: true);
        
        return message;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _apmService.RecordDatabaseOperation(
            "SELECT", 
            "Messages", 
            stopwatch.Elapsed.TotalSeconds, 
            success: false);
        
        activity?.SetTag("error", true);
        activity?.SetTag("error.message", ex.Message);
        
        throw;
    }
}
```

### Recording Queue Operations

```csharp
public async Task<string?> ReceiveMessageAsync(string queueName)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var message = await ReceiveInternalAsync(queueName);
        
        stopwatch.Stop();
        _apmService.RecordQueueOperation(
            "receive", 
            queueName, 
            stopwatch.Elapsed.TotalSeconds, 
            success: message != null);
        
        // Update queue depth metric
        var depth = await GetQueueDepthAsync(queueName);
        _apmService.UpdateQueueDepth(queueName, depth);
        
        return message;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _apmService.RecordQueueOperation(
            "receive", 
            queueName, 
            stopwatch.Elapsed.TotalSeconds, 
            success: false);
        
        throw;
    }
}
```

### Recording Inter-Service Communication

```csharp
public async Task<bool> SendCommandToConsoleAsync(string command)
{
    var stopwatch = Stopwatch.StartNew();
    
    using var activity = _apmService.StartInterServiceActivity(
        "console", 
        command);
    
    try
    {
        await SendCommandInternalAsync(command);
        
        stopwatch.Stop();
        _apmService.RecordInterServiceCall(
            "console", 
            command, 
            stopwatch.Elapsed.TotalSeconds, 
            success: true);
        
        return true;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _apmService.RecordInterServiceCall(
            "console", 
            command, 
            stopwatch.Elapsed.TotalSeconds, 
            success: false);
        
        activity?.SetTag("error", true);
        activity?.SetTag("error.message", ex.Message);
        
        return false;
    }
}
```

### Updating SignalR Connection Count

```csharp
public class MessageHub : Hub
{
    private readonly ApplicationPerformanceMonitoringService _apmService;
    private static long _connectionCount = 0;
    
    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectionCount);
        _apmService.UpdateActiveConnections(_connectionCount);
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectionCount);
        _apmService.UpdateActiveConnections(_connectionCount);
        
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Setting Test Mode Status

```csharp
public async Task EnableTestModeAsync()
{
    _testModeEnabled = true;
    _apmService.SetTestModeEnabled(true);
    
    _logger.LogInformation("Test mode enabled");
}

public async Task DisableTestModeAsync()
{
    _testModeEnabled = false;
    _apmService.SetTestModeEnabled(false);
    
    _logger.LogInformation("Test mode disabled");
}
```

## Using SystemHealthReportingService

### Starting Health Reporting

Health reporting is automatically started in `Program.cs`:

```csharp
var healthReportingService = app.Services
    .GetRequiredService<SystemHealthReportingService>();
    
healthReportingService.StartHealthReporting(TimeSpan.FromMinutes(1));
```

### Manual Health Checks

```csharp
public class SystemController : ControllerBase
{
    private readonly SystemHealthReportingService _healthReportingService;
    
    [HttpGet("health/detailed")]
    public async Task<ActionResult<DetailedHealthStatus>> GetDetailedHealth()
    {
        var healthStatus = await _healthReportingService
            .GetDetailedHealthStatusAsync();
        
        return Ok(healthStatus);
    }
    
    [HttpGet("health/summary")]
    public async Task<IActionResult> GetHealthSummary()
    {
        var summary = await _healthReportingService
            .GenerateHealthSummaryAsync();
        
        return Content(summary, "application/json");
    }
}
```

## Handling Alerts

### Receiving Alerts via SignalR

Frontend clients can subscribe to alerts:

```typescript
// Connect to SignalR hub
const connection = new HubConnectionBuilder()
    .withUrl('/hubs/messages')
    .build();

// Subscribe to critical alerts
connection.on('ReceiveCriticalAlert', (alert) => {
    console.error('Critical Alert:', alert);
    showNotification({
        title: alert.alertName,
        message: alert.description,
        severity: 'critical'
    });
});

// Subscribe to all alerts
connection.on('ReceiveAlert', (alert) => {
    console.log('Alert:', alert);
    updateAlertsDashboard(alert);
});

await connection.start();
```

### Custom Alert Handling

You can implement custom alert handlers:

```csharp
public class CustomAlertHandler : IHostedService
{
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<CustomAlertHandler> _logger;
    
    public async Task HandleAlertAsync(Alert alert)
    {
        // Custom logic for specific alerts
        if (alert.Labels?.GetValueOrDefault("alertname") == "ConsoleProcessingStalled")
        {
            _logger.LogCritical("Processing has stalled! Attempting automatic recovery...");
            
            // Attempt recovery
            await AttemptRecoveryAsync();
            
            // Notify administrators
            await NotifyAdministratorsAsync(alert);
        }
    }
}
```

## Metrics Endpoint

The `/metrics` endpoint exposes metrics in Prometheus format:

```bash
# Query metrics
curl http://localhost:8080/metrics

# Example output:
# HELP swift_console_messages_processed_total Counter metric
# TYPE swift_console_messages_processed_total counter
# swift_console_messages_processed_total{message_type="MT103"} 1234

# HELP swift_api_request_duration_seconds Timer metric
# TYPE swift_api_request_duration_seconds histogram
# swift_api_request_duration_seconds_count{endpoint="/api/messages",method="GET"} 567
# swift_api_request_duration_seconds_sum{endpoint="/api/messages",method="GET"} 12.34
```

## Best Practices

### 1. Use Distributed Tracing

Always use activities for operations that span multiple components:

```csharp
using var activity = _apmService.StartMessageProcessingActivity(messageId, messageType);
// ... perform operations ...
// Activity is automatically disposed and recorded
```

### 2. Record Both Success and Failure

Always record metrics for both successful and failed operations:

```csharp
try
{
    await PerformOperationAsync();
    _apmService.RecordOperation("operation", success: true);
}
catch (Exception ex)
{
    _apmService.RecordOperation("operation", success: false);
    throw;
}
```

### 3. Use Meaningful Labels

Add context to metrics with labels:

```csharp
_apmService.RecordMessageProcessed("MT103", duration);
_apmService.RecordMessageFailed("MT103", "ValidationError");
```

### 4. Keep Cardinality Low

Avoid high-cardinality labels (e.g., user IDs, message IDs):

```csharp
// Bad: High cardinality
_apmService.RecordOperation("process", tags: new() { ["message_id"] = messageId });

// Good: Low cardinality
_apmService.RecordOperation("process", tags: new() { ["message_type"] = "MT103" });
```

### 5. Monitor Critical Paths

Focus on monitoring critical paths in your application:

- Message processing pipeline
- API endpoints
- Database operations
- Queue operations
- Inter-service communication

### 6. Set Appropriate Alert Thresholds

Start with conservative thresholds and adjust based on actual behavior:

```yaml
# Start conservative
- alert: HighErrorRate
  expr: rate(errors_total[5m]) > 0.1  # 10% error rate

# Adjust based on baseline
- alert: HighErrorRate
  expr: rate(errors_total[5m]) > 0.01  # 1% error rate
```

## Troubleshooting

### Metrics Not Appearing

1. Verify service is registered:
   ```csharp
   services.AddSingleton<ApplicationPerformanceMonitoringService>();
   ```

2. Check metrics endpoint:
   ```bash
   curl http://localhost:8080/metrics
   ```

3. Verify Prometheus is scraping:
   - Open http://localhost:9090/targets
   - Check service status

### Activities Not Traced

1. Ensure Activity is disposed:
   ```csharp
   using var activity = _apmService.StartActivity(...);
   ```

2. Check Activity listener is configured
3. Verify distributed tracing is enabled

### Alerts Not Firing

1. Check alert rules are loaded in Prometheus
2. Verify Alertmanager is configured
3. Check webhook endpoint is accessible
4. Review alert inhibition rules

## Additional Resources

- [Prometheus Best Practices](https://prometheus.io/docs/practices/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/)
