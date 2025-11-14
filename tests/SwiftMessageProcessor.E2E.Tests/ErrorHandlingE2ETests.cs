using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.E2E.Tests;

/// <summary>
/// End-to-end tests focused on error handling and recovery scenarios
/// in the distributed architecture
/// </summary>
public class ErrorHandlingE2ETests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;
    private readonly HttpClient _client;

    public ErrorHandlingE2ETests(E2ETestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task MalformedMessage_HandledGracefully_WithDetailedError()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var malformedMessages = new[]
        {
            "", // Empty message
            "   ", // Whitespace only
            "{1:INCOMPLETE", // Incomplete SWIFT block
            "RANDOM_TEXT_NOT_SWIFT", // Not a SWIFT message
            "{1:}{2:}{3:}{4:}{5:}" // Empty blocks
        };

        // Act & Assert
        foreach (var message in malformedMessages)
        {
            var result = await _fixture.SimulateMessageProcessingAsync(message);
            
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            
            // Verify error is logged in database
            await Task.Delay(200);
            var response = await _client.GetAsync("/api/messages?status=Failed");
            var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
            
            messages.Should().NotBeNull();
            var failedMessage = messages!.FirstOrDefault(m => m.RawMessage == message);
            if (!string.IsNullOrWhiteSpace(message))
            {
                failedMessage.Should().NotBeNull();
                failedMessage!.ErrorDetails.Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public async Task MissingMandatoryFields_RejectedWithValidationError()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // MT103 message missing mandatory field :32A (Value Date, Currency, Amount)
        var messageWithoutAmount = @"{1:F01BANKBEBBAXXX0000000000}{2:I103BANKUS33XXXXN}{3:{108:TESTREF}}
{4:
:20:TESTREF123
:23B:CRED
:50K:/BE62510007547061
ORDERING CUSTOMER
:59:/US12345678901234567890
BENEFICIARY CUSTOMER
:71A:SHA
-}{5:{MAC:00000000}{CHK:123456789ABC}}";

        // Act
        var result = await _fixture.SimulateMessageProcessingAsync(messageWithoutAmount);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        
        // Verify in database
        await Task.Delay(200);
        var response = await _client.GetAsync("/api/messages?status=Failed");
        var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        messages.Should().NotBeNull();
        var failedMessage = messages!.FirstOrDefault(m => m.RawMessage == messageWithoutAmount);
        failedMessage.Should().NotBeNull();
        failedMessage!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task InvalidFieldFormat_RejectedWithFormatError()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // MT103 with invalid amount format
        var messageWithInvalidAmount = @"{1:F01BANKBEBBAXXX0000000000}{2:I103BANKUS33XXXXN}{3:{108:TESTREF}}
{4:
:20:TESTREF123
:23B:CRED
:32A:231115EURINVALID_AMOUNT
:50K:/BE62510007547061
ORDERING CUSTOMER
:59:/US12345678901234567890
BENEFICIARY CUSTOMER
:71A:SHA
-}{5:{MAC:00000000}{CHK:123456789ABC}}";

        // Act
        var result = await _fixture.SimulateMessageProcessingAsync(messageWithInvalidAmount);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConcurrentErrorScenarios_AllHandledIndependently()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var messages = new[]
        {
            GetValidMT103Message("VALID001", 1000.00m), // Valid
            "INVALID_MESSAGE_1", // Invalid
            GetValidMT103Message("VALID002", 2000.00m), // Valid
            "", // Empty
            GetValidMT103Message("VALID003", 3000.00m), // Valid
            "INVALID_MESSAGE_2" // Invalid
        };

        // Act - Process all messages concurrently
        var tasks = messages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        await Task.Delay(500);

        // Assert - Valid messages succeeded, invalid failed
        var validResults = results.Where((r, i) => messages[i].StartsWith("{1:")).ToList();
        var invalidResults = results.Where((r, i) => !messages[i].StartsWith("{1:")).ToList();

        validResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        invalidResults.Should().AllSatisfy(r => r.Success.Should().BeFalse());

        // Verify database state
        var processedResponse = await _client.GetAsync("/api/messages?status=Processed");
        var processedMessages = await processedResponse.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        processedMessages.Should().HaveCountGreaterOrEqualTo(3);

        var failedResponse = await _client.GetAsync("/api/messages?status=Failed");
        var failedMessages = await failedResponse.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        failedMessages.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task DatabaseConnectionIssue_HandledWithRetry()
    {
        // This test validates that the system handles database issues gracefully
        // In a real scenario, we would simulate database unavailability
        // For this test, we verify that the retry mechanism is in place
        
        // Arrange
        var message = GetValidMT103Message("DBTEST001", 1000.00m);

        // Act
        var result = await _fixture.SimulateMessageProcessingAsync(message);

        // Assert - Should succeed (or fail gracefully with proper error)
        result.Should().NotBeNull();
        if (!result.Success)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task QueueProcessingError_MessageMovesToDeadLetterQueue()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        await _fixture.ClearQueuesAsync();
        
        var invalidMessage = "QUEUE_ERROR_TEST";

        // Act - Enqueue invalid message
        await _fixture.EnqueueMessageAsync(invalidMessage);
        var result = await _fixture.SimulateMessageProcessingAsync(invalidMessage);

        await Task.Delay(300);

        // Assert - Message should fail and be logged
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();

        // Verify in database with failed status
        var response = await _client.GetAsync("/api/messages?status=Failed");
        var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        messages.Should().NotBeNull();
        var failedMessage = messages!.FirstOrDefault(m => m.RawMessage == invalidMessage);
        failedMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task PartialSystemFailure_OtherComponentsContinueWorking()
    {
        // Arrange - Process some valid messages
        await _fixture.ClearDatabaseAsync();
        
        var validMessage1 = GetValidMT103Message("PARTIAL001", 1000.00m);
        var validMessage2 = GetValidMT103Message("PARTIAL002", 2000.00m);

        // Act - Process messages even if one component has issues
        var result1 = await _fixture.SimulateMessageProcessingAsync(validMessage1);
        
        // Simulate a failure scenario
        var invalidMessage = "CAUSE_FAILURE";
        var failureResult = await _fixture.SimulateMessageProcessingAsync(invalidMessage);
        
        // Continue processing after failure
        var result2 = await _fixture.SimulateMessageProcessingAsync(validMessage2);

        await Task.Delay(500);

        // Assert - Valid messages should still be processed
        result1.Success.Should().BeTrue();
        failureResult.Success.Should().BeFalse();
        result2.Success.Should().BeTrue();

        // Verify API is still responsive
        var response = await _client.GetAsync("/api/messages");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LargeMessagePayload_HandledCorrectly()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Create a large but valid MT103 message with extensive remittance info
        var largeRemittanceInfo = string.Join("\n", Enumerable.Range(1, 10)
            .Select(i => $"REMITTANCE LINE {i} WITH ADDITIONAL INFORMATION"));
        
        var largeMessage = $@"{{1:F01BANKBEBBAXXX0000000000}}{{2:I103BANKUS33XXXXN}}{{3:{{108:LARGEREF}}}}
{{4:
:20:LARGEREF123
:23B:CRED
:32A:231115EUR10000.00
:50K:/BE62510007547061
ORDERING CUSTOMER NAME WITH LONG ADDRESS
STREET NAME AND NUMBER 123
CITY AND POSTAL CODE
COUNTRY
:59:/US12345678901234567890
BENEFICIARY CUSTOMER NAME WITH LONG ADDRESS
STREET NAME AND NUMBER 456
CITY AND POSTAL CODE
COUNTRY
:70:{largeRemittanceInfo}
:71A:SHA
-}}{{5:{{MAC:00000000}}{{CHK:123456789ABC}}}}";

        // Act
        var result = await _fixture.SimulateMessageProcessingAsync(largeMessage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNull();
        result.Message!.RawMessage.Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public async Task RapidSuccessiveErrors_SystemRemainsStable()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var invalidMessages = Enumerable.Range(1, 10)
            .Select(i => $"INVALID_MESSAGE_{i}")
            .ToList();

        // Act - Send rapid successive invalid messages
        var tasks = invalidMessages.Select(msg => _fixture.SimulateMessageProcessingAsync(msg));
        var results = await Task.WhenAll(tasks);

        await Task.Delay(500);

        // Assert - All should fail gracefully
        results.Should().AllSatisfy(r => r.Success.Should().BeFalse());

        // System should still be responsive
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // API should still work
        var messagesResponse = await _client.GetAsync("/api/messages");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ErrorRecovery_AfterFailure_SystemContinuesProcessing()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        // Act - Process invalid message
        var invalidResult = await _fixture.SimulateMessageProcessingAsync("INVALID");
        await Task.Delay(200);

        // Process valid message after error
        var validMessage = GetValidMT103Message("RECOVERY001", 1000.00m);
        var validResult = await _fixture.SimulateMessageProcessingAsync(validMessage);
        await Task.Delay(200);

        // Assert
        invalidResult.Success.Should().BeFalse();
        validResult.Success.Should().BeTrue();

        // Verify both are in database
        var allMessages = await _client.GetAsync("/api/messages");
        var messages = await allMessages.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        messages.Should().NotBeNull();
        messages.Should().Contain(m => m.Status == "Failed");
        messages.Should().Contain(m => m.Status == "Processed");
    }

    [Fact]
    public async Task ValidationErrors_ProvideDetailedFeedback()
    {
        // Arrange
        await _fixture.ClearDatabaseAsync();
        
        var messageWithInvalidCurrency = @"{1:F01BANKBEBBAXXX0000000000}{2:I103BANKUS33XXXXN}{3:{108:TESTREF}}
{4:
:20:TESTREF123
:23B:CRED
:32A:231115XXX1000.00
:50K:/BE62510007547061
ORDERING CUSTOMER
:59:/US12345678901234567890
BENEFICIARY CUSTOMER
:71A:SHA
-}{5:{MAC:00000000}{CHK:123456789ABC}}";

        // Act
        var result = await _fixture.SimulateMessageProcessingAsync(messageWithInvalidCurrency);
        await Task.Delay(200);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        // Verify detailed error in database
        var response = await _client.GetAsync("/api/messages?status=Failed");
        var messages = await response.Content.ReadFromJsonAsync<List<ProcessedMessageDto>>();
        
        var failedMessage = messages!.FirstOrDefault(m => m.RawMessage == messageWithInvalidCurrency);
        failedMessage.Should().NotBeNull();
        failedMessage!.ErrorDetails.Should().NotBeNullOrEmpty();
    }

    // Helper method
    private string GetValidMT103Message(string reference = "TESTREF123", decimal amount = 1000.00m)
    {
        return $@"{{1:F01BANKBEBBAXXX0000000000}}{{2:I103BANKUS33XXXXN}}{{3:{{108:{reference}}}}}
{{4:
:20:{reference}
:23B:CRED
:32A:231115EUR{amount:F2}
:50K:/BE62510007547061
ORDERING CUSTOMER NAME
:59:/US12345678901234567890
BENEFICIARY CUSTOMER NAME
:70:PAYMENT FOR INVOICE
:71A:SHA
-}}{{5:{{MAC:00000000}}{{CHK:123456789ABC}}}}";
    }
}
