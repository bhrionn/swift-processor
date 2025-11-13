using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class CommunicationOptions
{
    public const string SectionName = "Communication";
    
    public string CommunicationDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "swift-processor-ipc");
    public int StatusUpdateIntervalSeconds { get; set; } = 5;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public class CommunicationOptionsValidator : IValidateOptions<CommunicationOptions>
{
    public ValidateOptionsResult Validate(string? name, CommunicationOptions options)
    {
        var failures = new List<string>();
        
        if (string.IsNullOrEmpty(options.CommunicationDirectory))
            failures.Add("Communication directory must be specified");
            
        if (options.StatusUpdateIntervalSeconds < 1)
            failures.Add("Status update interval must be at least 1 second");
            
        if (options.CommandTimeoutSeconds < 1)
            failures.Add("Command timeout must be at least 1 second");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
