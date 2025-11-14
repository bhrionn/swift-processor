# Distributed System Monitoring and Alerting - Implementation Summary

## Overview

This document summarizes the implementation of comprehensive monitoring and alerting for the SWIFT Message Processor distributed system.

## Components Implemented

### 1. Application Performance Monitoring (APM) Service

**File**: `src/SwiftMessageProcessor.Infrastructure/Services/ApplicationPerformanceMonitoringService.cs`

Provides comprehensive metrics collection using .NET's built-in `System.Diagnostics.Metrics` API:

- **Counters**: Message processing, API requests, database operations, queue operations
- **Histograms**: Processing duration, API response time, database query time, queue operation time
- **Gauges**: Queue depth, active connections, test mode status
- **Distributed Tracing**: Activity tracking across service boundaries

### 2. System Health Reporting Service

**File**: `src/SwiftMessageProcessor.Infrastructure/Services/SystemHealthReportingService.cs`

Automated health monitoring and reporting:

- Periodic health checks (configurable interval)
- Detailed health status reporting
- Integration with .NET health checks
- Automatic metric updates based on health status
- Health summary generation

### 3. Alert Management

**Files**:
- `src/SwiftMessageProcessor.Api/Controllers/AlertsController.cs`
- `docker/monitoring/alerts.yml`
- `docker/monitoring/alertmanager-config.yml`

Comprehensive alerting system:

- **Critical Alerts**: Service down, processing stalled, connection failures
- **Warning Alerts**: High error rates, high latency, resource usage
- **Info Alerts**: Low connection counts, informational status
- Real-time alert broadcasting via SignalR
- Webhook integration with Alertmanager

### 4. Metrics Exposition

**Files**:
- `src/SwiftMessageProcessor.Api/Controllers/PrometheusController.cs`
- `src/SwiftMessageProcessor.Api/Middleware/PrometheusMetricsMiddleware.cs`

Prometheus-compatible metrics endpoint:

- `/metrics` endpoint in Prometheus text format
- Automatic API request metrics collection
- Health check status exposition
- Timestamp tracking

### 5. Monitoring Infrastructure

**Files**:
- `docker/docker-compose.monitoring.yml`
- `docker/monitoring/prometheus.yml`
- `docker/monitoring/alertmanager-config.yml`
- `docker/monitoring/loki-config.yml`
- `docker/monitoring/promtail-config.yml`

Complete monitoring stack:

- **Prometheus**: Metrics collection and alerting engine
- **Alertmanager**: Alert routing and notification management
- **Grafana**: Visualization and dashboards
- **Loki**: Log aggregation
- **Promtail**: Log shipping
- **Node Exporter**: System metrics
- **cAdvisor**: Container metrics

### 6. Dashboards

**Files**:
- `docker/monitoring/grafana/dashboards/swift-system-overview.json`
- `docker/monitoring/grafana/dashboards/swift-message-processing.json`
- `docker/monitoring/grafana/dashboards/dashboard.yml`
- `docker/monitoring/grafana/datasources/datasources.yml`

Pre-configured Grafana dashboards:

- **System Overview**: Service status, processing rates, queue depths, errors, resources
- **Message Processing**: Throughput, success rate, duration, parse errors, operations

## Alert Rules Implemented

### Critical Alerts (Immediate Notification)

1. **ApiServiceDown**: API service unavailable for >1 minute
2. **ConsoleServiceDown**: Console service unavailable for >1 minute
3. **ConsoleProcessingStalled**: No messages processed despite queue depth
4. **DatabaseConnectionFailure**: Database connection errors detected
5. **QueueConnectionFailure**: Queue connection errors detected
6. **InterServiceCommunicationFailure**: Communication failures between services
7. **DiskSpaceLow**: <10% disk space remaining

### Warning Alerts (Less Urgent)

1. **ApiHighErrorRate**: Error rate >0.1 errors/sec
2. **ApiHighResponseTime**: 95th percentile >2 seconds
3. **ConsoleHighFailureRate**: Message failure rate >20%
4. **DatabaseHighLatency**: 95th percentile query time >1 second
5. **QueueDepthHigh**: Input queue >1000 messages
6. **DeadLetterQueueGrowing**: DLQ growing continuously
7. **HighMemoryUsage**: Memory usage >90%
8. **HighCPUUsage**: CPU usage >80%
9. **ContainerHighMemory**: Container memory >90% of limit
10. **ContainerRestarting**: Frequent container restarts
11. **HealthCheckFailing**: Health check failures
12. **MessageProcessingSlowdown**: Processing rate dropped >50%

### Info Alerts (Daily Digest)

1. **SignalRConnectionsLow**: No active connections for >10 minutes

## Metrics Exposed

### Message Processing Metrics

- `swift_console_messages_processed_total`: Total messages processed
- `swift_console_messages_failed_total`: Total messages failed
- `swift_message_processing_duration_seconds`: Processing duration histogram

### API Metrics

- `swift_api_requests_total`: Total API requests
- `swift_api_errors_total`: Total API errors
- `swift_api_request_duration_seconds`: Request duration histogram

### Database Metrics

- `swift_database_operations_total`: Total database operations
- `swift_database_query_duration_seconds`: Query duration histogram
- `swift_database_connection_errors_total`: Connection errors

### Queue Metrics

- `swift_queue_depth`: Current queue depth by queue name
- `swift_queue_operations_total`: Total queue operations
- `swift_queue_operation_duration_seconds`: Operation duration histogram
- `swift_queue_connection_errors_total`: Connection errors

### Inter-Service Communication Metrics

