namespace SwiftMessageProcessor.Core.Models;

public enum MessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    DeadLetter = 4,
    Archived = 5
}