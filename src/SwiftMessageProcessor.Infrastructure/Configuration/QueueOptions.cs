using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class QueueOptions
{
    public const string SectionName = "Queue";
    
    public string Provider { get; set; } = string.Empty;
    public string? Region { get; set; }
    public QueueSettings Settings { get; set; } = new();
    
    public void Validate()
    {
        if (string.IsNullOrEmpty(Provider))
            throw new InvalidOperationException("Queue provider must be specified");
    }
}

public class QueueSettings
{
    public string InputQueue { get; set; } = "input-messages";
    public string CompletedQueue { get; set; } = "completed-messages";
    public string DeadLetterQueue { get; set; } = "failed-messages";
}

public class QueueOptionsValidator : IValidateOptions<QueueOptions>
{
    public ValidateOptionsResult Validate(string? name, QueueOptions options)
    {
        var failures = new List<string>();
        
        if (string.IsNullOrEmpty(options.Provider))
            failures.Add("Queue provider must be specified");
            
        var supportedProviders = new[] { "inmemory", "local", "amazonsqs", "sqs" };
        if (!supportedProviders.Contains(options.Provider.ToLowerInvariant()))
            failures.Add($"Queue provider '{options.Provider}' is not supported. Supported providers: {string.Join(", ", supportedProviders)}");
        
        if (string.IsNullOrEmpty(options.Settings.InputQueue))
            failures.Add("Input queue name must be specified");
            
        if (string.IsNullOrEmpty(options.Settings.CompletedQueue))
            failures.Add("Completed queue name must be specified");
            
        if (string.IsNullOrEmpty(options.Settings.DeadLetterQueue))
            failures.Add("Dead letter queue name must be specified");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}