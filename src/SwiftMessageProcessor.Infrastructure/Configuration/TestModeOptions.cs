using Microsoft.Extensions.Options;

namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class TestModeOptions
{
    public const string SectionName = "TestMode";
    
    public bool Enabled { get; set; } = false;
    public TimeSpan GenerationInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int ValidMessagePercentage { get; set; } = 80;
    public int BatchSize { get; set; } = 1;
}

public class TestModeOptionsValidator : IValidateOptions<TestModeOptions>
{
    public ValidateOptionsResult Validate(string? name, TestModeOptions options)
    {
        var failures = new List<string>();
        
        if (options.GenerationInterval < TimeSpan.FromSeconds(1))
            failures.Add("Generation interval must be at least 1 second");
            
        if (options.ValidMessagePercentage < 0 || options.ValidMessagePercentage > 100)
            failures.Add("Valid message percentage must be between 0 and 100");
            
        if (options.BatchSize < 1)
            failures.Add("Batch size must be at least 1");
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
