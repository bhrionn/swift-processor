using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Application.Services;
using SwiftMessageProcessor.Infrastructure.Extensions;
using SwiftMessageProcessor.Api.Hubs;
using SwiftMessageProcessor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add infrastructure services (includes database, queue, and repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Register application services
builder.Services.AddScoped<IMessageProcessingService, MessageProcessingService>();

// Register SignalR hub service
builder.Services.AddSingleton<IMessageHubService, MessageHubService>();

// Register background services
builder.Services.AddHostedService<StatusBroadcastService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ConsoleAppHealthCheck>("console-app");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR hub
app.MapHub<MessageHub>("/hubs/messages");

app.Run();

// Make Program class accessible for integration testing
public partial class Program { }
