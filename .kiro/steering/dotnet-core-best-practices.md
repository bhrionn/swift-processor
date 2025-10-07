---
inclusion: always
---

# .NET Core Best Practices

## Project Structure and Organization

### Solution Structure
```
SwiftMessageProcessor/
├── src/
│   ├── SwiftMessageProcessor.Api/          # Web API project
│   ├── SwiftMessageProcessor.Core/         # Domain models and interfaces
│   ├── SwiftMessageProcessor.Infrastructure/ # Data access and external services
│   └── SwiftMessageProcessor.Application/   # Business logic and services
├── tests/
│   ├── SwiftMessageProcessor.UnitTests/
│   ├── SwiftMessageProcessor.IntegrationTests/
│   └── SwiftMessageProcessor.Api.Tests/
└── docker/
    ├── Dockerfile.api
    └── docker-compose.yml
```

### Namespace Conventions
- Use company/project prefix: `SwiftMessageProcessor.Core.Models`
- Follow folder structure: `SwiftMessageProcessor.Infrastructure.Repositories`
- Keep namespaces concise and meaningful

## SOLID Principles Implementation

### Dependency Injection
```csharp
// Program.cs - Service registration
builder.Services.AddScoped<IMessageProcessingService, MessageProcessingService>();
builder.Services.AddScoped<ISwiftMessageParser<MT103Message>, MT103Parser>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

// Use factory pattern for multiple implementations
builder.Services.AddScoped<IQueueServiceFactory, QueueServiceFactory>();
builder.Services.AddScoped<IQueueService>(provider =>
{
    var factory = provider.GetRequiredService<IQueueServiceFactory>();
    var config = provider.GetRequiredService<IConfiguration>();
    return factory.CreateQueueService(config["Queue:Provider"]);
});
```

### Interface Design
```csharp
// Single Responsibility - focused interfaces
public interface ISwiftMessageParser<T> where T : SwiftMessage
{
    Task<T> ParseAsync(string rawMessage);
    Task<ValidationResult> ValidateAsync(T message);
    bool CanParse(string messageType);
}

// Open/Closed - extensible through inheritance
public abstract class SwiftMessageParser<T> : ISwiftMessageParser<T> where T : SwiftMessage
{
    protected abstract T CreateMessage();
    protected abstract Dictionary<string, Func<string, object>> GetFieldParsers();
    
    public virtual async Task<T> ParseAsync(string rawMessage)
    {
        // Common parsing logic
    }
}
```

## Configuration Management

### Strongly-Typed Configuration
```csharp
// Configuration classes
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

// Registration and validation
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
```

### Environment-Specific Settings
```json
// appsettings.Development.json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=messages.db"
  },
  "Queue": {
    "Provider": "InMemory"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SwiftMessageProcessor": "Trace"
    }
  }
}

// appsettings.Production.json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=prod-server;Database=SwiftMessages;Integrated Security=true"
  },
  "Queue": {
    "Provider": "AmazonSQS",
    "Region": "us-east-1"
  }
}
```

## Entity Framework Best Practices

### DbContext Configuration
```csharp
public class SwiftMessageContext : DbContext
{
    public SwiftMessageContext(DbContextOptions<SwiftMessageContext> options) : base(options) { }
    
    public DbSet<ProcessedMessage> Messages { get; set; }
    public DbSet<SystemAuditEntry> SystemAudit { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SwiftMessageContext).Assembly);
        
        // Configure indexes
        modelBuilder.Entity<ProcessedMessage>()
            .HasIndex(m => m.Status)
            .HasDatabaseName("IX_Messages_Status");
            
        modelBuilder.Entity<ProcessedMessage>()
            .HasIndex(m => new { m.MessageType, m.ProcessedAt })
            .HasDatabaseName("IX_Messages_Type_ProcessedAt");
    }
}
```

### Repository Pattern
```csharp
public class MessageRepository : IMessageRepository
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<MessageRepository> _logger;
    
    public MessageRepository(SwiftMessageContext context, ILogger<MessageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<ProcessedMessage?> GetByIdAsync(Guid id)
    {
        return await _context.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }
    
    public async Task<IEnumerable<ProcessedMessage>> GetByFilterAsync(MessageFilter filter)
    {
        var query = _context.Messages.AsNoTracking();
        
        if (filter.Status.HasValue)
            query = query.Where(m => m.Status == filter.Status.Value);
            
        if (filter.FromDate.HasValue)
            query = query.Where(m => m.ProcessedAt >= filter.FromDate.Value);
            
        return await query
            .OrderByDescending(m => m.ProcessedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync();
    }
}
```

## Error Handling and Resilience

### Global Exception Handling
```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = exception switch
        {
            ValidationException => new { error = "Validation failed", details = exception.Message },
            NotFoundException => new { error = "Resource not found", details = exception.Message },
            _ => new { error = "Internal server error", details = "An unexpected error occurred" }
        };
        
        context.Response.StatusCode = exception switch
        {
            ValidationException => 400,
            NotFoundException => 404,
            _ => 500
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

### Retry Policies with Polly
```csharp
// Service registration
builder.Services.AddHttpClient<IExternalService, ExternalService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("Retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
            });
}
```

## Logging and Monitoring

### Structured Logging
```csharp
public class MessageProcessingService : IMessageProcessingService
{
    private readonly ILogger<MessageProcessingService> _logger;
    
