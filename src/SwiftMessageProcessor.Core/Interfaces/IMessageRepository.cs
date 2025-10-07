using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Interfaces;

public interface IMessageRepository
{
    Task<Guid> SaveMessageAsync(ProcessedMessage message);
    Task<ProcessedMessage?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProcessedMessage>> GetByFilterAsync(MessageFilter filter);
    Task UpdateStatusAsync(Guid id, MessageStatus status);
    Task<int> GetMessageCountAsync(MessageFilter filter);
}

public class MessageFilter
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
    public MessageStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? MessageType { get; set; }
}