using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Repositories;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Repositories;

public class MessageRepositoryRetryTests : IDisposable
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<MessageRepository> _logger;
    private readonly MessageRepository _repository;
    
    public MessageRepositoryRetryTests()
    {
        var options = new DbContextOptionsBuilder<SwiftMessageContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _context = new SwiftMessageContext(options);
        _logger = Substitute.For<ILogger<MessageRepository>>();
        _repository = new MessageRepository(_context, _logger);
        
        _context.Database.EnsureCreated();
    }
    
    [Fact]
    public async Task SaveMessageAsync_WithValidData_ShouldSucceed()
    {
        // Arrange
        var message = CreateTestMessage();
        
        // Act
        var result = await _repository.SaveMessageAsync(message);
        
        // Assert
        result.Should().Be(message.Id);
        
        // Verify the message was saved
        var savedMessage = await _context.Messages.FirstOrDefaultAsync(m => m.Id == message.Id);
        savedMessage.Should().NotBeNull();
        savedMessage!.MessageType.Should().Be(message.MessageType);
    }
    
    [Fact]
    public async Task GetByFilterAsync_WithLargeDataset_ShouldPerformWell()
    {
        // Arrange - Create a larger dataset to test performance
        var messages = Enumerable.Range(1, 100)
            .Select(i => CreateTestMessage($"MT{i:D3}", i % 2 == 0 ? MessageStatus.Processed : MessageStatus.Failed))
            .ToList();
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            Status = MessageStatus.Processed,
            Take = 25,
            Skip = 10
        };
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetByFilterAsync(filter);
        stopwatch.Stop();
        
        // Assert
        result.Should().HaveCount(25);
        result.All(m => m.Status == MessageStatus.Processed).Should().BeTrue();
        
        // Performance assertion - should complete within reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // 1 second
    }
    
    [Fact]
    public async Task GetMessageCountAsync_WithComplexFilter_ShouldReturnAccurateCount()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.Date;
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed, baseDate.AddDays(-3)),
            CreateTestMessage("MT103", MessageStatus.Failed, baseDate.AddDays(-2)),
            CreateTestMessage("MT102", MessageStatus.Processed, baseDate.AddDays(-1)),
            CreateTestMessage("MT103", MessageStatus.Processed, baseDate),
            CreateTestMessage("MT103", MessageStatus.Failed, baseDate.AddDays(1))
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            MessageType = "MT103",
            Status = MessageStatus.Processed,
            FromDate = baseDate.AddDays(-2),
            ToDate = baseDate.AddDays(1)
        };
        
        // Act
        var count = await _repository.GetMessageCountAsync(filter);
        var messages_result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        count.Should().Be(1); // Only one MT103 Processed message in the date range
        messages_result.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Repository_WithConcurrentOperations_ShouldHandleCorrectly()
    {
        // Arrange
        var messages = Enumerable.Range(1, 10)
            .Select(i => CreateTestMessage($"MT{i:D3}"))
            .ToList();
        
        // Act - Perform concurrent save operations
        var tasks = messages.Select(message => _repository.SaveMessageAsync(message));
        var results = await Task.WhenAll(tasks);
        
        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(id => id != Guid.Empty);
        
        // Verify all messages were saved
        var savedCount = await _repository.GetMessageCountAsync(new MessageFilter());
        savedCount.Should().Be(10);
    }
    
    private static ProcessedMessage CreateTestMessage(
        string messageType = "MT103", 
        MessageStatus status = MessageStatus.Processed,
        DateTime? processedAt = null)
    {
        return new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
            RawMessage = $"{{1:F01TESTBIC0AXXX0000000000}}{{2:I{messageType}TESTBIC0AXXXN}}{{3:{{108:TEST}}}}{{4::20:TEST123:32A:240101EUR1000,00:50K:TEST CUSTOMER:59:TEST BENEFICIARY-}}",
            Status = status,
            ProcessedAt = processedAt ?? DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "source", "test" },
                { "version", "1.0" }
            }
        };
    }
    
    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}