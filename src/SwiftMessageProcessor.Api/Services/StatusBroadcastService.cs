using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Api.Hubs;

namespace SwiftMessageProcessor.Api.Services;

/// <summary>
/// Background service that periodically broadcasts system status to connected clients
/// </summary>
public class StatusBroadcastService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StatusBroadcastService> _logger;
    private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(5);

    public StatusBroadcastService(
        IServiceProvider serviceProvider,
        ILogger<StatusBroadcastService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Status broadcast service starting");

        // Wait a bit before starting to allow services to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var communicationService = scope.ServiceProvider.GetRequiredService<IProcessCommunicationService>();
                var hubService = scope.ServiceProvider.GetRequiredService<IMessageHubService>();

                // Get status from console application
                var status = await communicationService.GetStatusAsync(stoppingToken);

                // Broadcast to all connected clients
                await hubService.BroadcastSystemStatusAsync(status);

                _logger.LogDebug("Broadcasted system status: IsRunning={IsRunning}, MessagesProcessed={MessagesProcessed}",
                    status.IsRunning, status.MessagesProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system status");
            }

            await Task.Delay(_broadcastInterval, stoppingToken);
        }

        _logger.LogInformation("Status broadcast service stopping");
    }
}
