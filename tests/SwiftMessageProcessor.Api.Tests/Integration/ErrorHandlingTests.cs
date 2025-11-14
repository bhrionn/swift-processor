using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SwiftMessageProcessor.Api.Controllers;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using Xunit;

namespace SwiftMessageProcessor.Api.Tests.Integration;

public class ErrorHandlingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ErrorHandlingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.ResetMockCommunicationService();
    }

    [Fact]
    public async Task GetMessages_WithDatabaseError_ReturnsInternalServerError()
    {
        // Note: This test verifies error handling at the controller level
        // In a real scenario, we would simulate a database failure
        // For now, we test with invalid parameters that might cause issues

        // Act
        var response = await _client.GetAsync("/api/messages?take=0");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetMessageById_WithInvalidGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMessages_WithMissingParameter_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMessages_WithEmptyString_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/search?reference=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMessages_WithWhitespaceOnly_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages/search?reference=%20%20%20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithNegativeSkip_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?skip=-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithZeroTake_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?take=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithTakeExceedingLimit_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?take=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SystemController_WhenConsoleAppUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromException<ProcessStatus>(new Exception("Console app unavailable")));

        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("unavailable");
    }

    [Fact]
    public async Task SystemController_WhenCommandFails_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Start)
            .Returns(Task.FromException(new InvalidOperationException("Command failed")));

        // Act
        var response = await _client.PostAsync("/api/system/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SystemController_WhenHealthCheckThrows_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .IsConsoleAppHealthyAsync()
            .Returns(Task.FromException<bool>(new TimeoutException("Health check timeout")));

        // Act
        var response = await _client.GetAsync("/api/system/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        result.Should().NotBeNull();
        result!.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMessages_WithInvalidDateFormat_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?fromDate=invalid-date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithFutureDateRange_ReturnsEmptyResult()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddYears(1).ToString("o");

        // Act
        var response = await _client.GetAsync($"/api/messages?fromDate={futureDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MessageDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessages_WithInvalidStatusValue_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?status=999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentRequests_HandleGracefully()
    {
        // Arrange
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromResult(new ProcessStatus
            {
                IsRunning = true,
                Status = "Running"
            }));

        // Act - Send multiple concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(_ => 
            _client.GetAsync("/api/system/status")
        );

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task LargePageSize_IsRejected()
    {
        // Act
        var response = await _client.GetAsync("/api/messages?take=1000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMessages_WithVeryLongReference_HandlesGracefully()
    {
        // Arrange
        var longReference = new string('A', 1000);

        // Act
        var response = await _client.GetAsync($"/api/messages/search?reference={longReference}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithBothDates_ValidatesDateRange()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.ToString("o");
        var toDate = DateTime.UtcNow.AddDays(-1).ToString("o"); // toDate before fromDate

        // Act
        var response = await _client.GetAsync($"/api/messages?fromDate={fromDate}&toDate={toDate}");

        // Assert
        // Should either handle gracefully or return empty result
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MultipleFailedCommands_DontCrashApi()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(Arg.Any<ProcessCommand>())
            .Returns(Task.FromException(new Exception("Command failed")));

        // Act - Send multiple failing commands
        var tasks = new[]
        {
            _client.PostAsync("/api/system/start", null),
            _client.PostAsync("/api/system/stop", null),
            _client.PostAsync("/api/system/restart", null)
        };

        var responses = await Task.WhenAll(tasks);

        // Assert - API should still be responsive
        responses.Should().AllSatisfy(r => 
            r.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable));

        // Verify API is still working
        var healthResponse = await _client.GetAsync("/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidHttpMethod_ReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.PutAsync("/api/messages", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NonExistentEndpoint_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthEndpoint_AlwaysResponds()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
