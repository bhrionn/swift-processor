namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class QueueOptions
{
    public const string SectionName = "Queue";
    
    public string Provider { get; set; } = string.Empty;
    public string? Region { get; set; }
    public QueueSettings Settings { get; set; } = new();
    
    public void Validate()
    {
        if (string.IsNullOrEmpty(Provider))
            throw new InvalidOperationException("Queue provider must be specified");
    }
}

public class QueueSettings
{
    public string InputQueue { get; set; } = "input-messages";
    public string CompletedQueue { get; set; } = "completed-messages";
    public string DeadLetterQueue { get; set; } = "failed-messages";
}