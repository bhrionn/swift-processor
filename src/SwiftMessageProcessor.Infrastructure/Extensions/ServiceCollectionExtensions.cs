using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Repositories;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure database options
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        
        // Configure queue options
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));
        services.AddSingleton<IValidateOptions<QueueOptions>, QueueOptionsValidator>();
        
        // Add DbContext
        services.AddDbContext<SwiftMessageContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            
            switch (databaseOptions.Provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(databaseOptions.ConnectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(typeof(SwiftMessageContext).Assembly.FullName);
                    });
                    break;
                    
                case "sqlserver":
                    options.UseSqlServer(databaseOptions.ConnectionString, sqlServerOptions =>
                    {
                        sqlServerOptions.MigrationsAssembly(typeof(SwiftMessageContext).Assembly.FullName);
                        sqlServerOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
                    break;
                    
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {databaseOptions.Provider}");
            }
            
            // Enable sensitive data logging in development
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }
        });
        
        // Register repositories
        services.AddScoped<IMessageRepository, MessageRepository>();
        
        // Register queue services
        services.AddSingleton<LocalQueueService>();
        services.AddScoped<IQueueServiceFactory, QueueServiceFactory>();
        services.AddScoped<IQueueService>(provider =>
        {
            var factory = provider.GetRequiredService<IQueueServiceFactory>();
            var queueOptions = provider.GetRequiredService<IOptions<QueueOptions>>().Value;
            return factory.CreateQueueService(queueOptions.Provider);
        });
        
        return services;
    }
}

public class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();
        
        if (string.IsNullOrEmpty(options.Provider))
            failures.Add("Database provider must be specified");
            
        if (string.IsNullOrEmpty(options.ConnectionString))
            failures.Add("Database connection string must be specified");
            
        var supportedProviders = new[] { "sqlite", "sqlserver" };
        if (!supportedProviders.Contains(options.Provider.ToLowerInvariant()))
            failures.Add($"Database provider '{options.Provider}' is not supported. Supported providers: {string.Join(", ", supportedProviders)}");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}