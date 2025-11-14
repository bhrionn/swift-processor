using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Infrastructure.Configuration;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Deployment;

/// <summary>
/// Tests for environment configuration switching
/// </summary>
public class EnvironmentConfigurationTests
{
    [Fact]
    public void DevelopmentConfiguration_ShouldUseSQLiteAndInMemoryQueue()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");

        // Act
        var databaseProvider = configuration["Database:Provider"];
        var queueProvider = configuration["Queue:Provider"];

        // Assert
        databaseProvider.Should().Be("SQLite", "Development should use SQLite");
        queueProvider.Should().Be("InMemory", "Development should use in-memory queue");
    }

    [Fact]
    public void ProductionConfiguration_ShouldUseSqlServerAndAmazonSQS()
    {
        // Arrange
        var configuration = BuildConfiguration("Production");

        // Act
        var databaseProvider = configuration["Database:Provider"];
        var queueProvider = configuration["Queue:Provider"];

        // Assert
        databaseProvider.Should().Be("SqlServer", "Production should use SQL Server");
        queueProvider.Should().Be("AmazonSQS", "Production should use Amazon SQS");
    }

    [Fact]
    public void StagingConfiguration_ShouldUseSqlServerAndAmazonSQS()
    {
        // Arrange
        var configuration = BuildConfiguration("Staging");

        // Act
        var databaseProvider = configuration["Database:Provider"];
        var queueProvider = configuration["Queue:Provider"];

        // Assert
        databaseProvider.Should().Be("SqlServer", "Staging should use SQL Server");
        queueProvider.Should().Be("AmazonSQS", "Staging should use Amazon SQS");
    }

    [Fact]
    public void DatabaseOptions_ShouldBindCorrectlyFromConfiguration()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");
        var services = new ServiceCollection();
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        // Assert
        options.Provider.Should().Be("SQLite");
        options.ConnectionString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void QueueOptions_ShouldBindCorrectlyFromConfiguration()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");
        var services = new ServiceCollection();
        services.Configure<QueueOptions>(configuration.GetSection("Queue"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<QueueOptions>>().Value;

        // Assert
        options.Provider.Should().Be("InMemory");
    }

    [Fact]
    public void ProcessingOptions_ShouldBindCorrectlyFromConfiguration()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");
        var services = new ServiceCollection();
        services.Configure<ProcessingOptions>(configuration.GetSection("Processing"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<ProcessingOptions>>().Value;

        // Assert
        options.MaxConcurrentMessages.Should().BeGreaterThan(0);
        options.QueuePollingIntervalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TestModeOptions_ShouldBeEnabledInDevelopment()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");
        var services = new ServiceCollection();
        services.Configure<TestModeOptions>(configuration.GetSection("TestMode"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<TestModeOptions>>().Value;

        // Assert
        options.Enabled.Should().BeTrue("Test mode should be enabled in development");
    }

    [Fact]
    public void TestModeOptions_ShouldBeDisabledInProduction()
    {
        // Arrange
        var configuration = BuildConfiguration("Production");
        var services = new ServiceCollection();
        services.Configure<TestModeOptions>(configuration.GetSection("TestMode"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<TestModeOptions>>().Value;

        // Assert
        options.Enabled.Should().BeFalse("Test mode should be disabled in production");
    }

    [Fact]
    public void CommunicationOptions_ShouldBindCorrectlyFromConfiguration()
    {
        // Arrange
        var configuration = BuildConfiguration("Development");
        var services = new ServiceCollection();
        services.Configure<CommunicationOptions>(configuration.GetSection("Communication"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<CommunicationOptions>>().Value;

        // Assert - If section doesn't exist, defaults should be used
        options.CommunicationDirectory.Should().NotBeNullOrEmpty("Should have default communication directory");
        options.StatusUpdateIntervalSeconds.Should().BeGreaterThan(0, "Should have positive status update interval");
        options.CommandTimeoutSeconds.Should().BeGreaterThan(0, "Should have positive command timeout");
    }

    [Fact]
    public void ConfigurationValidator_ShouldValidateDatabaseOptions()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "",
                ["Database:ConnectionString"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<DatabaseOptions>(invalidConfig.GetSection("Database"));
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>("Empty provider should fail validation");
    }

    [Fact]
    public void AllEnvironmentConfigFiles_ShouldExist()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var apiConfigPath = Path.Combine(repoRoot, "src", "SwiftMessageProcessor.Api");
        var consoleConfigPath = Path.Combine(repoRoot, "src", "SwiftMessageProcessor.Console");

        var expectedFiles = new[]
        {
            Path.Combine(apiConfigPath, "appsettings.json"),
            Path.Combine(apiConfigPath, "appsettings.Development.json"),
            Path.Combine(apiConfigPath, "appsettings.Production.json"),
            Path.Combine(apiConfigPath, "appsettings.Staging.json"),
            Path.Combine(consoleConfigPath, "appsettings.Development.json"),
            Path.Combine(consoleConfigPath, "appsettings.Production.json"),
            Path.Combine(consoleConfigPath, "appsettings.Staging.json")
        };

        // Act & Assert
        foreach (var file in expectedFiles)
        {
            File.Exists(file).Should().BeTrue($"{Path.GetFileName(file)} should exist");
        }
    }

    [Fact]
    public void EnvironmentConfiguration_ShouldSwitchCorrectly()
    {
        // Arrange & Act
        var devConfig = BuildConfiguration("Development");
        var prodConfig = BuildConfiguration("Production");

        // Assert
        devConfig["Database:Provider"].Should().NotBe(prodConfig["Database:Provider"],
            "Different environments should have different database providers");
        
        devConfig["Queue:Provider"].Should().NotBe(prodConfig["Queue:Provider"],
            "Different environments should have different queue providers");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Configuration_ShouldHaveRequiredSections(string environment)
    {
        // Arrange
        var configuration = BuildConfiguration(environment);

        // Assert
        configuration.GetSection("Database").Exists().Should().BeTrue($"{environment} should have Database section");
        configuration.GetSection("Queue").Exists().Should().BeTrue($"{environment} should have Queue section");
        configuration.GetSection("Processing").Exists().Should().BeTrue($"{environment} should have Processing section");
        configuration.GetSection("Logging").Exists().Should().BeTrue($"{environment} should have Logging section");
    }

    [Fact]
    public void ProductionConfiguration_ShouldNotContainSensitiveData()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var prodConfigPath = Path.Combine(repoRoot, "src", "SwiftMessageProcessor.Api", "appsettings.Production.json");
        var content = File.ReadAllText(prodConfigPath);

        // Assert
        content.Should().NotContain("password=", "Production config should not contain plain text passwords");
        content.Should().NotContain("Password=", "Production config should not contain plain text passwords");
        content.Should().NotContain("pwd=", "Production config should not contain plain text passwords");
    }

    private IConfiguration BuildConfiguration(string environment)
    {
        var repoRoot = GetRepositoryRoot();
        var configPath = Path.Combine(repoRoot, "src", "SwiftMessageProcessor.Api");

        return new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();
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
