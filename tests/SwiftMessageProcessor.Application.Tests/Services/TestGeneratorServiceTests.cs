using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Configuration;
using Xunit;

namespace SwiftMessageProcessor.Application.Tests.Services;

public class TestGeneratorServiceTests
{
    private readonly IQueueService _queueService;
    private readonly ILogger<TestGeneratorService> _logger;
    private readonly TestModeOptions _testModeOptions;
    private readonly QueueOptions _queueOptions;
    private readonly TestGeneratorService _testGenerator;

    public TestGeneratorServiceTests()
    {
        _queueService = Substitute.For<IQueueService>();
        _logger = Substitute.For<ILogger<TestGeneratorService>>();
        
        _testModeOptions = new TestModeOptions
        {
            Enabled = true,
            GenerationInterval = TimeSpan.FromSeconds(1),
            ValidMessagePercentage = 80,
            BatchSize = 1
        };
        
        _queueOptions = new QueueOptions
        {
            Provider = "InMemory",
            Settings = new QueueSettings
            {
                InputQueue = "test-input-queue",
                CompletedQueue = "test-completed-queue",
                DeadLetterQueue = "test-dlq"
            }
        };

        _testGenerator = new TestGeneratorService(
            _queueService,
            _logger,
            Options.Create(_testModeOptions),
            Options.Create(_queueOptions)
        );
    }

    [Fact]
    public async Task GenerateValidMessageAsync_ShouldReturnValidMT103Message()
    {
        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.Should().NotBeNull();
        message.MessageType.Should().Be("MT103");
        message.TransactionReference.Should().NotBeNullOrEmpty();
        message.BankOperationCode.Should().NotBeNullOrEmpty();
        message.Currency.Should().NotBeNullOrEmpty();
        message.Amount.Should().BeGreaterThan(0);
        message.OrderingCustomer.Should().NotBeNull();
        message.BeneficiaryCustomer.Should().NotBeNull();
        
        // Validate the message passes SWIFT validation
        var validationResult = message.Validate();
        validationResult.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Theory]
    [InlineData(ValidationError.MissingTransactionReference)]
    [InlineData(ValidationError.InvalidAmount)]
    [InlineData(ValidationError.MissingCurrency)]
    [InlineData(ValidationError.InvalidBankCode)]
    [InlineData(ValidationError.MissingBeneficiary)]
    public async Task GenerateInvalidMessageAsync_ShouldReturnInvalidMessage(ValidationError errorType)
    {
        // Act
        var message = await _testGenerator.GenerateInvalidMessageAsync(errorType);

        // Assert
        message.Should().NotBeNull();
        message.MessageType.Should().Be("MT103");
        
        // Validate the message fails SWIFT validation
        var validationResult = message.Validate();
        validationResult.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public async Task GenerateRawMessageAsync_ShouldReturnValidSwiftFormat()
    {
        // Arrange
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Act
        var rawMessage = await _testGenerator.GenerateRawMessageAsync(message);

        // Assert
        rawMessage.Should().NotBeNullOrEmpty();
        rawMessage.Should().Contain("{1:F01"); // Block 1
        rawMessage.Should().Contain("{2:I103"); // Block 2
        rawMessage.Should().Contain("{4:"); // Block 4
        rawMessage.Should().Contain($":20:{message.TransactionReference}");
        rawMessage.Should().Contain($":23B:{message.BankOperationCode}");
        rawMessage.Should().Contain($":32A:");
        rawMessage.Should().Contain($":50K:"); // Ordering customer
        rawMessage.Should().Contain($":59:"); // Beneficiary
        rawMessage.Should().Contain("-}"); // Block end
    }

    [Fact]
    public async Task GenerateBatchAsync_ShouldReturnCorrectNumberOfMessages()
    {
        // Arrange
        var batchSize = 5;

        // Act
        var messages = await _testGenerator.GenerateBatchAsync(batchSize);

        // Assert
        messages.Should().HaveCount(batchSize);
        messages.Should().AllBeOfType<MT103Message>();
    }

    [Fact]
    public async Task GenerateBatchAsync_ShouldRespectValidMessagePercentage()
    {
        // Arrange
        var batchSize = 100;
        _testModeOptions.ValidMessagePercentage = 80;

        // Act
        var messages = await _testGenerator.GenerateBatchAsync(batchSize);

        // Assert
        var validMessages = messages.Count(m => m.Validate() == System.ComponentModel.DataAnnotations.ValidationResult.Success);
        var validPercentage = (validMessages * 100) / batchSize;
        
        // Allow some variance due to randomness (±15%)
        validPercentage.Should().BeInRange(65, 95);
    }

    [Fact]
    public async Task StartGenerationAsync_ShouldStartGenerating()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _queueService.SendMessageAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        await _testGenerator.StartGenerationAsync(TimeSpan.FromMilliseconds(100), cts.Token);
        
        // Give it time to generate at least one message
        await Task.Delay(200);

        // Assert
        _testGenerator.IsGenerating.Should().BeTrue();
        
        // Cleanup
        await _testGenerator.StopGenerationAsync();
        cts.Cancel();
        cts.Dispose();
    }

    [Fact]
    public async Task StopGenerationAsync_ShouldStopGenerating()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _queueService.SendMessageAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        
        await _testGenerator.StartGenerationAsync(TimeSpan.FromMilliseconds(100), cts.Token);
        await Task.Delay(100);

        // Act
        await _testGenerator.StopGenerationAsync();

        // Assert
        _testGenerator.IsGenerating.Should().BeFalse();
        
        // Cleanup
        cts.Cancel();
        cts.Dispose();
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveUniqueTransactionReferences()
    {
        // Arrange
        var batchSize = 10;

        // Act
        var messages = await _testGenerator.GenerateBatchAsync(batchSize);

        // Assert
        var references = messages.Select(m => m.TransactionReference).ToList();
        references.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveValidCurrency()
    {
        // Arrange
        var validCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "SGD" };

        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.Currency.Should().BeOneOf(validCurrencies);
        message.Currency.Should().MatchRegex("^[A-Z]{3}$");
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveValidBankOperationCode()
    {
        // Arrange
        var validCodes = new[] { "CRED", "CRTS", "SPAY", "SPRI", "SSTD" };

        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.BankOperationCode.Should().BeOneOf(validCodes);
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveValidAmount()
    {
        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.Amount.Should().BeGreaterThan(0);
        message.Amount.Should().BeLessThan(1000000);
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveValidOrderingCustomer()
    {
        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.OrderingCustomer.Should().NotBeNull();
        message.OrderingCustomer.Name.Should().NotBeNullOrEmpty();
        
        var customerValidation = message.OrderingCustomer.Validate();
        customerValidation.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public async Task GeneratedMessage_ShouldHaveValidBeneficiaryCustomer()
    {
        // Act
        var message = await _testGenerator.GenerateValidMessageAsync();

        // Assert
        message.BeneficiaryCustomer.Should().NotBeNull();
        message.BeneficiaryCustomer.Name.Should().NotBeNullOrEmpty();
        
        var customerValidation = message.BeneficiaryCustomer.Validate();
        customerValidation.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }
}
