using System.ComponentModel.DataAnnotations;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Infrastructure.Entities;

public class ProcessedMessageEntity
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string MessageType { get; set; } = string.Empty;
    
    [Required]
    public string RawMessage { get; set; } = string.Empty;
    
    public string? ParsedData { get; set; } // JSON serialized parsed message
    
    public MessageStatus Status { get; set; }
    
    public DateTime ProcessedAt { get; set; }
    
    public string? ErrorDetails { get; set; }
    
    public string? Metadata { get; set; } // JSON serialized metadata
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}