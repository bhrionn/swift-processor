using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Interfaces;

public interface IMessageProcessingService
{
    Task<ProcessingResult> ProcessMessageAsync(string rawMessage);
    Task StartProcessingAsync(CancellationToken cancellationToken);
    Task StopProcessingAsync();
    Task<SystemStatus> GetSystemStatusAsync();
    Task<ProcessingMetrics> GetMetricsAsync();
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public ProcessedMessage? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
}

public class SystemStatus
{
    public bool IsProcessing { get; set; }
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public DateTime LastProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}