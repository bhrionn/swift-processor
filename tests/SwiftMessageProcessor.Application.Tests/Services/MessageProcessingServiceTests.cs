using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Parsers;
using SwiftMessageProcessor.Infrastructure.Configuration;
using Xunit;

namespace SwiftMessageProcessor.Application.Tests.Services;

public class MessageProcessingServiceTests
{
    private readonly ILogger<MessageProcessingService> _logger;
    private readonly IQueueService _queueService;
    private readonly IMessageRepository _messageRepository;
    private readonly ISwiftMessageParser<MT103Message> _mt103Parser;
    private readonly IOptions<QueueOptions> _queueOptions;
    private readonly IOptions<ProcessingOptions> _processingOptions;
    private readonly MessageProcessingService _service;

    public MessageProcessingServiceTests()
    {
        _logger = Substitute.For<ILogger<MessageProcessingService>>();
        _queueService = Substitute.For<IQueueService>();
        _messageRepository = Substitute.For<IMessageRepository>();
        _mt103Parser = Substitute.For<ISwiftMessageParser<MT103Message>>();
        
        _queueOptions = Options.Create(new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "input-test",
                CompletedQueue = "completed-test",
                DeadLetterQueue = "dlq-test"
            }
        });
        
        _processingOptions = Options.Create(new ProcessingOptions
        {
            MaxConcurrentMessages = 10,
            MessageProcessingTimeoutSeconds = 60,
            RetryAttempts = 3,
            RetryDelaySeconds = 1
        });

        _service = new MessageProcessingService(
            _logger,
            _queueService,
            _messageRepository,
            _mt103Parser,
            _queueOptions,
            _processingOptions);
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidMessage_ReturnsSuccess()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();
        var parsedMessage = new MT103Message
        {
            MessageType = "MT103",
            TransactionReference = "TEST123",
            Amount = 1000.00m,
            Currency = "USD",
            ValueDate = DateTime.UtcNow,
            BankOperationCode = "CRED"
        };

        _mt103Parser.ParseAsync(rawMessage).Returns(parsedMessage);
        _mt103Parser.ValidateAsync(parsedMessage).Returns(ValidationResult.Success);
        _messageRepository.SaveMessageAsync(Arg.Any<ProcessedMessage>()).Returns(Guid.NewGuid());

        // Act
        var result = await _service.ProcessMessageAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();
        result.Message!.Status.Should().Be(MessageStatus.Processed);

        await _mt103Parser.Received(1).ParseAsync(rawMessage);
        await _mt103Parser.Received(1).ValidateAsync(parsedMessage);
        await _messageRepository.Received(1).SaveMessageAsync(Arg.Any<ProcessedMessage>());
        await _queueService.Received(1).SendMessageAsync(_queueOptions.Value.Settings.CompletedQueue, rawMessage);
    }

    [Fact]
    public async Task ProcessMessageAsync_ParsingFails_ReturnsFailureAndSendsToDeadLetterQueue()
    {
        // Arrange
        var rawMessage = "INVALID MESSAGE";
        _mt103Parser.ParseAsync(rawMessage).Returns<MT103Message>(x => throw new SwiftParsingException("Invalid format"));

        // Act
        var result = await _service.ProcessMessageAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Parsing failed");

        await _mt103Parser.Received(3).ParseAsync(rawMessage); // Should retry 3 times
        await _queueService.Received(1).SendMessageAsync(
            _queueOptions.Value.Settings.DeadLetterQueue, 
            Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidationFails_ReturnsFailureAndSendsToDeadLetterQueue()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();
        var parsedMessage = new MT103Message
        {
            MessageType = "MT103",
            TransactionReference = "TEST123"
        };

        _mt103Parser.ParseAsync(rawMessage).Returns(parsedMessage);
        _mt103Parser.ValidateAsync(parsedMessage).Returns(new ValidationResult("Missing required fields"));

        // Act
        var result = await _service.ProcessMessageAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Validation failed");

        await _messageRepository.DidNotReceive().SaveMessageAsync(Arg.Any<ProcessedMessage>());
        await _queueService.Received(1).SendMessageAsync(
            _queueOptions.Value.Settings.DeadLetterQueue, 
            Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessMessageAsync_DatabaseSaveFails_ReturnsFailureAndSendsToDeadLetterQueue()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();
        var parsedMessage = new MT103Message
        {
            MessageType = "MT103",
            TransactionReference = "TEST123",
            Amount = 1000.00m,
            Currency = "USD",
            ValueDate = DateTime.UtcNow,
            BankOperationCode = "CRED"
        };

        _mt103Parser.ParseAsync(rawMessage).Returns(parsedMessage);
        _mt103Parser.ValidateAsync(parsedMessage).Returns(ValidationResult.Success);
        _messageRepository.SaveMessageAsync(Arg.Any<ProcessedMessage>())
            .Returns<Guid>(x => throw new Exception("Database connection failed"));

        // Act
        var result = await _service.ProcessMessageAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database save failed");

        await _messageRepository.Received(3).SaveMessageAsync(Arg.Any<ProcessedMessage>()); // Should retry 3 times
        await _queueService.Received(1).SendMessageAsync(
            _queueOptions.Value.Settings.DeadLetterQueue, 
            Arg.Any<string>());
    }

    [Fact]
    public async Task StartProcessingAsync_SetsStatusToProcessing()
    {
        // Act
        await _service.StartProcessingAsync(CancellationToken.None);
        var status = await _service.GetSystemStatusAsync();

        // Assert
        status.IsProcessing.Should().BeTrue();
        status.Status.Should().Be("Processing");
    }

    [Fact]
    public async Task StopProcessingAsync_SetsStatusToStopped()
    {
        // Arrange
        await _service.StartProcessingAsync(CancellationToken.None);

        // Act
        await _service.StopProcessingAsync();
        var status = await _service.GetSystemStatusAsync();

        // Assert
        status.IsProcessing.Should().BeFalse();
        status.Status.Should().Be("Stopped");
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsMetrics()
    {
        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalMessagesProcessed.Should().Be(0);
        metrics.TotalMessagesFailed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessMessageAsync_UpdatesMetrics()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();
        var parsedMessage = new MT103Message
        {
            MessageType = "MT103",
            TransactionReference = "TEST123",
            Amount = 1000.00m,
            Currency = "USD",
            ValueDate = DateTime.UtcNow,
            BankOperationCode = "CRED"
        };

        _mt103Parser.ParseAsync(rawMessage).Returns(parsedMessage);
        _mt103Parser.ValidateAsync(parsedMessage).Returns(ValidationResult.Success);
        _messageRepository.SaveMessageAsync(Arg.Any<ProcessedMessage>()).Returns(Guid.NewGuid());

        // Act
        await _service.ProcessMessageAsync(rawMessage);
        var metrics = await _service.GetMetricsAsync();

        // Assert
        metrics.TotalMessagesProcessed.Should().Be(1);
        metrics.TotalMessagesFailed.Should().Be(0);
        metrics.AverageProcessingTimeMs.Should().BeGreaterThan(0);
    }

    private static string GetValidMT103Message()
    {
        return @"{1:F01BANKBEBBAXXX0000000000}{2:I103BANKDEFFXXXXN}{4:
:20:REFERENCE12345
:23B:CRED
:32A:231103USD1000,00
:50K:/12345678
ORDERING CUSTOMER NAME
ADDRESS LINE 1
:59:/98765432
BENEFICIARY NAME
ADDRESS LINE 1
-}";
    }
}
