using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Infrastructure.Services;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Console.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<QueueOptions>(
    builder.Configuration.GetSection(QueueOptions.SectionName));

// Register services
builder.Services.AddScoped<IMessageProcessingService, MessageProcessingService>();
builder.Services.AddScoped<IQueueService, LocalQueueService>();
builder.Services.AddHostedService<ConsoleHostService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();

Console.WriteLine("SWIFT Message Processor Console Application");
Console.WriteLine("Press Ctrl+C to stop the application");

await host.RunAsync();
