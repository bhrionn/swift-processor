using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SwiftMessageProcessor.Api.Controllers;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Data;
using Xunit;

namespace SwiftMessageProcessor.Api.Tests.Controllers;

public class MessagesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessagesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMessages_WithNoMessages_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/messages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MessageDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMessages_WithMessages_ReturnsPaginatedList()
    {
        // Arrange
        await SeedTestMessages(5);

        // Act
        var response = await _client.GetAsync("/api/messages?skip=0&take=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MessageDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(5);
        result.Skip.Should().Be(0);
        result.Take.Should().Be(3);
    }

    [Fact]
    public async Task GetMessages_WithStatusFilter_ReturnsFilteredMessages()
    {
        // Arrange
        await SeedTestMessages(3, MessageStatus.Processed);
        await SeedTestMessages(2, MessageStatus.Failed);

        // Act
        var response = await _client.GetAsync("/api/messages?status=1"); // Processed = 1

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MessageDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result!.Items.Should().OnlyContain(m => m.Status == MessageStatus.Processed);
    }

    [Fact]
    public async Task GetMessages_WithDateFilter_ReturnsFilteredMessages()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-1);
        await SeedTestMessagesWithDate(2, oldDate);
        await SeedTestMessagesWithDate(3, recentDate);

        // Act
        var fromDate = DateTime.UtcNow.AddDays(-5).ToString("o");
        var response = await _client.GetAsync($"/api/messages?fromDate={fromDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MessageDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetMessages_WithInvalidPagination_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?skip=-1&take=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessageById_WithExistingMessage_ReturnsMessageDetail()
    {
        // Arrange
        var messageId = await SeedSingleTestMessage();

        // Act
        var response = await _client.GetAsync($"/api/messages/{messageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MessageDetailDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.MessageType.Should().Be("MT103");
        result.ParsedData.Should().NotBeNull();
        result.ParsedData!.TransactionReference.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMessageById_WithNonExistentMessage_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/messages/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchMessages_WithValidReference_ReturnsMatchingMessages()
    {
        // Arrange
        await SeedTestMessageWithReference("REF12345");
        await SeedTestMessageWithReference("REF67890");
        await SeedTestMessageWithReference("OTHER123");

        // Act
        var response = await _client.GetAsync("/api/messages/search?reference=REF");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<MessageDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.TransactionReference!.Contains("REF"));
    }

    [Fact]
    public async Task SearchMessages_WithoutReference_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMessages_WithEmptyReference_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/search?reference=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStatistics_WithMessages_ReturnsCorrectStatistics()
    {
        // Arrange
        await SeedTestMessages(5, MessageStatus.Processed);
        await SeedTestMessages(3, MessageStatus.Failed);
        await SeedTestMessages(2, MessageStatus.Pending);

        // Act
        var response = await _client.GetAsync("/api/messages/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MessageStatisticsDto>();
        result.Should().NotBeNull();
        result!.TotalMessages.Should().Be(10);
        result.ProcessedCount.Should().Be(5);
        result.FailedCount.Should().Be(3);
        result.PendingCount.Should().Be(2);
        result.LastProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetStatistics_WithNoMessages_ReturnsZeroStatistics()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MessageStatisticsDto>();
        result.Should().NotBeNull();
        result!.TotalMessages.Should().Be(0);
        result.ProcessedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    // Helper methods
    private async Task<int> SeedTestMessages(int count, MessageStatus status = MessageStatus.Processed)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        for (int i = 0; i < count; i++)
        {
            var message = CreateTestMessage(status);
            await repository.SaveMessageAsync(message);
        }

        return count;
    }

    private async Task<int> SeedTestMessagesWithDate(int count, DateTime processedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        for (int i = 0; i < count; i++)
        {
            var message = CreateTestMessage(MessageStatus.Processed);
            message.ProcessedAt = processedAt;
            await repository.SaveMessageAsync(message);
        }

        return count;
    }

    private async Task<Guid> SeedSingleTestMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var message = CreateTestMessage(MessageStatus.Processed);
        return await repository.SaveMessageAsync(message);
    }

    private async Task SeedTestMessageWithReference(string reference)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var message = CreateTestMessage(MessageStatus.Processed);
        if (message.ParsedMessage is MT103Message mt103)
        {
            mt103.TransactionReference = reference;
        }
        await repository.SaveMessageAsync(message);
    }

    private ProcessedMessage CreateTestMessage(MessageStatus status)
    {
        var mt103 = new MT103Message
        {
            MessageType = "MT103",
            TransactionReference = $"REF{Guid.NewGuid().ToString()[..8]}",
            BankOperationCode = "CRED",
            ValueDate = DateTime.UtcNow,
            Currency = "USD",
            Amount = 1000.00m,
            OrderingCustomer = new OrderingCustomer
            {
                Account = "12345678",
                Name = "Test Customer",
                Address = "123 Test St"
            },
            BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Account = "87654321",
                Name = "Beneficiary Customer",
                Address = "456 Beneficiary Ave"
            }
        };

        return new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MT103",
            RawMessage = "{1:F01TESTBIC0AXXX}{2:O1031234567890}{4::20:REF:32A:231115USD1000,00:50K:Test Customer:59:Beneficiary-}",
            ParsedMessage = mt103,
            Status = status,
            ProcessedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["Source"] = "Test",
                ["Environment"] = "Testing"
            }
        };
    }
}
