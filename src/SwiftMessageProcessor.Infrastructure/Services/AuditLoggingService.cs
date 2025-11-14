using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for logging audit events for compliance and security
/// </summary>
public interface IAuditLoggingService
{
    Task LogEventAsync(string eventType, string eventData, string? userId = null, string? ipAddress = null);
    Task LogAdministrativeActionAsync(string action, string details, string? userId = null, string? ipAddress = null);
    Task LogSecurityEventAsync(string eventType, string details, string? userId = null, string? ipAddress = null);
    Task<IEnumerable<SystemAuditEntry>> GetAuditTrailAsync(DateTime? fromDate = null, DateTime? toDate = null, string? eventType = null);
}

public class AuditLoggingService : IAuditLoggingService
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<AuditLoggingService> _logger;

    public AuditLoggingService(SwiftMessageContext context, ILogger<AuditLoggingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogEventAsync(string eventType, string eventData, string? userId = null, string? ipAddress = null)
    {
        try
        {
            var auditEntry = new SystemAuditEntry
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                EventData = eventData,
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                IpAddress = ipAddress
            };

            _context.SystemAudit.Add(auditEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Audit event logged: {EventType} by {UserId} from {IpAddress}",
                eventType, userId ?? "System", ipAddress ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: {EventType}", eventType);
            // Don't throw - audit logging failure shouldn't break the application
        }
    }

    public async Task LogAdministrativeActionAsync(string action, string details, string? userId = null, string? ipAddress = null)
    {
        var eventData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Action = action,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        await LogEventAsync("AdministrativeAction", eventData, userId, ipAddress);
    }

    public async Task LogSecurityEventAsync(string eventType, string details, string? userId = null, string? ipAddress = null)
    {
        var eventData = System.Text.Json.JsonSerializer.Serialize(new
        {
            SecurityEventType = eventType,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        await LogEventAsync("SecurityEvent", eventData, userId, ipAddress);

        // Log security events at warning level for visibility
        _logger.LogWarning("Security event: {EventType} - {Details} by {UserId} from {IpAddress}",
            eventType, details, userId ?? "Unknown", ipAddress ?? "Unknown");
    }

    public async Task<IEnumerable<SystemAuditEntry>> GetAuditTrailAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? eventType = null)
    {
        var query = _context.SystemAudit.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(a => a.EventType == eventType);

        return await Task.FromResult(query.OrderByDescending(a => a.Timestamp).ToList());
    }
}
