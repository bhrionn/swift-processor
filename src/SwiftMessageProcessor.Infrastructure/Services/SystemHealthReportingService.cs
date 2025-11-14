using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for automated system health reporting and monitoring
/// </summary>
public class SystemHealthReportingService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<SystemHealthReportingService> _logger;
    private readonly ApplicationPerformanceMonitoringService _apmService;
    private Timer? _healthCheckTimer;
    private HealthReport? _lastHealthReport;

    public SystemHealthReportingService(
        HealthCheckService healthCheckService,
        ILogger<SystemHealthReportingService> logger,
        ApplicationPerformanceMonitoringService apmService)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
        _apmService = apmService;
    }

    /// <summary>
    /// Starts automated health reporting
    /// </summary>
    public void StartHealthReporting(TimeSpan interval)
    {
        _logger.LogInformation("Starting automated health reporting with interval {Interval}", interval);
        
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            TimeSpan.Zero,
            interval);
    }

    /// <summary>
    /// Stops automated health reporting
    /// </summary>
    public void StopHealthReporting()
    {
        _logger.LogInformation("Stopping automated health reporting");
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
    }

    /// <summary>
    /// Performs a health check and reports results
    /// </summary>
    public async Task<HealthReport> PerformHealthCheckAsync()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            _lastHealthReport = healthReport;

            // Log health status
            LogHealthReport(healthReport);

            // Update metrics
            UpdateHealthMetrics(healthReport);

            // Check for degraded or unhealthy status
            if (healthReport.Status != HealthStatus.Healthy)
            {
                _logger.LogWarning(
                    "System health is {Status}. {UnhealthyCount} checks are unhealthy, {DegradedCount} are degraded",
                    healthReport.Status,
                    healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy),
                    healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Degraded));
            }

            return healthReport;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform health check");
            throw;
        }
    }

    /// <summary>
    /// Gets the last health report
    /// </summary>
    public HealthReport? GetLastHealthReport() => _lastHealthReport;

    /// <summary>
    /// Gets detailed health status
    /// </summary>
    public async Task<DetailedHealthStatus> GetDetailedHealthStatusAsync()
    {
        var healthReport = await PerformHealthCheckAsync();
        
        return new DetailedHealthStatus
        {
            OverallStatus = healthReport.Status.ToString(),
            TotalDuration = healthReport.TotalDuration,
            Checks = healthReport.Entries.Select(e => new HealthCheckDetail
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description ?? string.Empty,
                Duration = e.Value.Duration,
                Exception = e.Value.Exception?.Message,
                Data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString() ?? string.Empty)
            }).ToList(),
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Logs health report details
    /// </summary>
    private void LogHealthReport(HealthReport healthReport)
    {
        var status = healthReport.Status;
        var duration = healthReport.TotalDuration.TotalMilliseconds;

        if (status == HealthStatus.Healthy)
        {
            _logger.LogInformation(
                "Health check completed: {Status} in {Duration}ms",
                status, duration);
        }
        else
        {
            _logger.LogWarning(
                "Health check completed: {Status} in {Duration}ms",
                status, duration);

            foreach (var entry in healthReport.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
            {
                _logger.LogWarning(
                    "Health check '{CheckName}' is {Status}: {Description}",
                    entry.Key,
                    entry.Value.Status,
                    entry.Value.Description ?? "No description");

                if (entry.Value.Exception != null)
                {
                    _logger.LogError(
                        entry.Value.Exception,
                        "Health check '{CheckName}' failed with exception",
                        entry.Key);
                }
            }
        }
    }

    /// <summary>
    /// Updates APM metrics based on health report
    /// </summary>
    private void UpdateHealthMetrics(HealthReport healthReport)
    {
        // This would integrate with the APM service to expose health check metrics
        // For Prometheus, we'd expose these as gauges
        foreach (var entry in healthReport.Entries)
        {
            var statusValue = entry.Value.Status switch
            {
                HealthStatus.Healthy => 1,
                HealthStatus.Degraded => 0.5,
                HealthStatus.Unhealthy => 0,
                _ => 0
            };

            // In a real implementation, this would be exposed as a Prometheus metric
            _logger.LogDebug(
                "Health check metric: swift_health_check_status{{check=\"{CheckName}\"}} = {Value}",
                entry.Key, statusValue);
        }
    }

    /// <summary>
    /// Generates a health summary report
    /// </summary>
    public async Task<string> GenerateHealthSummaryAsync()
    {
        var healthStatus = await GetDetailedHealthStatusAsync();
        
        var summary = new
        {
            timestamp = healthStatus.Timestamp,
            overall_status = healthStatus.OverallStatus,
            total_duration_ms = healthStatus.TotalDuration.TotalMilliseconds,
            checks = healthStatus.Checks.Select(c => new
            {
                name = c.Name,
                status = c.Status,
                duration_ms = c.Duration.TotalMilliseconds,
                description = c.Description
            })
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Detailed health status information
/// </summary>
public class DetailedHealthStatus
{
    public string OverallStatus { get; set; } = string.Empty;
    public TimeSpan TotalDuration { get; set; }
    public List<HealthCheckDetail> Checks { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Individual health check detail
/// </summary>
public class HealthCheckDetail
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? Exception { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}
