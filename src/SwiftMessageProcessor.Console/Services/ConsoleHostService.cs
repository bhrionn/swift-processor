using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.Console.Services;

public class ConsoleHostService : BackgroundService
{
    private readonly IMessageProcessingService _messageProcessor;
    private readonly ILogger<ConsoleHostService> _logger;

    public ConsoleHostService(
        IMessageProcessingService messageProcessor,
        ILogger<ConsoleHostService> logger)
    {
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Console Host Service starting");

        try
        {
            await _messageProcessor.StartProcessingAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Main processing loop will be implemented in later tasks
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Console Host Service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Console Host Service encountered an error");
        }
        finally
        {
            await _messageProcessor.StopProcessingAsync();
            _logger.LogInformation("Console Host Service stopped");
        }
    }
}