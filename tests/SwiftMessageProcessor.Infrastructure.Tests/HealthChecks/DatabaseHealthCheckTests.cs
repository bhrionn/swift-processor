using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.HealthChecks;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.HealthChecks;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_InMemoryDatabase_ReturnsResult()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SwiftMessageContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;

        using var context = new SwiftMessageContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var logger = Substitute.For<ILogger<DatabaseHealthCheck>>();
        var healthCheck = new DatabaseHealthCheck(context, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        // In-memory database should return a result (may be healthy or degraded depending on migrations)
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("responseTimeMs"));
        Assert.True(result.Data.ContainsKey("databaseProvider"));
    }

    [Fact]
    public async Task CheckHealthAsync_WithMessages_IncludesMessageCount()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SwiftMessageContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;

        using var context = new SwiftMessageContext(options);
        await context.Database.EnsureCreatedAsync();
        
        // Add some test messages
        for (int i = 0; i < 5; i++)
        {
            context.Messages.Add(new Entities.ProcessedMessageEntity
            {
                Id = Guid.NewGuid(),
                MessageType = "MT103",
                RawMessage = "test",
                Status = Core.Models.MessageStatus.Processed,
                ProcessedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var logger = Substitute.For<ILogger<DatabaseHealthCheck>>();
        var healthCheck = new DatabaseHealthCheck(context, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("messageCount"));
        Assert.Equal(5L, result.Data["messageCount"]);
    }
}
