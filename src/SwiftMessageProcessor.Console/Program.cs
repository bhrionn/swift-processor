using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Infrastructure.Extensions;
using SwiftMessageProcessor.Console.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add infrastructure services (includes database, queue, and repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Register services
builder.Services.AddScoped<IMessageProcessingService, MessageProcessingService>();
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
