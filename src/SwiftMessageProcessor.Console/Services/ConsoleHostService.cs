using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Console.Services;

/// <summary>
/// Background service that hosts the message processing pipeline and handles inter-process communication
/// </summary>
public class ConsoleHostService : BackgroundService
{
    private readonly IMessageProcessingService _messageProcessor;
    private readonly IProcessCommunicationService _communicationService;
    private readonly IQueueService _queueService;
    private readonly ILogger<ConsoleHostService> _logger;
    private readonly CommunicationOptions _communicationOptions;
    private readonly QueueOptions _queueOptions;
    private readonly CancellationTokenSource _processingCts = new();
    private Task? _processingTask;
    private bool _isProcessing;

    public ConsoleHostService(
        IMessageProcessingService messageProcessor,
        IProcessCommunicationService communicationService,
        IQueueService queueService,
        ILogger<ConsoleHostService> logger,
        IOptions<CommunicationOptions> communicationOptions,
        IOptions<QueueOptions> queueOptions)
    {
        _messageProcessor = messageProcessor;
        _communicationService = communicationService;
        _queueService = queueService;
        _logger = logger;
        _communicationOptions = communicationOptions.Value;
        _queueOptions = queueOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Console Host Service starting");

        try
        {
            // Start listening for commands from Web API
            await _communicationService.StartListeningAsync(HandleCommandAsync, stoppingToken);
            
            // Start status update loop
            var statusUpdateTask = StartStatusUpdateLoopAsync(stoppingToken);
            
            // Start message processing
            await StartProcessingAsync();

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Console Host Service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Console Host Service encountered an error");
            throw;
        }
        finally
        {
            await StopProcessingAsync();
            _logger.LogInformation("Console Host Service stopped");
        }
    }

    /// <summary>
    /// Handles commands received from the Web API
    /// </summary>
    private async Task HandleCommandAsync(ProcessCommand command)
    {
        _logger.LogInformation("Handling command: {Command}", command);

        try
        {
            switch (command)
            {
                case ProcessCommand.Start:
                    await StartProcessingAsync();
                    break;

                case ProcessCommand.Stop:
                    await StopProcessingAsync();
                    break;

                case ProcessCommand.Restart:
                    await StopProcessingAsync();
                    await Task.Delay(1000); // Brief pause before restart
                    await StartProcessingAsync();
                    break;

                case ProcessCommand.GetStatus:
                    // Status is updated automatically by the status update loop
                    break;

                case ProcessCommand.EnableTestMode:
                    _logger.LogInformation("Test mode enable command received (will be implemented in task 6)");
                    break;

                case ProcessCommand.DisableTestMode:
                    _logger.LogInformation("Test mode disable command received (will be implemented in task 6)");
                    break;

                default:
                    _logger.LogWarning("Unknown command received: {Command}", command);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command {Command}", command);
        }
    }

    /// <summary>
    /// Starts the message processing pipeline
    /// </summary>
    private async Task StartProcessingAsync()
    {
        if (_isProcessing)
        {
            _logger.LogWarning("Processing is already running");
            return;
        }

        _logger.LogInformation("Starting message processing");
        _isProcessing = true;

        try
        {
            await _messageProcessor.StartProcessingAsync(_processingCts.Token);
            
            // Start the processing loop in a background task
            _processingTask = Task.Run(async () =>
            {
                await ProcessMessagesLoopAsync(_processingCts.Token);
            }, _processingCts.Token);

            _logger.LogInformation("Message processing started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start message processing");
            _isProcessing = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the message processing pipeline
    /// </summary>
    private async Task StopProcessingAsync()
    {
        if (!_isProcessing)
        {
            _logger.LogWarning("Processing is not running");
            return;
        }

        _logger.LogInformation("Stopping message processing");
        _isProcessing = false;

        try
        {
            // Cancel the processing token
            _processingCts.Cancel();

            // Wait for processing task to complete
            if (_processingTask != null)
            {
                await _processingTask;
            }

            await _messageProcessor.StopProcessingAsync();
            
            _logger.LogInformation("Message processing stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping message processing");
        }
    }

    /// <summary>
    /// Periodically updates the process status for the Web API
    /// </summary>
    private async Task StartStatusUpdateLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting status update loop with interval of {Interval} seconds", 
            _communicationOptions.StatusUpdateIntervalSeconds);

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var systemStatus = await _messageProcessor.GetSystemStatusAsync();
                    var metrics = await _messageProcessor.GetMetricsAsync();
                    var queueStats = await _queueService.GetStatisticsAsync();
                    
                    var processStatus = new ProcessStatus
                    {
                        IsRunning = true,
                        IsProcessing = _isProcessing && systemStatus.IsProcessing,
                        MessagesProcessed = systemStatus.MessagesProcessed,
                        MessagesFailed = systemStatus.MessagesFailed,
                        MessagesPending = queueStats.MessagesInQueue,
                        LastProcessedAt = systemStatus.LastProcessedAt,
                        Status = systemStatus.Status,
                        TestModeEnabled = false, // Will be implemented in task 6
                        Metadata = new Dictionary<string, object>
                        {
                            ["ProcessId"] = Environment.ProcessId,
                            ["MachineName"] = Environment.MachineName,
                            ["StartTime"] = Process.GetCurrentProcess().StartTime,
                            ["AverageProcessingTimeMs"] = metrics.AverageProcessingTimeMs,
                            ["MessagesPerMinute"] = metrics.MessagesPerMinute,
                            ["ErrorsByType"] = metrics.ErrorsByType,
                            ["QueueHealth"] = await _queueService.IsHealthyAsync()
                        }
                    };

                    await _communicationService.UpdateStatusAsync(processStatus, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating process status");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_communicationOptions.StatusUpdateIntervalSeconds), 
                    cancellationToken);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Main message processing loop that reads from the input queue and processes messages
    /// </summary>
    private async Task ProcessMessagesLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting message processing loop, polling queue: {QueueName}", 
            _queueOptions.Settings.InputQueue);

        while (!cancellationToken.IsCancellationRequested && _isProcessing)
        {
            try
            {
                // Check if queue service is healthy
                if (!await _queueService.IsHealthyAsync())
                {
                    _logger.LogWarning("Queue service is not healthy, waiting before retry");
                    await Task.Delay(5000, cancellationToken);
                    continue;
                }

                // Receive message from input queue
                var rawMessage = await _queueService.ReceiveMessageAsync(_queueOptions.Settings.InputQueue);
                
                if (string.IsNullOrEmpty(rawMessage))
                {
                    // No messages available, wait before polling again
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                _logger.LogInformation("Received message from queue, length: {Length}", rawMessage.Length);

                // Process the message
                var result = await _messageProcessor.ProcessMessageAsync(rawMessage);
                
                if (result.Success)
                {
                    _logger.LogInformation("Message processed successfully");
                }
                else
                {
                    _logger.LogError("Message processing failed: {Error}", result.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message processing loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message processing loop");
                // Wait before retrying to avoid tight error loop
                await Task.Delay(5000, cancellationToken);
            }
        }

        _logger.LogInformation("Message processing loop stopped");
    }

    public override void Dispose()
    {
        _processingCts?.Dispose();
        base.Dispose();
    }
}