using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SwiftMessageProcessor.Infrastructure.Data;

public class DatabaseMigrationService
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(SwiftMessageContext context, ILogger<DatabaseMigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Applies all pending migrations to the database
    /// </summary>
    public async Task<bool> MigrateAsync()
    {
        try
        {
            _logger.LogInformation("Checking for pending migrations...");

            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            var pendingList = pendingMigrations.ToList();

            if (!pendingList.Any())
            {
                _logger.LogInformation("No pending migrations found");
                return true;
            }

            _logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                pendingList.Count, 
                string.Join(", ", pendingList));

            await _context.Database.MigrateAsync();

            _logger.LogInformation("Successfully applied all pending migrations");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            return false;
        }
    }

    /// <summary>
    /// Gets the list of applied migrations
    /// </summary>
    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync()
    {
        try
        {
            return await _context.Database.GetAppliedMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve applied migrations");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Gets the list of pending migrations
    /// </summary>
    public async Task<IEnumerable<string>> GetPendingMigrationsAsync()
    {
        try
        {
            return await _context.Database.GetPendingMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending migrations");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Checks if the database can be connected to
    /// </summary>
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

    /// <summary>
    /// Ensures the database is created (for development/testing)
    /// </summary>
    public async Task<bool> EnsureCreatedAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created...");
            var created = await _context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                _logger.LogInformation("Database was created");
            }
            else
            {
                _logger.LogInformation("Database already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database creation");
            return false;
        }
    }

    /// <summary>
    /// Deletes the database (for development/testing only)
    /// </summary>
    public async Task<bool> DeleteDatabaseAsync()
    {
        try
        {
            _logger.LogWarning("Deleting database...");
            var deleted = await _context.Database.EnsureDeletedAsync();
            
            if (deleted)
            {
                _logger.LogWarning("Database was deleted");
            }
            else
            {
                _logger.LogInformation("Database did not exist");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete database");
            return false;
        }
    }

    /// <summary>
    /// Gets database migration status information
    /// </summary>
    public async Task<MigrationStatus> GetMigrationStatusAsync()
    {
        try
        {
            var canConnect = await CanConnectAsync();
            if (!canConnect)
            {
                return new MigrationStatus
                {
                    CanConnect = false,
                    Message = "Cannot connect to database"
                };
            }

            var applied = (await GetAppliedMigrationsAsync()).ToList();
            var pending = (await GetPendingMigrationsAsync()).ToList();

            return new MigrationStatus
            {
                CanConnect = true,
                AppliedMigrations = applied,
                PendingMigrations = pending,
                IsUpToDate = !pending.Any(),
                Message = pending.Any() 
                    ? $"{pending.Count} pending migration(s)" 
                    : "Database is up to date"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get migration status");
            return new MigrationStatus
            {
                CanConnect = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}

public class MigrationStatus
{
    public bool CanConnect { get; set; }
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public bool IsUpToDate { get; set; }
    public string Message { get; set; } = string.Empty;
}
