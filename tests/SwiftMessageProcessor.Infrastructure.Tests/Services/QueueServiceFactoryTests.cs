using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class QueueServiceFactoryTests
{
    [Theory]
    [InlineData("inmemory")]
    [InlineData("local")]
    [InlineData("InMemory")]
    [InlineData("LOCAL")]
    public void CreateQueueService_LocalProvider_ShouldReturnLocalQueueService(string provider)
    {
        // Arrange
        var services = new ServiceCollection();
        var queueOptions = new QueueOptions
        {
            Provider = provider,
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
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<QueueServiceFactory>>();
        var factory = new QueueServiceFactory(serviceProvider, logger);

        // Act
        var queueService = factory.CreateQueueService(provider);

        // Assert
        queueService.Should().NotBeNull();
        queueService.Should().BeOfType<LocalQueueService>();
    }

    [Theory]
    [InlineData("amazonsqs")]
    [InlineData("sqs")]
    [InlineData("AmazonSQS")]
    [InlineData("SQS")]
    public void CreateQueueService_SQSProvider_ShouldReturnAmazonSQSService(string provider)
    {
        // Arrange
        var services = new ServiceCollection();
        var queueOptions = new QueueOptions
        {
            Provider = provider,
            Region = "us-east-1",
            Settings = new QueueSettings
            {
                InputQueue = "test-input",
                CompletedQueue = "test-completed",
                DeadLetterQueue = "test-dlq"
            }
        };
        
        services.AddSingleton(Options.Create(queueOptions));
        services.AddLogging();
        
        // Mock IAmazonSQS
        var mockSqsClient = Substitute.For<IAmazonSQS>();
        services.AddSingleton(mockSqsClient);
        services.AddScoped<AmazonSQSService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<QueueServiceFactory>>();
        var factory = new QueueServiceFactory(serviceProvider, logger);

        // Act
        var queueService = factory.CreateQueueService(provider);

        // Assert
        queueService.Should().NotBeNull();
        queueService.Should().BeOfType<AmazonSQSService>();
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("redis")]
    [InlineData("rabbitmq")]
    public void CreateQueueService_UnsupportedProvider_ShouldThrowInvalidOperationException(string provider)
    {
        // Arrange
        var services = new ServiceCollection();
        var queueOptions = new QueueOptions
        {
            Provider = provider,
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
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<QueueServiceFactory>>();
        var factory = new QueueServiceFactory(serviceProvider, logger);

        // Act & Assert
        factory.Invoking(f => f.CreateQueueService(provider))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"Unsupported queue provider: {provider}");
    }
}