using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.Infrastructure.HealthChecks;

/// <summary>
/// Health check for queue service connectivity and performance
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly IQueueService _queueService;
    private readonly ILogger<QueueHealthCheck> _logger;

    public QueueHealthCheck(
        IQueueService queueService,
        ILogger<QueueHealthCheck> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Check queue service health
            var isHealthy = await _queueService.IsHealthyAsync();
            
            if (!isHealthy)
            {
                return HealthCheckResult.Unhealthy("Queue service is not healthy");
            }

            // Get queue statistics
            var statistics = await _queueService.GetStatisticsAsync();
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var data = new Dictionary<string, object>
            {
                { "responseTimeMs", responseTime },
                { "messagesInQueue", statistics.MessagesInQueue },
                { "messagesProcessed", statistics.MessagesProcessed },
                { "messagesFailed", statistics.MessagesFailed },
                { "lastUpdated", statistics.LastUpdated }
            };

            // Check for concerning queue depths or failure rates
            if (statistics.MessagesFailed > 100)
            {
                _logger.LogWarning("Queue has {Count} failed messages", statistics.MessagesFailed);
                return HealthCheckResult.Degraded(
                    $"Queue has {statistics.MessagesFailed} failed messages",
                    data: data);
            }

            if (statistics.MessagesInQueue > 1000)
            {
                _logger.LogWarning("Queue has {Count} messages", statistics.MessagesInQueue);
                return HealthCheckResult.Degraded(
                    $"Queue has high message count ({statistics.MessagesInQueue})",
                    data: data);
            }

            // Consider degraded if response time is slow
            if (responseTime > 2000)
            {
                return HealthCheckResult.Degraded(
                    $"Queue service is responding slowly ({responseTime:F0}ms)",
                    data: data);
            }

            return HealthCheckResult.Healthy("Queue service is healthy and responsive", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue health check failed");
            return HealthCheckResult.Unhealthy("Queue health check failed", ex);
        }
    }
}
