namespace SwiftMessageProcessor.Core.Models;

public class ProcessedMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string RawMessage { get; set; } = string.Empty;
    public SwiftMessage? ParsedMessage { get; set; }
    public MessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}