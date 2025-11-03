using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Repositories;

namespace SwiftMessageProcessor.Infrastructure.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ServiceProvider ServiceProvider;
    protected readonly SwiftMessageContext Context;
    protected readonly ILogger<MessageRepository> Logger;
    
    protected TestBase()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<SwiftMessageContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        ServiceProvider = services.BuildServiceProvider();
        Context = ServiceProvider.GetRequiredService<SwiftMessageContext>();
        Logger = ServiceProvider.GetRequiredService<ILogger<MessageRepository>>();
        
        // Ensure database is created
        Context.Database.EnsureCreated();
    }
    
    public void Dispose()
    {
        Context?.Dispose();
        ServiceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}