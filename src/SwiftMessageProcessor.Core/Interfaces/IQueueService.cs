namespace SwiftMessageProcessor.Core.Interfaces;

public interface IQueueService
{
    Task<string?> ReceiveMessageAsync(string queueName);
    Task SendMessageAsync(string queueName, string message);
    Task<bool> IsHealthyAsync();
    Task<QueueStatistics> GetStatisticsAsync();
}

public class QueueStatistics
{
    public int MessagesInQueue { get; set; }
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public DateTime LastUpdated { get; set; }
}