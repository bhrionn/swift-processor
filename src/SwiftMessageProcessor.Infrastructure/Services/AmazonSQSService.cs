using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Services;

public class AmazonSQSService : IQueueService, IDisposable
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<AmazonSQSService> _logger;
    private readonly QueueOptions _options;
    private readonly Dictionary<string, string> _queueUrls = new();
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _disposed;

    public AmazonSQSService(
        IAmazonSQS sqsClient,
        ILogger<AmazonSQSService> logger,
        IOptions<QueueOptions> options)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("AmazonSQSService initialized for region: {Region}", _options.Region ?? "default");
    }

    public async Task<string?> ReceiveMessageAsync(string queueName)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        try
        {
            var queueUrl = await GetQueueUrlAsync(queueName);

            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 5, // Long polling
                MessageAttributeNames = new List<string> { "All" }
            };

            var response = await _sqsClient.ReceiveMessageAsync(request);

            if (response.Messages.Count > 0)
            {
                var message = response.Messages[0];
                
                // Delete the message from the queue after receiving
                await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = message.ReceiptHandle
                });

                _logger.LogDebug("Message received from SQS queue {QueueName}: {MessageId}", 
                    queueName, message.MessageId);

                return message.Body;
            }

            return null;
        }
        catch (QueueDoesNotExistException ex)
        {
            _logger.LogError(ex, "Queue {QueueName} does not exist", queueName);
            throw new InvalidOperationException($"Queue '{queueName}' does not exist", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving message from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task SendMessageAsync(string queueName, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentException.ThrowIfNullOrEmpty(message);

        try
        {
            var queueUrl = await GetQueueUrlAsync(queueName);

            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = message
            };

            var response = await _sqsClient.SendMessageAsync(request);

            _logger.LogDebug("Message sent to SQS queue {QueueName}: {MessageId}", 
                queueName, response.MessageId);
        }
        catch (QueueDoesNotExistException ex)
        {
            _logger.LogError(ex, "Queue {QueueName} does not exist", queueName);
            throw new InvalidOperationException($"Queue '{queueName}' does not exist", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Try to get queue attributes for the input queue as a health check
            var queueUrl = await GetQueueUrlAsync(_options.Settings.InputQueue);
            
            var request = new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "ApproximateNumberOfMessages" }
            };

            var response = await _sqsClient.GetQueueAttributesAsync(request);

            _logger.LogDebug("SQS health check successful. Queue has approximately {MessageCount} messages",
                response.Attributes.GetValueOrDefault("ApproximateNumberOfMessages", "0"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQS health check failed");
            return false;
        }
    }

    public async Task<QueueStatistics> GetStatisticsAsync()
    {
        try
        {
            var statistics = new QueueStatistics
            {
                LastUpdated = DateTime.UtcNow
            };

            // Get statistics for all configured queues
            var queueNames = new[]
            {
                _options.Settings.InputQueue,
                _options.Settings.CompletedQueue,
                _options.Settings.DeadLetterQueue
            };

            foreach (var queueName in queueNames)
            {
                try
                {
                    var queueUrl = await GetQueueUrlAsync(queueName);
                    var attributes = await GetQueueAttributesAsync(queueUrl);

                    if (attributes.TryGetValue("ApproximateNumberOfMessages", out var messagesInQueue))
                    {
                        statistics.MessagesInQueue += int.Parse(messagesInQueue);
                    }

                    // Note: AWS SQS doesn't provide processed/failed counts directly
                    // These would need to be tracked separately in CloudWatch or a database
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get statistics for queue {QueueName}", queueName);
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue statistics");
            throw;
        }
    }

    private async Task<string> GetQueueUrlAsync(string queueName)
    {
        // Check cache first
        if (_queueUrls.TryGetValue(queueName, out var cachedUrl))
        {
            return cachedUrl;
        }

        await _initializationLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_queueUrls.TryGetValue(queueName, out cachedUrl))
            {
                return cachedUrl;
            }

            var request = new GetQueueUrlRequest
            {
                QueueName = queueName
            };

            var response = await _sqsClient.GetQueueUrlAsync(request);
            _queueUrls[queueName] = response.QueueUrl;

            _logger.LogInformation("Queue URL resolved for {QueueName}: {QueueUrl}", 
                queueName, response.QueueUrl);

            return response.QueueUrl;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> GetQueueAttributesAsync(string queueUrl)
    {
        var request = new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string>
            {
                "ApproximateNumberOfMessages",
                "ApproximateNumberOfMessagesNotVisible",
                "ApproximateNumberOfMessagesDelayed"
            }
        };

        var response = await _sqsClient.GetQueueAttributesAsync(request);
        return response.Attributes;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _initializationLock?.Dispose();
            _sqsClient?.Dispose();
            _disposed = true;
        }
    }
}
