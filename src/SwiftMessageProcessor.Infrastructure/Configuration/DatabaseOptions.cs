namespace SwiftMessageProcessor.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    
    public void Validate()
    {
        if (string.IsNullOrEmpty(Provider))
            throw new InvalidOperationException("Database provider must be specified");
        if (string.IsNullOrEmpty(ConnectionString))
            throw new InvalidOperationException("Database connection string must be specified");
    }
}