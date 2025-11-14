using Microsoft.Extensions.Diagnostics.HealthChecks;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.Api.Services;

/// <summary>
/// Health check for the console application
/// </summary>
public class ConsoleAppHealthCheck : IHealthCheck
{
    private readonly IProcessCommunicationService _communicationService;
    private readonly ILogger<ConsoleAppHealthCheck> _logger;

    public ConsoleAppHealthCheck(
        IProcessCommunicationService communicationService,
        ILogger<ConsoleAppHealthCheck> logger)
    {
        _communicationService = communicationService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _communicationService.IsConsoleAppHealthyAsync(cancellationToken);

            if (isHealthy)
            {
                var status = await _communicationService.GetStatusAsync(cancellationToken);
                var data = new Dictionary<string, object>
                {
                    { "isRunning", status.IsRunning },
                    { "isProcessing", status.IsProcessing },
                    { "messagesProcessed", status.MessagesProcessed },
                    { "messagesFailed", status.MessagesFailed },
                    { "lastProcessedAt", status.LastProcessedAt },
                    { "statusUpdatedAt", status.StatusUpdatedAt }
                };

                return HealthCheckResult.Healthy("Console application is healthy and responsive", data);
            }

            return HealthCheckResult.Unhealthy("Console application is not responding or not running");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Console application health check failed");
            return HealthCheckResult.Unhealthy("Console application health check failed", ex);
        }
    }
}
