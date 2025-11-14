# SWIFT Message Processor - Monitoring and Alerting

This directory contains the monitoring and alerting infrastructure for the SWIFT Message Processor system.

## Overview

The monitoring stack includes:

- **Prometheus**: Metrics collection and alerting
- **Alertmanager**: Alert routing and notification management
- **Grafana**: Visualization and dashboards
- **Loki**: Log aggregation
- **Promtail**: Log shipping
- **Node Exporter**: System metrics
- **cAdvisor**: Container metrics

## Quick Start

### Start Monitoring Stack

```bash
# Start the monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Check status
docker-compose -f docker-compose.monitoring.yml ps
```

### Access Dashboards

- **Grafana**: http://localhost:3001 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Alertmanager**: http://localhost:9093
- **Loki**: http://localhost:3100

## Dashboards

### System Overview Dashboard

The System Overview dashboard provides a high-level view of the entire system:

- Service status (API and Console)
- Message processing rates
- Queue depths
- API response times
- Error rates
- Database performance
- Resource usage (CPU, Memory)
- Active alerts

### Message Processing Dashboard

The Message Processing dashboard focuses on message processing metrics:

- Processing throughput
- Success rate
- Message status distribution
- Processing duration percentiles
- Parse errors by type
- Queue operations
- Database operations
- Retry operations
- Test mode activity

## Metrics

### Application Metrics

The system exposes the following metrics:

#### Message Processing
- `swift_console_messages_processed_total`: Total messages processed
- `swift_console_messages_failed_total`: Total messages failed
- `swift_message_processing_duration_seconds`: Processing duration histogram

#### API Metrics
- `swift_api_requests_total`: Total API requests
- `swift_api_errors_total`: Total API errors
- `swift_api_request_duration_seconds`: Request duration histogram

#### Database Metrics
- `swift_database_operations_total`: Total database operations
- `swift_database_query_duration_seconds`: Query duration histogram
- `swift_database_connection_errors_total`: Connection errors

#### Queue Metrics
- `swift_queue_depth`: Current queue depth by queue name
- `swift_queue_operations_total`: Total queue operations
- `swift_queue_operation_duration_seconds`: Operation duration histogram
- `swift_queue_connection_errors_total`: Connection errors

#### Health Metrics
- `swift_health_check_status`: Health check status (1=healthy, 0.5=degraded, 0=unhealthy)
- `swift_signalr_active_connections`: Active SignalR connections
- `swift_test_mode_enabled`: Test mode status

### System Metrics

Node Exporter provides system-level metrics:
- CPU usage
- Memory usage
- Disk usage
- Network I/O

cAdvisor provides container-level metrics:
- Container CPU usage
- Container memory usage
- Container network I/O
- Container filesystem usage

## Alerts

### Alert Rules

The system includes comprehensive alert rules in `alerts.yml`:

#### Critical Alerts
- **ApiServiceDown**: API service is down for more than 1 minute
- **ConsoleServiceDown**: Console service is down for more than 1 minute
- **ConsoleProcessingStalled**: Message processing has stalled
- **DatabaseConnectionFailure**: Database connection failures detected
- **QueueConnectionFailure**: Queue connection failures detected
- **InterServiceCommunicationFailure**: Communication failures between services
- **DiskSpaceLow**: Less than 10% disk space remaining

#### Warning Alerts
- **ApiHighErrorRate**: API error rate exceeds 0.1 errors/sec
- **ApiHighResponseTime**: 95th percentile response time exceeds 2 seconds
- **ConsoleHighFailureRate**: Message failure rate exceeds 20%
- **DatabaseHighLatency**: 95th percentile query time exceeds 1 second
- **QueueDepthHigh**: Input queue depth exceeds 1000 messages
- **DeadLetterQueueGrowing**: Dead letter queue is growing
- **HighMemoryUsage**: Memory usage exceeds 90%
- **HighCPUUsage**: CPU usage exceeds 80%
- **ContainerHighMemory**: Container memory usage exceeds 90% of limit
- **ContainerRestarting**: Container is restarting frequently
- **HealthCheckFailing**: Health check is failing
- **MessageProcessingSlowdown**: Processing rate has dropped by more than 50%

