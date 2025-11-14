using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.E2E.Tests;

/// <summary>
/// Test fixture that sets up both the Web API and simulates the Console Application
/// for end-to-end testing
/// </summary>
public class E2ETestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private IHost? _consoleAppHost;
    private HubConnection? _signalRConnection;
    
    public IQueueService? QueueService { get; private set; }
    public IMessageRepository? MessageRepository { get; private set; }
    public IMessageProcessingService? ProcessingService { get; private set; }
    public HubConnection? SignalRConnection => _signalRConnection;
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.E2E.json");
            config.AddJsonFile(testConfigPath, optional: false);
        });

        builder.ConfigureServices(services =>
        {
            // Replace database with in-memory database
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SwiftMessageContext>));
            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }
            
            services.AddDbContext<SwiftMessageContext>(options =>
                options.UseInMemoryDatabase("E2ETestDb"));

            // Build service provider to get shared services
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            
            QueueService = scopedServices.GetRequiredService<IQueueService>();
            MessageRepository = scopedServices.GetRequiredService<IMessageRepository>();
            
            var db = scopedServices.GetRequiredService<SwiftMessageContext>();
            db.Database.EnsureCreated();
        });
    }

    public async Task InitializeAsync()
    {
        // Start the Web API
        await Task.CompletedTask;
        
        // Initialize SignalR connection
        var client = CreateClient();
        _signalRConnection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}messageHub", options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            })
            .Build();
            
        await _signalRConnection.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_signalRConnection != null)
        {
            await _signalRConnection.StopAsync();
            await _signalRConnection.DisposeAsync();
        }
        
        if (_consoleAppHost != null)
        {
            await _consoleAppHost.StopAsync();
            _consoleAppHost.Dispose();
        }
        
        await base.DisposeAsync();
    }

    /// <summary>
    /// Simulates the console application processing messages
    /// </summary>
    public async Task<ProcessingResult> SimulateMessageProcessingAsync(string rawMessage)
    {
        if (ProcessingService == null)
        {
            using var scope = Services.CreateScope();
            ProcessingService = scope.ServiceProvider.GetRequiredService<IMessageProcessingService>();
        }
        
        return await ProcessingService.ProcessMessageAsync(rawMessage);
    }

    /// <summary>
    /// Adds a message to the input queue
    /// </summary>
    public async Task EnqueueMessageAsync(string message)
    {
        if (QueueService == null)
        {
            using var scope = Services.CreateScope();
            QueueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
        }
        
        await QueueService.SendMessageAsync("e2e-input-messages", message);
    }

    /// <summary>
    /// Gets the count of messages in a specific queue
    /// </summary>
    public async Task<int> GetQueueMessageCountAsync(string queueName)
    {
        if (QueueService == null)
        {
            using var scope = Services.CreateScope();
            QueueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
        }
        
        var stats = await QueueService.GetStatisticsAsync();
        return stats.MessagesInQueue;
    }

    /// <summary>
    /// Clears all queues
    /// </summary>
    public async Task ClearQueuesAsync()
    {
        if (QueueService is LocalQueueService localQueue)
        {
            // Clear all messages from local queues
            while (await localQueue.ReceiveMessageAsync("e2e-input-messages") != null) { }
            while (await localQueue.ReceiveMessageAsync("e2e-completed-messages") != null) { }
            while (await localQueue.ReceiveMessageAsync("e2e-failed-messages") != null) { }
        }
    }

    /// <summary>
    /// Clears the database
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SwiftMessageContext>();
        context.Messages.RemoveRange(context.Messages);
        await context.SaveChangesAsync();
    }
}
