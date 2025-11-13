using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Services;

public class QueueServiceFactory : IQueueServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueueServiceFactory> _logger;

    public QueueServiceFactory(IServiceProvider serviceProvider, ILogger<QueueServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IQueueService CreateQueueService(string provider)
    {
        _logger.LogInformation("Creating queue service for provider: {Provider}", provider);

        return provider.ToLowerInvariant() switch
        {
            "inmemory" or "local" => _serviceProvider.GetRequiredService<LocalQueueService>(),
            "amazonsqs" or "sqs" => _serviceProvider.GetRequiredService<AmazonSQSService>(),
            _ => throw new InvalidOperationException($"Unsupported queue provider: {provider}")
        };
    }
}