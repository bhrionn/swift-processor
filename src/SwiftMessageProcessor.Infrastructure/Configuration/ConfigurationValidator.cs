using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

/// <summary>
/// Validates all configuration options at application startup
/// </summary>
public class ConfigurationValidator
{
    private readonly IEnumerable<IValidateOptions<DatabaseOptions>> _databaseValidators;
    private readonly IEnumerable<IValidateOptions<QueueOptions>> _queueValidators;
    private readonly IEnumerable<IValidateOptions<ProcessingOptions>> _processingValidators;
    private readonly IEnumerable<IValidateOptions<CommunicationOptions>> _communicationValidators;
    private readonly IEnumerable<IValidateOptions<TestModeOptions>> _testModeValidators;

    public ConfigurationValidator(
        IEnumerable<IValidateOptions<DatabaseOptions>> databaseValidators,
        IEnumerable<IValidateOptions<QueueOptions>> queueValidators,
        IEnumerable<IValidateOptions<ProcessingOptions>> processingValidators,
        IEnumerable<IValidateOptions<CommunicationOptions>> communicationValidators,
        IEnumerable<IValidateOptions<TestModeOptions>> testModeValidators)
    {
        _databaseValidators = databaseValidators;
        _queueValidators = queueValidators;
        _processingValidators = processingValidators;
        _communicationValidators = communicationValidators;
        _testModeValidators = testModeValidators;
    }

    /// <summary>
    /// Validates all configuration sections and returns validation results
    /// </summary>
    public ConfigurationValidationResult ValidateAll(
        DatabaseOptions databaseOptions,
        QueueOptions queueOptions,
        ProcessingOptions processingOptions,
        CommunicationOptions communicationOptions,
        TestModeOptions testModeOptions)
    {
        var errors = new List<string>();

        // Validate database options
        foreach (var validator in _databaseValidators)
        {
            var result = validator.Validate(null, databaseOptions);
            if (result.Failed)
            {
                errors.Add($"Database Configuration: {result.FailureMessage}");
            }
        }

        // Validate queue options
        foreach (var validator in _queueValidators)
        {
            var result = validator.Validate(null, queueOptions);
            if (result.Failed)
            {
                errors.Add($"Queue Configuration: {result.FailureMessage}");
            }
        }

        // Validate processing options
        foreach (var validator in _processingValidators)
        {
            var result = validator.Validate(null, processingOptions);
            if (result.Failed)
            {
                errors.Add($"Processing Configuration: {result.FailureMessage}");
            }
        }

        // Validate communication options
        foreach (var validator in _communicationValidators)
        {
            var result = validator.Validate(null, communicationOptions);
            if (result.Failed)
            {
                errors.Add($"Communication Configuration: {result.FailureMessage}");
            }
        }

        // Validate test mode options
        foreach (var validator in _testModeValidators)
        {
            var result = validator.Validate(null, testModeOptions);
            if (result.Failed)
            {
                errors.Add($"TestMode Configuration: {result.FailureMessage}");
            }
        }

        return new ConfigurationValidationResult(errors);
    }
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid => !Errors.Any();
    public IReadOnlyList<string> Errors { get; }

    public ConfigurationValidationResult(IEnumerable<string> errors)
    {
        Errors = errors.ToList().AsReadOnly();
    }

    public string GetErrorMessage()
    {
        return string.Join(Environment.NewLine, Errors);
    }
}
