using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Deployment;

/// <summary>
/// Tests for inter-service communication and dependencies
/// </summary>
public class InterServiceCommunicationTests : IDisposable
{
    private readonly string _testCommunicationPath;
    private readonly ServiceProvider _serviceProvider;

    public InterServiceCommunicationTests()
    {
        _testCommunicationPath = Path.Combine(Path.GetTempPath(), $"test_comm_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testCommunicationPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Communication:CommunicationDirectory"] = _testCommunicationPath,
                ["Communication:StatusUpdateIntervalSeconds"] = "5",
                ["Communication:CommandTimeoutSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<CommunicationOptions>(configuration.GetSection("Communication"));
        services.AddScoped<IProcessCommunicationService, FileBasedCommunicationService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task FileBasedCommunication_ShouldSendAndReceiveStatus()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();
        var expectedStatus = new ProcessStatus
        {
            IsRunning = true,
            StatusUpdatedAt = DateTime.UtcNow,
            MessagesProcessed = 100,
            MessagesFailed = 5
        };

        // Act - Simulate console app writing status
        var statusFilePath = Path.Combine(_testCommunicationPath, "status.json");
        var statusJson = System.Text.Json.JsonSerializer.Serialize(expectedStatus);
        await File.WriteAllTextAsync(statusFilePath, statusJson);

        // Act - API reading status
        var actualStatus = await communicationService.GetStatusAsync();

        // Assert
        actualStatus.Should().NotBeNull();
        actualStatus.IsRunning.Should().Be(expectedStatus.IsRunning);
        actualStatus.MessagesProcessed.Should().Be(expectedStatus.MessagesProcessed);
        actualStatus.MessagesFailed.Should().Be(expectedStatus.MessagesFailed);
    }

    [Fact]
    public async Task FileBasedCommunication_ShouldSendCommands()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();
        var command = ProcessCommand.Restart;

        // Act
        await communicationService.SendCommandAsync(command);

        // Assert
        var commandFilePath = Path.Combine(_testCommunicationPath, "command.json");
        File.Exists(commandFilePath).Should().BeTrue("Command file should be created");

        var commandJson = await File.ReadAllTextAsync(commandFilePath);
        commandJson.Should().Contain("Restart", "Command should be written to file");
    }

    [Fact]
    public async Task FileBasedCommunication_ShouldDetectConsoleAppHealth()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();
        
        // Write a recent status to indicate healthy
        var status = new ProcessStatus
        {
            IsRunning = true,
            StatusUpdatedAt = DateTime.UtcNow
        };
        var statusFilePath = Path.Combine(_testCommunicationPath, "status.json");
        var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
        await File.WriteAllTextAsync(statusFilePath, statusJson);

