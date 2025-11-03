using System.ComponentModel.DataAnnotations;

namespace SwiftMessageProcessor.Infrastructure.Entities;

public class SystemAuditEntry
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;
    
    public string? EventData { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(100)]
    public string? UserId { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
}