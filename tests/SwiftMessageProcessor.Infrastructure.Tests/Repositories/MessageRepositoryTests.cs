using FluentAssertions;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Repositories;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Repositories;

public class MessageRepositoryTests : TestBase
{
    private readonly IMessageRepository _repository;
    
    public MessageRepositoryTests()
    {
        _repository = new MessageRepository(Context, Logger);
    }
    
    [Fact]
    public async Task SaveMessageAsync_NewMessage_ShouldSaveSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage();
        
        // Act
        var result = await _repository.SaveMessageAsync(message);
        
        // Assert
        result.Should().Be(message.Id);
        
        var savedMessage = await _repository.GetByIdAsync(message.Id);
        savedMessage.Should().NotBeNull();
        savedMessage!.Id.Should().Be(message.Id);
        savedMessage.MessageType.Should().Be(message.MessageType);
        savedMessage.RawMessage.Should().Be(message.RawMessage);
        savedMessage.Status.Should().Be(message.Status);
    }
    
    [Fact]
    public async Task SaveMessageAsync_ExistingMessage_ShouldUpdateSuccessfully()
    {
        // Arrange
        var message = CreateTestMessage();
        await _repository.SaveMessageAsync(message);
        
        // Modify the message
        message.Status = MessageStatus.Failed;
        message.ErrorDetails = "Test error";
        
        // Act
        var result = await _repository.SaveMessageAsync(message);
        
        // Assert
        result.Should().Be(message.Id);
        
        var updatedMessage = await _repository.GetByIdAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.Status.Should().Be(MessageStatus.Failed);
        updatedMessage.ErrorDetails.Should().Be("Test error");
    }
    
    [Fact]
    public async Task GetByIdAsync_ExistingMessage_ShouldReturnMessage()
    {
        // Arrange
        var message = CreateTestMessage();
        await _repository.SaveMessageAsync(message);
        
        // Act
        var result = await _repository.GetByIdAsync(message.Id);
        
        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(message.Id);
        result.MessageType.Should().Be(message.MessageType);
        result.RawMessage.Should().Be(message.RawMessage);
        result.Status.Should().Be(message.Status);
    }
    
    [Fact]
    public async Task GetByIdAsync_NonExistentMessage_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        
        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetByFilterAsync_NoFilter_ShouldReturnAllMessages()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed),
            CreateTestMessage("MT103", MessageStatus.Failed),
            CreateTestMessage("MT102", MessageStatus.Processed)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter { Take = 10 };
        
        // Act
        var result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        result.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task GetByFilterAsync_StatusFilter_ShouldReturnFilteredMessages()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed),
            CreateTestMessage("MT103", MessageStatus.Failed),
            CreateTestMessage("MT103", MessageStatus.Processed)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            Status = MessageStatus.Processed,
            Take = 10 
        };
        
        // Act
        var result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        result.Should().HaveCount(2);
        result.All(m => m.Status == MessageStatus.Processed).Should().BeTrue();
    }
    
    [Fact]
    public async Task GetByFilterAsync_MessageTypeFilter_ShouldReturnFilteredMessages()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed),
            CreateTestMessage("MT102", MessageStatus.Processed),
            CreateTestMessage("MT103", MessageStatus.Failed)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            MessageType = "MT103",
            Take = 10 
        };
        
        // Act
        var result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        result.Should().HaveCount(2);
        result.All(m => m.MessageType == "MT103").Should().BeTrue();
    }
    
    [Fact]
    public async Task GetByFilterAsync_DateRangeFilter_ShouldReturnFilteredMessages()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.Date;
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed, baseDate.AddDays(-2)),
            CreateTestMessage("MT103", MessageStatus.Processed, baseDate.AddDays(-1)),
            CreateTestMessage("MT103", MessageStatus.Processed, baseDate)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            FromDate = baseDate.AddDays(-1),
            Take = 10 
        };
        
        // Act
        var result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        result.Should().HaveCount(2);
        result.All(m => m.ProcessedAt >= baseDate.AddDays(-1)).Should().BeTrue();
    }
    
    [Fact]
    public async Task GetByFilterAsync_Pagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var messages = Enumerable.Range(1, 5)
            .Select(i => CreateTestMessage($"MT10{i}", MessageStatus.Processed))
            .ToArray();
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter 
        { 
            Skip = 2,
            Take = 2 
        };
        
        // Act
        var result = await _repository.GetByFilterAsync(filter);
        
        // Assert
        result.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task UpdateStatusAsync_ExistingMessage_ShouldUpdateStatus()
    {
        // Arrange
        var message = CreateTestMessage();
        await _repository.SaveMessageAsync(message);
        
        // Act
        await _repository.UpdateStatusAsync(message.Id, MessageStatus.Failed);
        
        // Assert
        var updatedMessage = await _repository.GetByIdAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.Status.Should().Be(MessageStatus.Failed);
    }
    
    [Fact]
    public async Task UpdateStatusAsync_NonExistentMessage_ShouldThrowException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        
        // Act & Assert
        await _repository.Invoking(r => r.UpdateStatusAsync(nonExistentId, MessageStatus.Failed))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Message with ID {nonExistentId} not found");
    }
    
    [Fact]
    public async Task GetMessageCountAsync_NoFilter_ShouldReturnTotalCount()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed),
            CreateTestMessage("MT103", MessageStatus.Failed),
            CreateTestMessage("MT102", MessageStatus.Processed)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter();
        
        // Act
        var result = await _repository.GetMessageCountAsync(filter);
        
        // Assert
        result.Should().Be(3);
    }
    
    [Fact]
    public async Task GetMessageCountAsync_WithFilter_ShouldReturnFilteredCount()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("MT103", MessageStatus.Processed),
            CreateTestMessage("MT103", MessageStatus.Failed),
            CreateTestMessage("MT102", MessageStatus.Processed)
        };
        
        foreach (var message in messages)
        {
            await _repository.SaveMessageAsync(message);
        }
        
        var filter = new MessageFilter { Status = MessageStatus.Processed };
        
        // Act
        var result = await _repository.GetMessageCountAsync(filter);
        
        // Assert
        result.Should().Be(2);
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
}