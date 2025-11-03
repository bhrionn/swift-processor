using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
    public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
    {
        builder.ToTable("Messages");
        
        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.MessageType)
            .IsRequired()
            .HasMaxLength(10);
            
        builder.Property(m => m.RawMessage)
            .IsRequired()
            .HasColumnType("NTEXT");
            
        builder.Property(m => m.ParsedData)
            .HasColumnType("NTEXT");
            
        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();
            
        builder.Property(m => m.ProcessedAt)
            .IsRequired()
            .HasColumnType("DATETIME2");
            
        builder.Property(m => m.ErrorDetails)
            .HasColumnType("NTEXT");
            
        builder.Property(m => m.Metadata)
            .HasColumnType("NTEXT");
            
        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasColumnType("DATETIME2")
            .HasDefaultValueSql("GETUTCDATE()");
            
        builder.Property(m => m.UpdatedAt)
            .IsRequired()
            .HasColumnType("DATETIME2")
            .HasDefaultValueSql("GETUTCDATE()");
        
        // Indexes for performance
        builder.HasIndex(m => m.Status)
            .HasDatabaseName("IX_Messages_Status");
            
        builder.HasIndex(m => m.MessageType)
            .HasDatabaseName("IX_Messages_MessageType");
            
        builder.HasIndex(m => m.ProcessedAt)
            .HasDatabaseName("IX_Messages_ProcessedAt");
            
        builder.HasIndex(m => new { m.MessageType, m.ProcessedAt })
            .HasDatabaseName("IX_Messages_Type_ProcessedAt");
    }
}