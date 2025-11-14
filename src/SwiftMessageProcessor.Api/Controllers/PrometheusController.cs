using Microsoft.AspNetCore.Mvc;
using System.Text;
using SwiftMessageProcessor.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SwiftMessageProcessor.Api.Controllers;

/// <summary>
/// Controller for exposing Prometheus metrics
/// </summary>
[ApiController]
public class PrometheusController : ControllerBase
{
    private readonly MetricsCollectionService _metricsService;
    private readonly SystemHealthReportingService _healthReportingService;
    private readonly ILogger<PrometheusController> _logger;

    public PrometheusController(
        MetricsCollectionService metricsService,
        SystemHealthReportingService healthReportingService,
        ILogger<PrometheusController> logger)
    {
        _metricsService = metricsService;
        _healthReportingService = healthReportingService;
        _logger = logger;
    }

    /// <summary>
    /// Prometheus metrics endpoint
    /// </summary>
    [HttpGet("/metrics")]
    [Produces("text/plain")]
    public IActionResult GetMetrics()
    {
        try
        {
            var sb = new StringBuilder();
            
            // Get metrics snapshot
            var snapshot = _metricsService.GetSnapshot();
            
            // Export counters
            foreach (var counter in snapshot.Counters)
            {
                sb.AppendLine($"# HELP {counter.Name} Counter metric");
                sb.AppendLine($"# TYPE {counter.Name} counter");
                
                if (counter.Tags.Any())
                {
                    var tags = string.Join(",", counter.Tags.Select(t => $"{t.Key}=\"{t.Value}\""));
                    sb.AppendLine($"{counter.Name}{{{tags}}} {counter.Value}");
                }
                else
                {
                    sb.AppendLine($"{counter.Name} {counter.Value}");
                }
            }
            
            // Export timers as histograms
            foreach (var timer in snapshot.Timers)
            {
                sb.AppendLine($"# HELP {timer.Name} Timer metric");
                sb.AppendLine($"# TYPE {timer.Name} histogram");
                
                var tagsStr = timer.Tags.Any() 
                    ? "{" + string.Join(",", timer.Tags.Select(t => $"{t.Key}=\"{t.Value}\"")) + "}"
                    : "";
                
                sb.AppendLine($"{timer.Name}_count{tagsStr} {timer.Count}");
                sb.AppendLine($"{timer.Name}_sum{tagsStr} {timer.Sum}");
                sb.AppendLine($"{timer.Name}_min{tagsStr} {timer.Min}");
                sb.AppendLine($"{timer.Name}_max{tagsStr} {timer.Max}");
            }
            
            // Export health check status
            var healthReport = _healthReportingService.GetLastHealthReport();
            if (healthReport != null)
            {
                sb.AppendLine("# HELP swift_health_check_status Health check status (1=healthy, 0.5=degraded, 0=unhealthy)");
                sb.AppendLine("# TYPE swift_health_check_status gauge");
                
                foreach (var entry in healthReport.Entries)
                {
                    var statusValue = entry.Value.Status switch
                    {
                        HealthStatus.Healthy => 1,
                        HealthStatus.Degraded => 0.5,
                        HealthStatus.Unhealthy => 0,
                        _ => 0
                    };
                    
                    var service = entry.Key.Contains("Api") ? "api" : "console";
                    sb.AppendLine($"swift_health_check_status{{check=\"{entry.Key}\",service=\"{service}\"}} {statusValue}");
                }
            }
            
            // Export timestamp
            sb.AppendLine($"# HELP swift_metrics_timestamp_seconds Timestamp of metrics collection");
            sb.AppendLine($"# TYPE swift_metrics_timestamp_seconds gauge");
            sb.AppendLine($"swift_metrics_timestamp_seconds {new DateTimeOffset(snapshot.Timestamp).ToUnixTimeSeconds()}");
            
            return Content(sb.ToString(), "text/plain; version=0.0.4");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Prometheus metrics");
            return StatusCode(500, "Failed to generate metrics");
        }
    }
}
