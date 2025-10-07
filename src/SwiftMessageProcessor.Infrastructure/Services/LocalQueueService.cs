using System.Collections.Concurrent;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.Infrastructure.Services;

public class LocalQueueService : IQueueService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _queues = new();
    private readonly QueueStatistics _statistics = new();

    public Task<string?> ReceiveMessageAsync(string queueName)
    {
        var queue = _queues.GetOrAdd(queueName, _ => new ConcurrentQueue<string>());
        
        if (queue.TryDequeue(out var message))
        {
            _statistics.MessagesProcessed++;
            _statistics.LastUpdated = DateTime.UtcNow;
            return Task.FromResult<string?>(message);
        }
        
        return Task.FromResult<string?>(null);
    }

    public Task SendMessageAsync(string queueName, string message)
    {
        var queue = _queues.GetOrAdd(queueName, _ => new ConcurrentQueue<string>());
        queue.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<QueueStatistics> GetStatisticsAsync()
    {
        _statistics.MessagesInQueue = _queues.Values.Sum(q => q.Count);
        _statistics.LastUpdated = DateTime.UtcNow;
        return Task.FromResult(_statistics);
    }
}