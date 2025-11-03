using Microsoft.EntityFrameworkCore;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Data;

public class SwiftMessageContext : DbContext
{
    public SwiftMessageContext(DbContextOptions<SwiftMessageContext> options) : base(options)
    {
    }
    
    public DbSet<ProcessedMessageEntity> Messages { get; set; }
    public DbSet<SystemAuditEntry> SystemAudit { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwiftMessageContext).Assembly);
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps for entities
        var entries = ChangeTracker.Entries<ProcessedMessageEntity>()
            .Where(e => e.State == EntityState.Modified);
            
        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
}