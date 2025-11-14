using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Deployment;

/// <summary>
/// Tests for Docker container builds and deployments
/// </summary>
public class DockerBuildTests
{
    private const string DockerComposeFile = "docker/docker-compose.yml";
    private const string DockerComposeProdFile = "docker/docker-compose.prod.yml";
    private const string DockerComposeFullFile = "docker/docker-compose.full.yml";

    [Fact]
    public async Task ApiDockerfile_ShouldBuildSuccessfully()
    {
        // Arrange
        if (!IsDockerAvailable())
        {
            // Skip test if Docker is not available
            return;
        }

        var dockerfilePath = "docker/Dockerfile.api";
        var contextPath = GetRepositoryRoot();

        // Act
        var result = await RunDockerBuildAsync(dockerfilePath, contextPath, "swift-api-test");

        // Assert
        result.ExitCode.Should().Be(0, $"Docker build should succeed. Output: {result.Output}");
    }

    [Fact]
    public async Task ConsoleDockerfile_ShouldBuildSuccessfully()
    {
        // Arrange
        if (!IsDockerAvailable())
        {
            // Skip test if Docker is not available
            return;
        }

        var dockerfilePath = "docker/Dockerfile.console";
        var contextPath = GetRepositoryRoot();

        // Act
        var result = await RunDockerBuildAsync(dockerfilePath, contextPath, "swift-console-test");

        // Assert
        result.ExitCode.Should().Be(0, $"Docker build should succeed. Output: {result.Output}");
    }

    [Fact]
    public async Task FrontendDockerfile_ShouldBuildSuccessfully()
    {
        // Arrange
        if (!IsDockerAvailable())
        {
            // Skip test if Docker is not available
            return;
        }

        var dockerfilePath = "docker/Dockerfile.frontend";
        var contextPath = GetRepositoryRoot();

        // Act
        var result = await RunDockerBuildAsync(dockerfilePath, contextPath, "swift-frontend-test");

        // Assert
        result.ExitCode.Should().Be(0, $"Docker build should succeed. Output: {result.Output}");
    }

    [Fact]
    public void DockerComposeFile_ShouldExistAndBeValid()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, DockerComposeFile);

        // Assert
        File.Exists(composePath).Should().BeTrue("docker-compose.yml should exist");
        
        var content = File.ReadAllText(composePath);
        content.Should().Contain("version:", "Should have version specified");
        content.Should().Contain("services:", "Should define services");
        content.Should().Contain("swift-api:", "Should define API service");
        content.Should().Contain("swift-console:", "Should define console service");
        content.Should().Contain("swift-frontend:", "Should define frontend service");
    }

    [Fact]
    public void DockerComposeProdFile_ShouldExistAndBeValid()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, DockerComposeProdFile);

        // Assert
        File.Exists(composePath).Should().BeTrue("docker-compose.prod.yml should exist");
        
        var content = File.ReadAllText(composePath);
        content.Should().Contain("version:", "Should have version specified");
        content.Should().Contain("services:", "Should define services");
        content.Should().NotContain("build:", "Production compose should use pre-built images");
    }

    [Fact]
    public void AllDockerfiles_ShouldHaveHealthChecks()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var dockerfiles = new[]
        {
            Path.Combine(repoRoot, "docker/Dockerfile.api"),
            Path.Combine(repoRoot, "docker/Dockerfile.console"),
            Path.Combine(repoRoot, "docker/Dockerfile.frontend")
        };

        // Act & Assert
        foreach (var dockerfile in dockerfiles)
        {
            File.Exists(dockerfile).Should().BeTrue($"{dockerfile} should exist");
            var content = File.ReadAllText(dockerfile);
            content.Should().Contain("HEALTHCHECK", $"{Path.GetFileName(dockerfile)} should have health check");
        }
    }

    [Fact]
    public void AllDockerfiles_ShouldUseNonRootUser()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var dockerfiles = new[]
        {
            Path.Combine(repoRoot, "docker/Dockerfile.api"),
            Path.Combine(repoRoot, "docker/Dockerfile.console")
        };

        // Act & Assert
        foreach (var dockerfile in dockerfiles)
        {
            var content = File.ReadAllText(dockerfile);
            content.Should().Contain("USER appuser", $"{Path.GetFileName(dockerfile)} should switch to non-root user");
        }
    }

    [Fact]
    public void DockerComposeFile_ShouldDefineRequiredVolumes()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, DockerComposeFile);
        var content = File.ReadAllText(composePath);

        // Assert
        content.Should().Contain("volumes:", "Should define volumes section");
        content.Should().Contain("swift-data:", "Should define data volume");
        content.Should().Contain("swift-communication:", "Should define communication volume");
    }

    [Fact]
    public void DockerComposeFile_ShouldDefineNetworks()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repoRoot, DockerComposeFile);
        var content = File.ReadAllText(composePath);

        // Assert
        content.Should().Contain("networks:", "Should define networks section");
        content.Should().Contain("swift-network:", "Should define swift-network");
    }

    private async Task<ProcessResult> RunDockerBuildAsync(string dockerfilePath, string contextPath, string tag)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build -f {dockerfilePath} -t {tag} {contextPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = contextPath
            }
        };

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString() + error.ToString()
        };
    }

    private bool IsDockerAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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

    private class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
    }
}
