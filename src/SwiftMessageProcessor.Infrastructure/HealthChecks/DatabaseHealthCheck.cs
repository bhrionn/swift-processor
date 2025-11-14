using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Infrastructure.Data;

namespace SwiftMessageProcessor.Infrastructure.HealthChecks;

/// <summary>
/// Health check for database connectivity and performance
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        SwiftMessageContext context,
        ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Test database connectivity with a simple query
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Check if migrations are applied
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
            var hasPendingMigrations = pendingMigrations.Any();

            // Measure query performance
            var messageCount = await _context.Messages.CountAsync(cancellationToken);
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var data = new Dictionary<string, object>
            {
                { "messageCount", messageCount },
                { "responseTimeMs", responseTime },
                { "hasPendingMigrations", hasPendingMigrations },
                { "databaseProvider", _context.Database.ProviderName ?? "Unknown" }
            };

            if (hasPendingMigrations)
            {
                data["pendingMigrations"] = string.Join(", ", pendingMigrations);
                _logger.LogWarning("Database has pending migrations: {Migrations}", string.Join(", ", pendingMigrations));
            }

            // Consider degraded if response time is slow
            if (responseTime > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database is responding slowly ({responseTime:F0}ms)",
                    data: data);
            }

            return HealthCheckResult.Healthy("Database is healthy and responsive", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}
