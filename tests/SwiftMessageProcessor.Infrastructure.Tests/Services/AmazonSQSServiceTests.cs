using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class AmazonSQSServiceTests
{
    private readonly IAmazonSQS _mockSqsClient;
    private readonly ILogger<AmazonSQSService> _logger;
    private readonly QueueOptions _queueOptions;
    private readonly AmazonSQSService _sqsService;

    public AmazonSQSServiceTests()
    {
        _mockSqsClient = Substitute.For<IAmazonSQS>();
        _logger = Substitute.For<ILogger<AmazonSQSService>>();
        
        _queueOptions = new QueueOptions
        {
            Provider = "AmazonSQS",
            Region = "us-east-1",
            Settings = new QueueSettings
            {
                InputQueue = "test-input-queue",
                CompletedQueue = "test-completed-queue",
                DeadLetterQueue = "test-dlq"
            }
        };

        var options = Options.Create(_queueOptions);
        _sqsService = new AmazonSQSService(_mockSqsClient, _logger, options);
    }

    [Fact]
    public async Task SendMessageAsync_ValidMessage_ShouldSendSuccessfully()
    {
        // Arrange
        const string queueName = "test-queue";
        const string message = "test message content";
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName))
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.SendMessageAsync(Arg.Is<SendMessageRequest>(r => 
            r.QueueUrl == queueUrl && r.MessageBody == message))
            .Returns(new SendMessageResponse { MessageId = "test-message-id" });

        // Act
        await _sqsService.SendMessageAsync(queueName, message);

        // Assert
        await _mockSqsClient.Received(1).GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName));
        await _mockSqsClient.Received(1).SendMessageAsync(Arg.Is<SendMessageRequest>(r => 
            r.QueueUrl == queueUrl && r.MessageBody == message));
    }

    [Fact]
    public async Task ReceiveMessageAsync_MessageAvailable_ShouldReturnMessage()
    {
        // Arrange
        const string queueName = "test-queue";
        const string messageBody = "test message";
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";
        const string receiptHandle = "test-receipt-handle";

        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName))
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl))
            .Returns(new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message { Body = messageBody, ReceiptHandle = receiptHandle }
                }
            });

        _mockSqsClient.DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(r => 
            r.QueueUrl == queueUrl && r.ReceiptHandle == receiptHandle))
            .Returns(new DeleteMessageResponse());

        // Act
        var result = await _sqsService.ReceiveMessageAsync(queueName);

        // Assert
        result.Should().Be(messageBody);
        await _mockSqsClient.Received(1).ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl));
        await _mockSqsClient.Received(1).DeleteMessageAsync(Arg.Is<DeleteMessageRequest>(r => 
            r.QueueUrl == queueUrl && r.ReceiptHandle == receiptHandle));
    }

    [Fact]
    public async Task ReceiveMessageAsync_NoMessages_ShouldReturnNull()
    {
        // Arrange
        const string queueName = "test-queue";
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName))
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl))
            .Returns(new ReceiveMessageResponse { Messages = new List<Message>() });

        // Act
        var result = await _sqsService.ReceiveMessageAsync(queueName);

        // Assert
        result.Should().BeNull();
        await _mockSqsClient.Received(1).ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl));
        await _mockSqsClient.DidNotReceive().DeleteMessageAsync(Arg.Any<DeleteMessageRequest>());
    }

    [Fact]
    public async Task IsHealthyAsync_AllQueuesExist_ShouldReturnTrue()
    {
        // Arrange
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/input-queue";
        
        _mockSqsClient.GetQueueUrlAsync(Arg.Any<GetQueueUrlRequest>())
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.GetQueueAttributesAsync(Arg.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl))
            .Returns(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    { "ApproximateNumberOfMessages", "0" }
                }
            });

        // Act
        var result = await _sqsService.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_QueueDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _mockSqsClient.GetQueueUrlAsync(Arg.Any<GetQueueUrlRequest>())
            .Throws(new QueueDoesNotExistException("Queue does not exist"));

        // Act
        var result = await _sqsService.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnQueueStatistics()
    {
        // Arrange
        const string inputQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/input-queue";
        const string completedQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/completed-queue";
        const string dlqUrl = "https://sqs.us-east-1.amazonaws.com/123456789/dlq";
        
        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == _queueOptions.Settings.InputQueue))
            .Returns(new GetQueueUrlResponse { QueueUrl = inputQueueUrl });
        
        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == _queueOptions.Settings.CompletedQueue))
            .Returns(new GetQueueUrlResponse { QueueUrl = completedQueueUrl });
        
        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == _queueOptions.Settings.DeadLetterQueue))
            .Returns(new GetQueueUrlResponse { QueueUrl = dlqUrl });

        _mockSqsClient.GetQueueAttributesAsync(Arg.Is<GetQueueAttributesRequest>(r => r.QueueUrl == inputQueueUrl))
            .Returns(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    { "ApproximateNumberOfMessages", "5" }
                }
            });

        _mockSqsClient.GetQueueAttributesAsync(Arg.Is<GetQueueAttributesRequest>(r => r.QueueUrl == completedQueueUrl))
            .Returns(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    { "ApproximateNumberOfMessages", "3" }
                }
            });

        _mockSqsClient.GetQueueAttributesAsync(Arg.Is<GetQueueAttributesRequest>(r => r.QueueUrl == dlqUrl))
            .Returns(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    { "ApproximateNumberOfMessages", "2" }
                }
            });

        // Act
        var statistics = await _sqsService.GetStatisticsAsync();

        // Assert
        statistics.Should().NotBeNull();
        statistics.MessagesInQueue.Should().Be(10); // 5 + 3 + 2 = 10 (sum of all queues)
        statistics.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendMessageAsync_QueueUrlCached_ShouldNotCallGetQueueUrlTwice()
    {
        // Arrange
        const string queueName = "test-queue";
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

        _mockSqsClient.GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName))
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.SendMessageAsync(Arg.Any<SendMessageRequest>())
            .Returns(new SendMessageResponse { MessageId = "test-message-id" });

        // Act
        await _sqsService.SendMessageAsync(queueName, "message1");
        await _sqsService.SendMessageAsync(queueName, "message2");

        // Assert
        await _mockSqsClient.Received(1).GetQueueUrlAsync(Arg.Is<GetQueueUrlRequest>(r => r.QueueName == queueName));
        await _mockSqsClient.Received(2).SendMessageAsync(Arg.Any<SendMessageRequest>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SendMessageAsync_InvalidQueueName_ShouldThrowArgumentException(string queueName)
    {
        // Act & Assert
        await _sqsService.Invoking(s => s.SendMessageAsync(queueName, "test message"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SendMessageAsync_InvalidMessage_ShouldThrowArgumentException(string message)
    {
        // Act & Assert
        await _sqsService.Invoking(s => s.SendMessageAsync("test-queue", message))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ReceiveMessageAsync_InvalidQueueName_ShouldThrowArgumentException(string queueName)
    {
        // Act & Assert
        await _sqsService.Invoking(s => s.ReceiveMessageAsync(queueName))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMessageAsync_SQSException_ShouldLogAndThrow()
    {
        // Arrange
        const string queueName = "test-queue";
        const string queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

        _mockSqsClient.GetQueueUrlAsync(Arg.Any<GetQueueUrlRequest>())
            .Returns(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.SendMessageAsync(Arg.Any<SendMessageRequest>())
            .Throws(new AmazonSQSException("SQS error"));

        // Act & Assert
        await _sqsService.Invoking(s => s.SendMessageAsync(queueName, "test message"))
            .Should().ThrowAsync<AmazonSQSException>();
    }
}
