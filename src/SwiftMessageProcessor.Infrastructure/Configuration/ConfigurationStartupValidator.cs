using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

/// <summary>
/// Hosted service that validates configuration at application startup
/// </summary>
public class ConfigurationStartupValidator : IHostedService
{
    private readonly ILogger<ConfigurationStartupValidator> _logger;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly IOptions<QueueOptions> _queueOptions;
    private readonly IOptions<ProcessingOptions> _processingOptions;
    private readonly IOptions<CommunicationOptions> _communicationOptions;
    private readonly IOptions<TestModeOptions> _testModeOptions;
    private readonly ConfigurationValidator _validator;

    public ConfigurationStartupValidator(
        ILogger<ConfigurationStartupValidator> _logger,
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<QueueOptions> queueOptions,
        IOptions<ProcessingOptions> processingOptions,
        IOptions<CommunicationOptions> communicationOptions,
        IOptions<TestModeOptions> testModeOptions,
        ConfigurationValidator validator)
    {
        this._logger = _logger;
        _databaseOptions = databaseOptions;
        _queueOptions = queueOptions;
        _processingOptions = processingOptions;
        _communicationOptions = communicationOptions;
        _testModeOptions = testModeOptions;
        _validator = validator;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating application configuration...");

        try
        {
            // Trigger options validation by accessing the Value property
            var dbOptions = _databaseOptions.Value;
            var queueOptions = _queueOptions.Value;
            var processingOptions = _processingOptions.Value;
            var commOptions = _communicationOptions.Value;
            var testOptions = _testModeOptions.Value;

            // Perform comprehensive validation
            var result = _validator.ValidateAll(
                dbOptions,
                queueOptions,
                processingOptions,
                commOptions,
                testOptions);

            if (!result.IsValid)
            {
                _logger.LogCritical("Configuration validation failed:{NewLine}{Errors}",
                    Environment.NewLine,
                    result.GetErrorMessage());
                throw new InvalidOperationException($"Configuration validation failed: {result.GetErrorMessage()}");
            }

            _logger.LogInformation("Configuration validation completed successfully");
            LogConfigurationSummary(dbOptions, queueOptions, processingOptions, commOptions, testOptions);
        }
        catch (OptionsValidationException ex)
        {
            _logger.LogCritical(ex, "Configuration validation failed: {Message}", ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void LogConfigurationSummary(
        DatabaseOptions dbOptions,
        QueueOptions queueOptions,
        ProcessingOptions processingOptions,
        CommunicationOptions commOptions,
        TestModeOptions testOptions)
    {
        _logger.LogInformation("Configuration Summary:");
        _logger.LogInformation("  Database Provider: {Provider}", dbOptions.Provider);
        _logger.LogInformation("  Queue Provider: {Provider}", queueOptions.Provider);
        _logger.LogInformation("  Max Concurrent Messages: {MaxConcurrent}", processingOptions.MaxConcurrentMessages);
        _logger.LogInformation("  Test Mode Enabled: {TestMode}", testOptions.Enabled);
        
        if (queueOptions.Provider.Equals("AmazonSQS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("  AWS Region: {Region}", queueOptions.Region);
        }
    }
}
