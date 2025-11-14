using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class AuditLoggingServiceTests : IDisposable
{
    private readonly SwiftMessageContext _context;
    private readonly IAuditLoggingService _auditService;
    private readonly ILogger<AuditLoggingService> _logger;

    public AuditLoggingServiceTests()
    {
        var options = new DbContextOptionsBuilder<SwiftMessageContext>()
            .UseInMemoryDatabase(databaseName: $"AuditTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new SwiftMessageContext(options);
        _logger = Substitute.For<ILogger<AuditLoggingService>>();
        _auditService = new AuditLoggingService(_context, _logger);
    }

    [Fact]
    public async Task LogEventAsync_ValidEvent_SavesAuditEntry()
    {
        // Arrange
        var eventType = "TestEvent";
        var eventData = "Test data";
        var userId = "user123";
        var ipAddress = "192.168.1.1";

        // Act
        await _auditService.LogEventAsync(eventType, eventData, userId, ipAddress);

        // Assert
        var auditEntry = await _context.SystemAudit.FirstOrDefaultAsync();
        Assert.NotNull(auditEntry);
        Assert.Equal(eventType, auditEntry.EventType);
        Assert.Equal(eventData, auditEntry.EventData);
        Assert.Equal(userId, auditEntry.UserId);
        Assert.Equal(ipAddress, auditEntry.IpAddress);
    }

    [Fact]
    public async Task LogAdministrativeActionAsync_ValidAction_SavesAuditEntry()
    {
        // Arrange
        var action = "RestartProcessor";
        var details = "Processor restarted by admin";
        var userId = "admin123";

        // Act
        await _auditService.LogAdministrativeActionAsync(action, details, userId);

        // Assert
        var auditEntry = await _context.SystemAudit.FirstOrDefaultAsync();
        Assert.NotNull(auditEntry);
        Assert.Equal("AdministrativeAction", auditEntry.EventType);
        Assert.Contains(action, auditEntry.EventData);
    }

    [Fact]
    public async Task LogSecurityEventAsync_ValidEvent_SavesAuditEntry()
    {
        // Arrange
        var eventType = "UnauthorizedAccess";
        var details = "Invalid API key attempt";
        var ipAddress = "10.0.0.1";

        // Act
        await _auditService.LogSecurityEventAsync(eventType, details, null, ipAddress);

        // Assert
        var auditEntry = await _context.SystemAudit.FirstOrDefaultAsync();
        Assert.NotNull(auditEntry);
        Assert.Equal("SecurityEvent", auditEntry.EventType);
        Assert.Contains(eventType, auditEntry.EventData);
    }

    [Fact]
    public async Task GetAuditTrailAsync_WithDateFilter_ReturnsFilteredEntries()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-1);
        await _auditService.LogEventAsync("Event1", "Data1");
        await Task.Delay(10); // Ensure different timestamps
        await _auditService.LogEventAsync("Event2", "Data2");

        // Act
        var auditTrail = await _auditService.GetAuditTrailAsync(fromDate: fromDate);

        // Assert
        Assert.Equal(2, auditTrail.Count());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
