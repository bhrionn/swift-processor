using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Services;

public class LocalQueueService : IQueueService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _queues = new();
    private readonly ConcurrentDictionary<string, QueueMetrics> _queueMetrics = new();
    private readonly ILogger<LocalQueueService> _logger;
    private readonly QueueOptions _options;

    public LocalQueueService(ILogger<LocalQueueService> logger, IOptions<QueueOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        // Initialize default queues
        InitializeQueue(_options.Settings.InputQueue);
        InitializeQueue(_options.Settings.CompletedQueue);
        InitializeQueue(_options.Settings.DeadLetterQueue);
        
        _logger.LogInformation("LocalQueueService initialized with queues: {Queues}", 
            string.Join(", ", new[] { _options.Settings.InputQueue, _options.Settings.CompletedQueue, _options.Settings.DeadLetterQueue }));
    }

    public Task<string?> ReceiveMessageAsync(string queueName)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        
        var queue = _queues.GetOrAdd(queueName, _ => new ConcurrentQueue<string>());
        var metrics = _queueMetrics.GetOrAdd(queueName, _ => new QueueMetrics());
        
        if (queue.TryDequeue(out var message))
        {
            Interlocked.Increment(ref metrics.MessagesProcessed);
            metrics.LastUpdated = DateTime.UtcNow;
            
            _logger.LogDebug("Message received from queue {QueueName}: {MessageLength} characters", 
                queueName, message.Length);
            
            return Task.FromResult<string?>(message);
        }
        
        return Task.FromResult<string?>(null);
    }

    public Task SendMessageAsync(string queueName, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentException.ThrowIfNullOrEmpty(message);
        
        var queue = _queues.GetOrAdd(queueName, _ => new ConcurrentQueue<string>());
        var metrics = _queueMetrics.GetOrAdd(queueName, _ => new QueueMetrics());
        
        queue.Enqueue(message);
        Interlocked.Increment(ref metrics.MessagesSent);
        metrics.LastUpdated = DateTime.UtcNow;
        
        _logger.LogDebug("Message sent to queue {QueueName}: {MessageLength} characters", 
            queueName, message.Length);
        
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync()
    {
        try
        {
            // Perform basic health checks
            var queueCount = _queues.Count;
            var totalMessages = _queues.Values.Sum(q => q.Count);
            
            _logger.LogDebug("Health check: {QueueCount} queues, {TotalMessages} total messages", 
                queueCount, totalMessages);
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for LocalQueueService");
            return Task.FromResult(false);
        }
    }

    public Task<QueueStatistics> GetStatisticsAsync()
    {
        var statistics = new QueueStatistics
        {
            MessagesInQueue = _queues.Values.Sum(q => q.Count),
            MessagesProcessed = (int)_queueMetrics.Values.Sum(m => m.MessagesProcessed),
            MessagesFailed = (int)_queueMetrics.Values.Sum(m => m.MessagesFailed),
            LastUpdated = DateTime.UtcNow
        };
        
        return Task.FromResult(statistics);
    }

    private void InitializeQueue(string queueName)
    {
        _queues.TryAdd(queueName, new ConcurrentQueue<string>());
        _queueMetrics.TryAdd(queueName, new QueueMetrics());
    }

    private class QueueMetrics
    {
        public long MessagesProcessed;
        public long MessagesSent;
        public long MessagesFailed;
        public DateTime LastUpdated = DateTime.UtcNow;
    }
}