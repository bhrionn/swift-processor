using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Parsers;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Infrastructure.Configuration;
using SwiftMessageProcessor.Infrastructure.Extensions;
using SwiftMessageProcessor.Console.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add infrastructure services (includes database, queue, and repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Configure processing options
builder.Services.Configure<ProcessingOptions>(builder.Configuration.GetSection(ProcessingOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<ProcessingOptions>, ProcessingOptionsValidator>();

// Register parsers
builder.Services.AddScoped<ISwiftMessageParser<MT103Message>, MT103Parser>();

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
Console.WriteLine();

await host.RunAsync();
