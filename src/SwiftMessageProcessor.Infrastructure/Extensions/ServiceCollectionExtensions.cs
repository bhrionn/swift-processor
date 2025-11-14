using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.HealthChecks;
using SwiftMessageProcessor.Infrastructure.Repositories;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure database options with validation
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();
        
        // Configure queue options with validation
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));
        services.AddSingleton<IValidateOptions<QueueOptions>, QueueOptionsValidator>();
        services.AddOptions<QueueOptions>()
            .Bind(configuration.GetSection(QueueOptions.SectionName))
            .ValidateOnStart();
        
        // Configure processing options with validation
        services.Configure<ProcessingOptions>(configuration.GetSection(ProcessingOptions.SectionName));
        services.AddSingleton<IValidateOptions<ProcessingOptions>, ProcessingOptionsValidator>();
        services.AddOptions<ProcessingOptions>()
            .Bind(configuration.GetSection(ProcessingOptions.SectionName))
            .ValidateOnStart();
        
        // Configure communication options with validation
        services.Configure<CommunicationOptions>(configuration.GetSection(CommunicationOptions.SectionName));
        services.AddSingleton<IValidateOptions<CommunicationOptions>, CommunicationOptionsValidator>();
        services.AddOptions<CommunicationOptions>()
            .Bind(configuration.GetSection(CommunicationOptions.SectionName))
            .ValidateOnStart();
        
        // Configure test mode options with validation
        services.Configure<TestModeOptions>(configuration.GetSection(TestModeOptions.SectionName));
        services.AddSingleton<IValidateOptions<TestModeOptions>, TestModeOptionsValidator>();
        services.AddOptions<TestModeOptions>()
            .Bind(configuration.GetSection(TestModeOptions.SectionName))
            .ValidateOnStart();
        
        // Register configuration validator
        services.AddSingleton<ConfigurationValidator>();
        services.AddHostedService<ConfigurationStartupValidator>();
        
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
                    
                case "inmemory":
                    options.UseInMemoryDatabase(databaseOptions.ConnectionString);
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
        
        // Register AWS SQS client
        services.AddAWSService<IAmazonSQS>(configuration.GetAWSOptions());
        
        // Register queue services
        services.AddSingleton<LocalQueueService>();
        services.AddScoped<AmazonSQSService>();
        services.AddScoped<IQueueServiceFactory, QueueServiceFactory>();
        services.AddScoped<IQueueService>(provider =>
        {
            var factory = provider.GetRequiredService<IQueueServiceFactory>();
            var queueOptions = provider.GetRequiredService<IOptions<QueueOptions>>().Value;
            return factory.CreateQueueService(queueOptions.Provider);
        });
        
        // Register inter-process communication service
        services.AddSingleton<IProcessCommunicationService, FileBasedCommunicationService>();
        
        // Register monitoring and metrics services
        services.AddSingleton<MetricsCollectionService>();
        services.AddSingleton<ErrorLoggingService>();
        
        // Register security services
        services.AddSingleton<IDataEncryptionService, DataEncryptionService>();
        services.AddScoped<IAuditLoggingService, AuditLoggingService>();
        services.AddScoped<IDataRetentionService, DataRetentionService>();
        
        // Register health checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database", "infrastructure" })
            .AddCheck<QueueHealthCheck>("queue", tags: new[] { "queue", "infrastructure" });
        
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
            
        var supportedProviders = new[] { "sqlite", "sqlserver", "inmemory" };
        if (!supportedProviders.Contains(options.Provider.ToLowerInvariant()))
            failures.Add($"Database provider '{options.Provider}' is not supported. Supported providers: {string.Join(", ", supportedProviders)}");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}