        // Act
        var isHealthy = await communicationService.IsConsoleAppHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue("Console app should be healthy with recent heartbeat");
    }

    [Fact]
    public async Task FileBasedCommunication_ShouldDetectUnhealthyConsoleApp()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();
        
        // Write an old status to indicate unhealthy
        var status = new ProcessStatus
        {
            IsRunning = true,
            StatusUpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var statusFilePath = Path.Combine(_testCommunicationPath, "status.json");
        var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
        await File.WriteAllTextAsync(statusFilePath, statusJson);

        // Act
        var isHealthy = await communicationService.IsConsoleAppHealthyAsync();

        // Assert
        isHealthy.Should().BeFalse("Console app should be unhealthy with old heartbeat");
    }

    [Fact]
    public async Task FileBasedCommunication_ShouldHandleMissingStatusFile()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();

        // Act
        var status = await communicationService.GetStatusAsync();

        // Assert
        status.Should().NotBeNull("Should return default status when file is missing");
        status.IsRunning.Should().BeFalse("Should indicate not running when status file is missing");
    }

    [Fact]
    public async Task SharedVolumes_ShouldBeAccessibleByBothServices()
    {
        // Arrange
        var testFilePath = Path.Combine(_testCommunicationPath, "shared_test.txt");
        var testContent = "Shared data between services";

        // Act - Simulate one service writing
        await File.WriteAllTextAsync(testFilePath, testContent);

        // Act - Simulate other service reading
        var readContent = await File.ReadAllTextAsync(testFilePath);

        // Assert
        readContent.Should().Be(testContent, "Both services should access shared volume");
    }

    [Fact]
    public void DockerCompose_ShouldDefineSharedVolumes()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, "docker", "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        // Assert
        content.Should().Contain("swift-data:", "Should define shared data volume");
        content.Should().Contain("swift-communication:", "Should define shared communication volume");
        
        // Both services should mount the volumes
        var apiSection = GetServiceSection(content, "swift-api");
        apiSection.Should().Contain("swift-data:", "API should mount data volume");
        apiSection.Should().Contain("swift-communication:", "API should mount communication volume");

        var consoleSection = GetServiceSection(content, "swift-console");
        consoleSection.Should().Contain("swift-data:", "Console should mount data volume");
        consoleSection.Should().Contain("swift-communication:", "Console should mount communication volume");
    }

    [Fact]
    public void DockerCompose_ShouldDefineServiceDependencies()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, "docker", "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        // Assert
        var apiSection = GetServiceSection(content, "swift-api");
        apiSection.Should().Contain("depends_on:", "API should have dependencies");
        apiSection.Should().Contain("swift-console", "API should depend on console");

        var frontendSection = GetServiceSection(content, "swift-frontend");
        frontendSection.Should().Contain("depends_on:", "Frontend should have dependencies");
        frontendSection.Should().Contain("swift-api", "Frontend should depend on API");
    }

    [Fact]
    public void DockerCompose_ShouldDefineHealthChecks()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, "docker", "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        // Assert
        var apiSection = GetServiceSection(content, "swift-api");
        apiSection.Should().Contain("healthcheck:", "API should have health check");

        var consoleSection = GetServiceSection(content, "swift-console");
        consoleSection.Should().Contain("healthcheck:", "Console should have health check");

        var frontendSection = GetServiceSection(content, "swift-frontend");
        frontendSection.Should().Contain("healthcheck:", "Frontend should have health check");
    }

    [Fact]
    public void DockerCompose_ShouldUseCommonNetwork()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, "docker", "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        // Assert
        content.Should().Contain("swift-network:", "Should define common network");

        var apiSection = GetServiceSection(content, "swift-api");
        apiSection.Should().Contain("swift-network", "API should use common network");

        var consoleSection = GetServiceSection(content, "swift-console");
        consoleSection.Should().Contain("swift-network", "Console should use common network");

        var frontendSection = GetServiceSection(content, "swift-frontend");
        frontendSection.Should().Contain("swift-network", "Frontend should use common network");
    }

    [Fact]
    public void DockerCompose_ShouldConfigureRestartPolicies()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, "docker", "docker-compose.yml");
        var content = File.ReadAllText(composePath);

        // Assert
        var apiSection = GetServiceSection(content, "swift-api");
        apiSection.Should().Contain("restart:", "API should have restart policy");

        var consoleSection = GetServiceSection(content, "swift-console");
        consoleSection.Should().Contain("restart:", "Console should have restart policy");
    }

    [Fact]
    public async Task CommunicationService_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var communicationService = _serviceProvider.GetRequiredService<IProcessCommunicationService>();
        var tasks = new List<Task>();

        // Act - Simulate multiple concurrent status updates
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var status = new ProcessStatus
                {
                    IsRunning = true,
                    StatusUpdatedAt = DateTime.UtcNow,
                    MessagesProcessed = index * 10
                };
                var statusFilePath = Path.Combine(_testCommunicationPath, "status.json");
                var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                await File.WriteAllTextAsync(statusFilePath, statusJson);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without errors
        var finalStatus = await communicationService.GetStatusAsync();
        finalStatus.Should().NotBeNull("Should be able to read status after concurrent writes");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();

        if (Directory.Exists(_testCommunicationPath))
        {
            try
            {
                Directory.Delete(_testCommunicationPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string GetServiceSection(string composeContent, string serviceName)
    {
        var lines = composeContent.Split('\n');
        var serviceIndex = -1;
        
        // Find the service definition under "services:"
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == $"{serviceName}:" || trimmed.StartsWith($"{serviceName}:"))
            {
                serviceIndex = i;
                break;
            }
        }
        
        if (serviceIndex == -1)
            return string.Empty;

        var section = new System.Text.StringBuilder();
        var baseIndent = lines[serviceIndex].TakeWhile(char.IsWhiteSpace).Count();

        // Include the service line and all indented content below it
        for (int i = serviceIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                section.AppendLine(line);
                continue;
            }

            var currentIndent = line.TakeWhile(char.IsWhiteSpace).Count();

            // Stop when we hit another service at the same level
            if (i > serviceIndex && currentIndent <= baseIndent)
                break;

            section.AppendLine(line);
        }

        return section.ToString();
    }

    private string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "SwiftMessageProcessor.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find repository root");
    }
}
