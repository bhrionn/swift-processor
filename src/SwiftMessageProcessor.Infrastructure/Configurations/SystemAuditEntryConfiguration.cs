using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Configurations;

public class SystemAuditEntryConfiguration : IEntityTypeConfiguration<SystemAuditEntry>
{
    public void Configure(EntityTypeBuilder<SystemAuditEntry> builder)
    {
        builder.ToTable("SystemAudit");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.EventType)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(s => s.EventData)
            .HasColumnType("NTEXT");
            
        builder.Property(s => s.Timestamp)
            .IsRequired()
            .HasColumnType("DATETIME2")
            .HasDefaultValueSql("GETUTCDATE()");
            
        builder.Property(s => s.UserId)
            .HasMaxLength(100);
            
        builder.Property(s => s.IpAddress)
            .HasMaxLength(45);
        
        // Index for performance
        builder.HasIndex(s => s.Timestamp)
            .HasDatabaseName("IX_SystemAudit_Timestamp");
            
        builder.HasIndex(s => s.EventType)
            .HasDatabaseName("IX_SystemAudit_EventType");
    }
}