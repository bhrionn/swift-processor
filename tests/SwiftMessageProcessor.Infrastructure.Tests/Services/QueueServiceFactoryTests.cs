using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class QueueServiceFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueServiceFactory _factory;

    public QueueServiceFactoryTests()
    {
        var services = new ServiceCollection();
        
        // Configure queue options
        var queueOptions = new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "test-input",
                CompletedQueue = "test-completed",
                DeadLetterQueue = "test-dlq"
            }
        };
        
        services.AddSingleton(Options.Create(queueOptions));
        services.AddLogging();
        services.AddSingleton<LocalQueueService>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        var logger = _serviceProvider.GetRequiredService<ILogger<QueueServiceFactory>>();
        _factory = new QueueServiceFactory(_serviceProvider, logger);
    }

    [Theory]
    [InlineData("inmemory")]
    [InlineData("local")]
    [InlineData("InMemory")]
    [InlineData("LOCAL")]
    public void CreateQueueService_LocalProvider_ShouldReturnLocalQueueService(string provider)
    {
        // Act
        var queueService = _factory.CreateQueueService(provider);

        // Assert
        queueService.Should().NotBeNull();
        queueService.Should().BeOfType<LocalQueueService>();
    }

    [Theory]
    [InlineData("amazonsqs")]
    [InlineData("sqs")]
    [InlineData("AmazonSQS")]
    [InlineData("SQS")]
    public void CreateQueueService_SQSProvider_ShouldThrowNotImplementedException(string provider)
    {
        // Act & Assert
        _factory.Invoking(f => f.CreateQueueService(provider))
            .Should().Throw<NotImplementedException>()
            .WithMessage("AWS SQS service will be implemented in task 4.2");
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("redis")]
    [InlineData("rabbitmq")]
    public void CreateQueueService_UnsupportedProvider_ShouldThrowInvalidOperationException(string provider)
    {
        // Act & Assert
        _factory.Invoking(f => f.CreateQueueService(provider))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"Unsupported queue provider: {provider}");
    }
}