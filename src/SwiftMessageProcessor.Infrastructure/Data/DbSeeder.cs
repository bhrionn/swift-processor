using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Data;

public class DbSeeder
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(SwiftMessageContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting database seeding...");

            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            if (_context.Database.GetPendingMigrations().Any())
            {
                _logger.LogInformation("Applying pending migrations...");
                await _context.Database.MigrateAsync();
            }

            // Seed initial audit entry
            if (!await _context.SystemAudit.AnyAsync())
            {
                _logger.LogInformation("Seeding initial system audit entries...");
                await SeedSystemAuditAsync();
            }

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private async Task SeedSystemAuditAsync()
    {
        var auditEntries = new[]
        {
            new SystemAuditEntry
            {
                Id = Guid.NewGuid(),
                EventType = "SystemInitialized",
                EventData = "{\"message\":\"Database initialized and seeded\",\"version\":\"1.0.0\"}",
                Timestamp = DateTime.UtcNow,
                UserId = "System",
                IpAddress = "127.0.0.1"
            }
        };

        await _context.SystemAudit.AddRangeAsync(auditEntries);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} system audit entries", auditEntries.Length);
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync()
    {
        return await _context.Database.GetPendingMigrationsAsync();
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync()
    {
        return await _context.Database.GetAppliedMigrationsAsync();
    }
}
