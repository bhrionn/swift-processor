using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Data;

public class SwiftMessageContextFactory : IDesignTimeDbContextFactory<SwiftMessageContext>
{
    public SwiftMessageContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SwiftMessageContext>();
        
        var provider = configuration["Database:Provider"] ?? "SQLite";
        var connectionString = configuration["Database:ConnectionString"] ?? "Data Source=messages.db";

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString);
                break;
            case "sqlserver":
                optionsBuilder.UseSqlServer(connectionString);
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }

        return new SwiftMessageContext(optionsBuilder.Options);
    }
}