    public async Task<ProcessingResult> ProcessMessageAsync(string rawMessage)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = Guid.NewGuid(),
            ["Operation"] = "ProcessMessage"
        });
        
        _logger.LogInformation("Starting message processing for message length {MessageLength}", rawMessage.Length);
        
        try
        {
            var result = await ProcessInternalAsync(rawMessage);
            
            _logger.LogInformation("Message processing completed successfully with status {Status}", result.Status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing failed");
            throw;
        }
    }
}
```

### Health Checks
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SwiftMessageContext>("database")
    .AddCheck<QueueHealthCheck>("queue")
    .AddCheck<ExternalServiceHealthCheck>("external-service");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Custom health check
public class QueueHealthCheck : IHealthCheck
{
    private readonly IQueueService _queueService;
    
    public QueueHealthCheck(IQueueService queueService)
    {
        _queueService = queueService;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _queueService.IsHealthyAsync();
            return isHealthy 
                ? HealthCheckResult.Healthy("Queue service is responsive")
                : HealthCheckResult.Unhealthy("Queue service is not responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Queue health check failed", ex);
        }
    }
}
```

## API Design Best Practices

### Controller Design
```csharp
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MessagesController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<MessagesController> _logger;
    
    public MessagesController(IMessageProcessingService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }
    
    /// <summary>
    /// Retrieves messages based on filter criteria
    /// </summary>
    /// <param name="filter">Filter criteria for message retrieval</param>
    /// <returns>Paginated list of messages</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages([FromQuery] MessageFilterDto filter)
    {
        var messages = await _messageService.GetMessagesAsync(filter.ToFilter());
        var result = new PagedResult<MessageDto>
        {
            Items = messages.Select(MessageDto.FromMessage),
            TotalCount = await _messageService.GetMessageCountAsync(filter.ToFilter())
        };
        
        return Ok(result);
    }
}
```

### Model Validation
```csharp
public class MessageFilterDto
{
    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;
    
    [Range(1, 100)]
    public int Take { get; set; } = 20;
    
    public MessageStatus? Status { get; set; }
    
    [DataType(DataType.DateTime)]
    public DateTime? FromDate { get; set; }
    
    [DataType(DataType.DateTime)]
    public DateTime? ToDate { get; set; }
    
    public MessageFilter ToFilter() => new()
    {
        Skip = Skip,
        Take = Take,
        Status = Status,
        FromDate = FromDate,
        ToDate = ToDate
    };
}
```

## Testing Standards

### Unit Testing
```csharp
public class MT103ParserTests
{
    private readonly MT103Parser _parser;
    private readonly ILogger<MT103Parser> _logger;
    
    public MT103ParserTests()
    {
        _logger = Substitute.For<ILogger<MT103Parser>>();
        _parser = new MT103Parser(_logger);
    }
    
    [Fact]
    public async Task ParseAsync_ValidMT103Message_ReturnsCorrectlyParsedMessage()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();
        
        // Act
        var result = await _parser.ParseAsync(rawMessage);
        
        // Assert
        result.Should().NotBeNull();
        result.TransactionReference.Should().Be("REFERENCE12345");
        result.Amount.Should().Be(123456.78m);
        result.Currency.Should().Be("EUR");
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("{1:F01INVALID")]
    public async Task ParseAsync_InvalidMessage_ThrowsValidationException(string invalidMessage)
    {
        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(invalidMessage))
            .Should().ThrowAsync<ValidationException>();
    }
}
```

### Integration Testing
```csharp
public class MessageProcessingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public MessageProcessingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace with test database
                services.RemoveAll<DbContextOptions<SwiftMessageContext>>();
                services.AddDbContext<SwiftMessageContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            });
        });
        
        _client = _factory.CreateClient();
    }
    
    [Fact]
    public async Task GetMessages_ReturnsExpectedMessages()
    {
        // Arrange
        await SeedTestData();
        
        // Act
        var response = await _client.GetAsync("/api/messages");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<MessageDto>>(content);
        result.Items.Should().HaveCount(2);
    }
}
```

## Performance Optimization

### Async/Await Best Practices
```csharp
// Use ConfigureAwait(false) in library code
public async Task<ProcessingResult> ProcessMessageAsync(string message)
{
    var parsed = await ParseMessageAsync(message).ConfigureAwait(false);
    var validated = await ValidateMessageAsync(parsed).ConfigureAwait(false);
    return await StoreMessageAsync(validated).ConfigureAwait(false);
}

// Use ValueTask for frequently called methods that may complete synchronously
public ValueTask<bool> IsValidAsync(string message)
{
    if (string.IsNullOrEmpty(message))
        return ValueTask.FromResult(false);
        
    return ValidateInternalAsync(message);
}
```

### Memory Management
```csharp
// Use object pooling for frequently created objects
public class MessageParserPool : ObjectPool<MT103Parser>
{
    public override MT103Parser Get() => new MT103Parser();
    public override void Return(MT103Parser obj) => obj.Reset();
}

// Implement IDisposable properly
public class MessageProcessor : IMessageProcessor, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}
```

## Security Best Practices

### Input Validation
```csharp
public class SwiftMessageValidator
{
    private static readonly Regex MessageTypeRegex = new(@"^MT\d{3}$", RegexOptions.Compiled);
    
    public ValidationResult ValidateMessage(string rawMessage)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(rawMessage))
            errors.Add("Message cannot be empty");
            
        if (rawMessage.Length > 10000)
            errors.Add("Message exceeds maximum length");
            
        // Additional validation rules
        
        return new ValidationResult(errors);
    }
}
```

### Secure Configuration
```csharp
// Use IOptions pattern with validation
[OptionsValidator]
public partial class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();
        
        if (string.IsNullOrEmpty(options.ConnectionString))
            failures.Add("Connection string is required");
            
        if (options.ConnectionString?.Contains("password=", StringComparison.OrdinalIgnoreCase) == true)
            failures.Add("Connection string should not contain plain text passwords");
            
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```