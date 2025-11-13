using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class ProcessingOptions
{
    public const string SectionName = "Processing";
    
    public int MaxConcurrentMessages { get; set; } = 10;
    public int MessageProcessingTimeoutSeconds { get; set; } = 60;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public int QueuePollingIntervalMilliseconds { get; set; } = 1000;
}

public class ProcessingOptionsValidator : IValidateOptions<ProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, ProcessingOptions options)
    {
        var failures = new List<string>();
        
        if (options.MaxConcurrentMessages < 1)
            failures.Add("Max concurrent messages must be at least 1");
            
        if (options.MessageProcessingTimeoutSeconds < 1)
            failures.Add("Message processing timeout must be at least 1 second");
            
        if (options.RetryAttempts < 0)
            failures.Add("Retry attempts cannot be negative");
            
        if (options.RetryDelaySeconds < 1)
            failures.Add("Retry delay must be at least 1 second");
            
        if (options.QueuePollingIntervalMilliseconds < 100)
            failures.Add("Queue polling interval must be at least 100 milliseconds");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