### Alert Configuration

Alertmanager configuration is in `alertmanager-config.yml`:

- **Critical alerts**: Immediate notification, repeat every 5 minutes
- **Warning alerts**: 30-second wait, repeat every hour
- **Info alerts**: 5-minute wait, repeat every 24 hours

### Alert Routing

Alerts are routed based on:
- Severity (critical, warning, info)
- Service (api, console)
- Component (database, queue, system, etc.)

### Notification Channels

Configure notification channels in `alertmanager-config.yml`:

```yaml
receivers:
  - name: 'critical-alerts'
    # Email
    email_configs:
      - to: 'oncall@example.com'
    # Slack
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
        channel: '#critical-alerts'
    # PagerDuty
    pagerduty_configs:
      - service_key: 'YOUR_SERVICE_KEY'
```

## Health Reporting

The system includes automated health reporting:

### Health Check Endpoints

- `/health`: Overall health status
- `/health/ready`: Readiness check (infrastructure components)
- `/health/live`: Liveness check (application is running)

### Health Checks

- **database**: Database connectivity and query performance
- **queue**: Queue service availability
- **console-app**: Console application status

### Automated Reporting

Health checks run automatically every minute and:
- Log health status
- Update Prometheus metrics
- Trigger alerts on failures

## Troubleshooting

### Prometheus Not Scraping Metrics

1. Check if services are exposing metrics:
   ```bash
   curl http://localhost:8080/metrics
   ```

2. Check Prometheus targets:
   - Open http://localhost:9090/targets
   - Verify all targets are "UP"

3. Check service discovery:
   ```bash
   docker-compose -f docker-compose.monitoring.yml logs prometheus
   ```

### Alerts Not Firing

1. Check alert rules in Prometheus:
   - Open http://localhost:9090/alerts
   - Verify rules are loaded and evaluating

2. Check Alertmanager:
   - Open http://localhost:9093
   - Verify alerts are being received

3. Check webhook endpoint:
   ```bash
   curl -X POST http://localhost:8080/api/alerts/webhook \
     -H "Content-Type: application/json" \
     -d '{"alerts": []}'
   ```

### Grafana Dashboards Not Loading

1. Check datasource configuration:
   - Open Grafana → Configuration → Data Sources
   - Test Prometheus connection

2. Check dashboard provisioning:
   ```bash
   docker-compose -f docker-compose.monitoring.yml logs grafana
   ```

3. Manually import dashboards:
   - Copy JSON from `grafana/dashboards/*.json`
   - Import via Grafana UI

### High Resource Usage

1. Adjust retention periods:
   ```yaml
   # In prometheus.yml
   --storage.tsdb.retention.time=15d  # Reduce from 30d
   ```

2. Reduce scrape frequency:
   ```yaml
   # In prometheus.yml
   global:
     scrape_interval: 30s  # Increase from 15s
   ```

3. Limit log retention:
   ```yaml
   # In loki-config.yml
   retention_period: 168h  # 7 days
   ```

## Best Practices

### Metric Naming

Follow Prometheus naming conventions:
- Use `_total` suffix for counters
- Use `_seconds` suffix for durations
- Use descriptive names with units

### Alert Tuning

- Start with conservative thresholds
- Monitor false positive rate
- Adjust based on actual system behavior
- Document threshold rationale

### Dashboard Design

- Group related metrics
- Use consistent time ranges
- Include context (annotations, thresholds)
- Optimize query performance

### Performance

- Use recording rules for expensive queries
- Limit cardinality of labels
- Use appropriate scrape intervals
- Monitor Prometheus resource usage

## Integration with CI/CD

### Automated Testing

Test alert rules:
```bash
promtool check rules alerts.yml
```

Test Prometheus configuration:
```bash
promtool check config prometheus.yml
```

### Deployment

1. Update configurations in version control
2. Test in staging environment
3. Deploy to production
4. Verify metrics and alerts

## Additional Resources

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [Alertmanager Documentation](https://prometheus.io/docs/alerting/latest/alertmanager/)
- [Best Practices for Monitoring](https://prometheus.io/docs/practices/)