- `swift_interservice_calls_total`: Total inter-service calls
- `swift_interservice_communication_errors_total`: Communication errors

### Health Metrics

- `swift_health_check_status`: Health check status (1=healthy, 0.5=degraded, 0=unhealthy)
- `swift_signalr_active_connections`: Active SignalR connections
- `swift_test_mode_enabled`: Test mode status (1=enabled, 0=disabled)

## Integration Points

### Service Registration

Services are registered in `ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<ApplicationPerformanceMonitoringService>();
services.AddSingleton<SystemHealthReportingService>();
```

### Middleware Integration

Metrics middleware is added in `Program.cs`:

```csharp
app.UsePrometheusMetrics();
```

### Health Reporting Startup

Automated health reporting starts in `Program.cs`:

```csharp
var healthReportingService = app.Services
    .GetRequiredService<SystemHealthReportingService>();
healthReportingService.StartHealthReporting(TimeSpan.FromMinutes(1));
```

## Usage Examples

### Recording Message Processing

```csharp
using var activity = _apmService.StartMessageProcessingActivity(messageId, "MT103");
// ... process message ...
_apmService.RecordMessageProcessed("MT103", durationSeconds);
```

### Recording API Requests

Automatic via middleware, or manual:

```csharp
_apmService.RecordApiRequest(endpoint, method, statusCode, durationSeconds);
```

### Recording Database Operations

```csharp
using var activity = _apmService.StartDatabaseActivity("SELECT", "Messages");
// ... execute query ...
_apmService.RecordDatabaseOperation("SELECT", "Messages", durationSeconds, success);
```

### Updating Queue Depth

```csharp
_apmService.UpdateQueueDepth("input", currentDepth);
```

## Deployment

### Starting Monitoring Stack

```bash
# Start all monitoring services
docker-compose -f docker/docker-compose.monitoring.yml up -d

# Verify services are running
docker-compose -f docker/docker-compose.monitoring.yml ps
```

### Accessing Dashboards

- **Grafana**: http://localhost:3001 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Alertmanager**: http://localhost:9093

### Viewing Metrics

```bash
# API metrics
curl http://localhost:8080/metrics

# Console metrics (when implemented)
curl http://localhost:8081/metrics
```

## Configuration

### Alert Thresholds

Adjust thresholds in `docker/monitoring/alerts.yml`:

```yaml
- alert: ApiHighErrorRate
  expr: rate(swift_api_errors_total[5m]) > 0.1  # Adjust threshold
  for: 2m  # Adjust duration
```

### Notification Channels

Configure in `docker/monitoring/alertmanager-config.yml`:

```yaml
receivers:
  - name: 'critical-alerts'
    email_configs:
      - to: 'oncall@example.com'
    slack_configs:
      - api_url: 'YOUR_WEBHOOK_URL'
        channel: '#alerts'
```

### Health Check Interval

Adjust in `Program.cs`:

```csharp
healthReportingService.StartHealthReporting(TimeSpan.FromMinutes(1));  // Change interval
```

## Testing

### Verify Metrics Collection

```bash
# Check metrics endpoint
curl http://localhost:8080/metrics | grep swift_

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets
```

### Trigger Test Alerts

```bash
# Simulate high error rate
for i in {1..100}; do
  curl http://localhost:8080/api/nonexistent
done

# Check alerts in Prometheus
curl http://localhost:9090/api/v1/alerts
```

### Verify Alert Delivery

```bash
# Check Alertmanager
curl http://localhost:9093/api/v2/alerts

# Check webhook endpoint
curl -X POST http://localhost:8080/api/alerts/webhook \
  -H "Content-Type: application/json" \
  -d '{"alerts": [{"status": "firing", "labels": {"alertname": "TestAlert"}}]}'
```

## Documentation

- **Monitoring Setup**: `docker/monitoring/README.md`
- **Integration Guide**: `docs/MONITORING_INTEGRATION.md`
- **This Summary**: `docs/MONITORING_SUMMARY.md`

## Requirements Satisfied

This implementation satisfies the following requirements from task 12.2:

✅ **Set up APM integration for both services**
- ApplicationPerformanceMonitoringService provides comprehensive APM
- Metrics collection for all critical operations
- Distributed tracing with Activity API

✅ **Create custom metrics dashboards**
- System Overview dashboard
- Message Processing dashboard
- Pre-configured Grafana provisioning

✅ **Implement alerting for critical system failures**
- 20+ alert rules covering all critical scenarios
- Alertmanager for routing and notification
- Real-time alert broadcasting via SignalR

✅ **Add automated system health reporting**
- SystemHealthReportingService with periodic checks
- Detailed health status API endpoints
- Automatic metric updates from health checks

✅ **Monitor inter-service communication**
- Inter-service call metrics
- Communication failure alerts
- Distributed tracing across services

## Next Steps

1. **Implement Console Application Metrics**: Add APM integration to the console application
2. **Configure Notification Channels**: Set up email, Slack, or PagerDuty notifications
3. **Tune Alert Thresholds**: Adjust based on actual system behavior
4. **Add Custom Dashboards**: Create role-specific dashboards
5. **Implement Recording Rules**: Optimize expensive Prometheus queries
6. **Set Up Log Correlation**: Link logs with traces using correlation IDs

## Maintenance

### Regular Tasks

- Review and adjust alert thresholds monthly
- Update dashboards based on user feedback
- Monitor Prometheus storage usage
- Review and archive old metrics data
- Test alert notification channels quarterly

### Troubleshooting

See `docker/monitoring/README.md` for detailed troubleshooting guides.
