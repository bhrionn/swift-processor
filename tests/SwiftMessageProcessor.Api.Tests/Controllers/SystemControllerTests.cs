using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using SwiftMessageProcessor.Api.Controllers;
using SwiftMessageProcessor.Core.Interfaces;
using Xunit;

namespace SwiftMessageProcessor.Api.Tests.Controllers;

public class SystemControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SystemControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.ResetMockCommunicationService();
    }

    [Fact]
    public async Task GetStatus_WhenConsoleAppIsRunning_ReturnsStatus()
    {
        // Arrange
        var expectedStatus = new ProcessStatus
        {
            IsRunning = true,
            IsProcessing = true,
            MessagesProcessed = 100,
            MessagesFailed = 5,
            MessagesPending = 10,
            LastProcessedAt = DateTime.UtcNow.AddMinutes(-1),
            StatusUpdatedAt = DateTime.UtcNow,
            Status = "Running",
            TestModeEnabled = false,
            Metadata = new Dictionary<string, object> { ["Version"] = "1.0.0" }
        };

        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromResult(expectedStatus));

        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        result.Should().NotBeNull();
        result!.IsRunning.Should().BeTrue();
        result.IsProcessing.Should().BeTrue();
        result.MessagesProcessed.Should().Be(100);
        result.MessagesFailed.Should().Be(5);
        result.MessagesPending.Should().Be(10);
        result.Status.Should().Be("Running");
        result.TestModeEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_WhenConsoleAppIsUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromException<ProcessStatus>(new Exception("Console app unavailable")));

        // Act
        var response = await _client.GetAsync("/api/system/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task CheckHealth_WhenConsoleAppIsHealthy_ReturnsHealthy()
    {
        // Arrange
        _factory.MockCommunicationService!
            .IsConsoleAppHealthyAsync()
            .Returns(Task.FromResult(true));

        // Act
        var response = await _client.GetAsync("/api/system/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        result.Should().NotBeNull();
        result!.IsHealthy.Should().BeTrue();
        result.Status.Should().Be("Healthy");
        result.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CheckHealth_WhenConsoleAppIsUnhealthy_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .IsConsoleAppHealthyAsync()
            .Returns(Task.FromResult(false));

        // Act
        var response = await _client.GetAsync("/api/system/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        result.Should().NotBeNull();
        result!.IsHealthy.Should().BeFalse();
        result.Status.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task CheckHealth_WhenHealthCheckFails_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .IsConsoleAppHealthyAsync()
            .Returns(Task.FromException<bool>(new Exception("Health check failed")));

        // Act
        var response = await _client.GetAsync("/api/system/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        result.Should().NotBeNull();
        result!.IsHealthy.Should().BeFalse();
        result.Status.Should().Be("Unavailable");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartProcessor_SendsStartCommand_ReturnsSuccess()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Start)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsync("/api/system/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.MockCommunicationService.Received(1).SendCommandAsync(ProcessCommand.Start);
    }

    [Fact]
    public async Task StartProcessor_WhenCommunicationFails_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Start)
            .Returns(Task.FromException(new Exception("Communication failed")));

        // Act
        var response = await _client.PostAsync("/api/system/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task StopProcessor_SendsStopCommand_ReturnsSuccess()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Stop)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsync("/api/system/stop", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.MockCommunicationService.Received(1).SendCommandAsync(ProcessCommand.Stop);
    }

    [Fact]
    public async Task RestartProcessor_SendsRestartCommand_ReturnsSuccess()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Restart)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsync("/api/system/restart", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.MockCommunicationService.Received(1).SendCommandAsync(ProcessCommand.Restart);
    }

    [Fact]
    public async Task RestartProcessor_WhenCommunicationFails_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.Restart)
            .Returns(Task.FromException(new Exception("Communication failed")));

        // Act
        var response = await _client.PostAsync("/api/system/restart", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task EnableTestMode_SendsEnableCommand_ReturnsSuccess()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.EnableTestMode)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsync("/api/system/test-mode/enable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.MockCommunicationService.Received(1).SendCommandAsync(ProcessCommand.EnableTestMode);
    }

    [Fact]
    public async Task DisableTestMode_SendsDisableCommand_ReturnsSuccess()
    {
        // Arrange
        _factory.MockCommunicationService!
            .SendCommandAsync(ProcessCommand.DisableTestMode)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsync("/api/system/test-mode/disable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.MockCommunicationService.Received(1).SendCommandAsync(ProcessCommand.DisableTestMode);
    }

    [Fact]
    public async Task GetTestMode_ReturnsTestModeStatus()
    {
        // Arrange
        var expectedStatus = new ProcessStatus
        {
            IsRunning = true,
            TestModeEnabled = true,
            StatusUpdatedAt = DateTime.UtcNow
        };

        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromResult(expectedStatus));

        // Act
        var response = await _client.GetAsync("/api/system/test-mode");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestModeDto>();
        result.Should().NotBeNull();
        result!.Enabled.Should().BeTrue();
        result.RetrievedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetTestMode_WhenConsoleAppUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        _factory.MockCommunicationService!
            .GetStatusAsync()
            .Returns(Task.FromException<ProcessStatus>(new Exception("Console app unavailable")));

        // Act
        var response = await _client.GetAsync("/api/system/test-mode");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
