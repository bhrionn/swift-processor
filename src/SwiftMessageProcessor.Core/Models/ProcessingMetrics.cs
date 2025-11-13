namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Metrics for message processing performance and status
/// </summary>
public class ProcessingMetrics
{
    public int TotalMessagesProcessed { get; set; }
    public int TotalMessagesFailed { get; set; }
    public int MessagesInQueue { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double MessagesPerMinute { get; set; }
    public DateTime MetricsStartTime { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = new();
    public Dictionary<string, double> ProcessingTimeByStage { get; set; } = new();
}
