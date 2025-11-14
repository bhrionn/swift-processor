using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Data;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Deployment;

/// <summary>
/// Tests for database migrations and seeding
/// </summary>
public class DatabaseMigrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testDbPath;

    public DatabaseMigrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_migration_{Guid.NewGuid()}.db");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["Database:ConnectionString"] = $"Data Source={_testDbPath}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        
        services.AddDbContext<SwiftMessageContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath}"));

        services.AddScoped<DatabaseMigrationService>();
        services.AddScoped<DbSeeder>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DatabaseMigrationService_ShouldApplyMigrationsSuccessfully()
    {
        // Arrange
        var migrationService = _serviceProvider.GetRequiredService<DatabaseMigrationService>();

        // Act
        await migrationService.MigrateAsync();

        // Assert
        var context = _serviceProvider.GetRequiredService<SwiftMessageContext>();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().NotBeEmpty("Migrations should be applied");
    }

    [Fact]
    public async Task DatabaseMigrationService_ShouldCreateRequiredTables()
    {
        // Arrange
        var migrationService = _serviceProvider.GetRequiredService<DatabaseMigrationService>();
        await migrationService.MigrateAsync();

        // Act
        var context = _serviceProvider.GetRequiredService<SwiftMessageContext>();
        var canConnectToMessages = await context.Database.CanConnectAsync();

        // Assert
        canConnectToMessages.Should().BeTrue("Should be able to connect to database");
        
        // Verify tables exist by querying them
        var messagesCount = await context.Messages.CountAsync();
        messagesCount.Should().BeGreaterThanOrEqualTo(0, "Messages table should exist");

        var auditCount = await context.SystemAudit.CountAsync();
        auditCount.Should().BeGreaterThanOrEqualTo(0, "SystemAudit table should exist");
    }

    [Fact]
    public async Task DbSeeder_ShouldSeedDatabaseSuccessfully()
    {
        // Arrange
        var migrationService = _serviceProvider.GetRequiredService<DatabaseMigrationService>();
        await migrationService.MigrateAsync();

        var seeder = _serviceProvider.GetRequiredService<DbSeeder>();

        // Act
        await seeder.SeedAsync();

        // Assert - Seeding should complete without errors
        var context = _serviceProvider.GetRequiredService<SwiftMessageContext>();
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue("Database should be accessible after seeding");
    }

    [Fact]
    public async Task DatabaseMigration_ShouldBeIdempotent()
    {
        // Arrange
        var migrationService = _serviceProvider.GetRequiredService<DatabaseMigrationService>();

        // Act - Apply migrations twice
        await migrationService.MigrateAsync();
        await migrationService.MigrateAsync();

        // Assert - Should not throw and migrations should still be applied
        var context = _serviceProvider.GetRequiredService<SwiftMessageContext>();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().NotBeEmpty("Migrations should remain applied");
    }

    [Fact]
    public async Task DatabaseContext_ShouldHaveCorrectIndexes()
    {
        // Arrange
        var migrationService = _serviceProvider.GetRequiredService<DatabaseMigrationService>();
        await migrationService.MigrateAsync();

        // Act
        var context = _serviceProvider.GetRequiredService<SwiftMessageContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        // Query SQLite to check for indexes
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='Messages'";
        
        var indexes = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain(idx => idx.Contains("Status"), "Should have index on Status");
        indexes.Should().Contain(idx => idx.Contains("MessageType"), "Should have index on MessageType");
    }

    [Fact]
    public async Task DatabaseMigration_ShouldHandleConnectionFailureGracefully()
    {
        // Arrange - Use an invalid connection string that will definitely fail
        var invalidPath = "/nonexistent/readonly/path/database.db";
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["Database:ConnectionString"] = $"Data Source={invalidPath};Mode=ReadOnly"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Critical));
        services.Configure<DatabaseOptions>(invalidConfig.GetSection("Database"));
        
        services.AddDbContext<SwiftMessageContext>(options =>
            options.UseSqlite($"Data Source={invalidPath};Mode=ReadOnly"));

        services.AddScoped<DatabaseMigrationService>();

        var provider = services.BuildServiceProvider();
        var migrationService = provider.GetRequiredService<DatabaseMigrationService>();

        // Act & Assert - Should throw when trying to migrate to a non-existent readonly database
        var exception = await Record.ExceptionAsync(async () => await migrationService.MigrateAsync());
        exception.Should().NotBeNull("Migration should fail with invalid connection");
    }

    [Fact]
    public void MigrationFiles_ShouldExist()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var migrationsPath = Path.Combine(repoRoot, "src", "SwiftMessageProcessor.Infrastructure", "Migrations");

        // Assert
        Directory.Exists(migrationsPath).Should().BeTrue("Migrations directory should exist");
        
        var migrationFiles = Directory.GetFiles(migrationsPath, "*.cs");
        migrationFiles.Should().NotBeEmpty("Should have migration files");
        migrationFiles.Should().Contain(f => f.Contains("InitialCreate"), "Should have InitialCreate migration");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "SwiftMessageProcessor.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find repository root");
    }
}
