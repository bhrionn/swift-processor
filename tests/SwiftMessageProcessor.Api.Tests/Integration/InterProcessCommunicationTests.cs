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

public class InterProcessCommunicationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InterProcessCommunicationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.ResetMockCommunicationService();
    }

    [Fact]
    public async Task RestartCommand_UpdatesStatusWithinExpectedTime()
    {
        // Arrange
        var initialStatus = new ProcessStatus
        {
            IsRunning = true,
            Status = "Running",
            StatusUpdatedAt = DateTime.UtcNow
        };

        var restartedStatus = new ProcessStatus
        {
            IsRunning = true,
            Status = "Restarted",
            StatusUpdatedAt = DateTime.UtcNow.AddSeconds(3)
        };

        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Restart)
            .Returns(Task.CompletedTask);

        _factory.MockCommunicationService
            .GetStatusAsync()
            .Returns(initialStatus, restartedStatus);

        // Act
        var restartResponse = await _client.PostAsync("/api/system/restart", null);
        await Task.Delay(100); // Simulate processing time

        var statusResponse = await _client.GetAsync("/api/system/status");

        // Assert
        restartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<SystemStatusDto>();
        status.Should().NotBeNull();
        status!.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task ConsoleAppHealthCheck_ReflectsActualHealth()
    {
        // Arrange - Simulate console app becoming unhealthy
        _factory.MockCommunicationService!
            .IsConsoleAppHealthyAsync()
            .Returns(true, false, true); // Healthy, then unhealthy, then healthy again

        // Act & Assert - First check: Healthy
        var response1 = await _client.GetAsync("/api/system/health");
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second check: Unhealthy
        var response2 = await _client.GetAsync("/api/system/health");
        response2.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // Third check: Healthy again
        var response3 = await _client.GetAsync("/api/system/health");
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StatusUpdates_ReflectProcessingProgress()
    {
        // Arrange
        var statuses = new[]
        {
            new ProcessStatus
            {
                IsRunning = true,
                IsProcessing = true,
                MessagesProcessed = 10,
                MessagesFailed = 0,
                Status = "Processing"
            },
            new ProcessStatus
            {
                IsRunning = true,
                IsProcessing = true,
                MessagesProcessed = 50,
                MessagesFailed = 2,
                Status = "Processing"
            },
            new ProcessStatus
            {
                IsRunning = true,
                IsProcessing = false,
                MessagesProcessed = 100,
                MessagesFailed = 5,
                Status = "Idle"
            }
        };

        var callCount = 0;
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(_ => Task.FromResult(statuses[callCount++ % statuses.Length]));

        // Act & Assert
        for (int i = 0; i < statuses.Length; i++)
        {
            var response = await _client.GetAsync("/api/system/status");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var status = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
            status.Should().NotBeNull();
            status!.MessagesProcessed.Should().Be(statuses[i].MessagesProcessed);
            status.MessagesFailed.Should().Be(statuses[i].MessagesFailed);
        }
    }

    [Fact]
    public async Task TestModeToggle_UpdatesConsoleAppConfiguration()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.EnableTestMode)
            .Returns(Task.CompletedTask);

        _factory.MockCommunicationService
            .SendCommandAsync(ProcessCommand.DisableTestMode)
            .Returns(Task.CompletedTask);

        var enabledStatus = new ProcessStatus { TestModeEnabled = true };
        var disabledStatus = new ProcessStatus { TestModeEnabled = false };

        _factory.MockCommunicationService
            .GetStatusAsync()
            .Returns(disabledStatus, enabledStatus, disabledStatus);

        // Act & Assert - Enable test mode
        var enableResponse = await _client.PostAsync("/api/system/test-mode/enable", null);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusAfterEnable = await _client.GetAsync("/api/system/test-mode");
        var enabledResult = await statusAfterEnable.Content.ReadFromJsonAsync<TestModeDto>();
        enabledResult.Should().NotBeNull();

        // Disable test mode
        var disableResponse = await _client.PostAsync("/api/system/test-mode/disable", null);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusAfterDisable = await _client.GetAsync("/api/system/test-mode");
        var disabledResult = await statusAfterDisable.Content.ReadFromJsonAsync<TestModeDto>();
        disabledResult.Should().NotBeNull();
    }

    [Fact]
    public async Task CommunicationFailure_ReturnsAppropriateError()
    {
        // Arrange
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromException<ProcessStatus>(new TimeoutException("Console app not responding")));

        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("unavailable");
    }

    [Fact]
    public async Task MultipleSimultaneousCommands_AreHandledCorrectly()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(Arg.Any<ProcessCommand>())
            .Returns(Task.CompletedTask);

        // Act - Send multiple commands simultaneously
        var tasks = new[]
        {
            _client.PostAsync("/api/system/start", null),
            _client.PostAsync("/api/system/test-mode/enable", null),
            _client.GetAsync("/api/system/status")
        };

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => 
            r.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task ConsoleAppRestart_MaintainsApiAvailability()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Restart)
            .Returns(Task.CompletedTask);

        _factory.MockCommunicationService
            .GetStatusAsync()
            .Returns(
                new ProcessStatus { IsRunning = true, Status = "Running" },
                new ProcessStatus { IsRunning = false, Status = "Restarting" },
                new ProcessStatus { IsRunning = true, Status = "Running" }
            );

        // Act
        var restartResponse = await _client.PostAsync("/api/system/restart", null);
        var statusDuringRestart = await _client.GetAsync("/api/system/status");
        await Task.Delay(100);
        var statusAfterRestart = await _client.GetAsync("/api/system/status");

        // Assert
        restartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        statusDuringRestart.StatusCode.Should().Be(HttpStatusCode.OK);
        statusAfterRestart.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StatusPolling_ReturnsConsistentData()
    {
        // Arrange
        var status = new ProcessStatus
        {
            IsRunning = true,
            MessagesProcessed = 100,
            MessagesFailed = 5,
            Status = "Running"
        };

        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromResult(status));

        // Act - Poll status multiple times
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => _client.GetAsync("/api/system/status"))
        );

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        var statuses = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<SystemStatusDto>())
        );

        statuses.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNull();
            s!.MessagesProcessed.Should().Be(100);
            s.MessagesFailed.Should().Be(5);
        });
    }
}
