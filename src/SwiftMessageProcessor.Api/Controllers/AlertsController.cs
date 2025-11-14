using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SwiftMessageProcessor.Api.Controllers;

/// <summary>
/// Controller for handling alert webhooks from Prometheus Alertmanager
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IHubContext<MessageHub> hubContext,
        ILogger<AlertsController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Webhook endpoint for Alertmanager notifications
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleAlertWebhook([FromBody] AlertmanagerWebhook webhook)
    {
        try
        {
            _logger.LogInformation("Received alert webhook with {AlertCount} alerts", webhook.Alerts?.Count ?? 0);

            if (webhook.Alerts == null || webhook.Alerts.Count == 0)
            {
                return Ok(new { message = "No alerts in webhook" });
            }

            foreach (var alert in webhook.Alerts)
            {
                var severity = alert.Labels?.GetValueOrDefault("severity", "unknown");
                var alertName = alert.Labels?.GetValueOrDefault("alertname", "Unknown Alert");
                var service = alert.Labels?.GetValueOrDefault("service", "unknown");

                _logger.LogWarning(
                    "Alert: {AlertName} | Severity: {Severity} | Service: {Service} | Status: {Status} | Description: {Description}",
                    alertName, severity, service, alert.Status, alert.Annotations?.GetValueOrDefault("description", ""));

                // Broadcast critical alerts to all connected clients
                if (severity == "critical" && alert.Status == "firing")
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveCriticalAlert", new
                    {
                        alertName,
                        severity,
                        service,
                        description = alert.Annotations?.GetValueOrDefault("description", ""),
                        timestamp = alert.StartsAt ?? DateTime.UtcNow
                    });
                }

                // Broadcast all alerts for monitoring dashboard
                await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
                {
                    alertName,
                    severity,
                    service,
                    status = alert.Status,
                    description = alert.Annotations?.GetValueOrDefault("description", ""),
                    summary = alert.Annotations?.GetValueOrDefault("summary", ""),
                    startsAt = alert.StartsAt,
                    endsAt = alert.EndsAt,
                    labels = alert.Labels
                });
            }

            return Ok(new { message = "Alerts processed successfully", count = webhook.Alerts.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process alert webhook");
            return StatusCode(500, new { error = "Failed to process alerts" });
        }
    }

    /// <summary>
    /// Gets current active alerts
    /// </summary>
    [HttpGet("active")]
    public ActionResult<List<AlertInfo>> GetActiveAlerts()
    {
        // This would typically query a database or cache of active alerts
        // For now, return empty list as alerts are pushed via webhook
        return Ok(new List<AlertInfo>());
    }
}

/// <summary>
/// Alertmanager webhook payload structure
/// </summary>
public class AlertmanagerWebhook
{
    public string? Version { get; set; }
    public string? GroupKey { get; set; }
    public string? Status { get; set; }
    public string? Receiver { get; set; }
    public Dictionary<string, string>? GroupLabels { get; set; }
    public Dictionary<string, string>? CommonLabels { get; set; }
    public Dictionary<string, string>? CommonAnnotations { get; set; }
    public string? ExternalURL { get; set; }
    public List<Alert>? Alerts { get; set; }
}

/// <summary>
/// Individual alert from Alertmanager
/// </summary>
public class Alert
{
    public string? Status { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public Dictionary<string, string>? Annotations { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? GeneratorURL { get; set; }
    public string? Fingerprint { get; set; }
}

/// <summary>
/// Alert information for API responses
/// </summary>
public class AlertInfo
{
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}
