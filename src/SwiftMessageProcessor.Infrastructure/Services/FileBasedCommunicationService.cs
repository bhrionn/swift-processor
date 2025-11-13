using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// File-based inter-process communication service for development environments
/// Uses shared file system for command and status exchange
/// </summary>
public class FileBasedCommunicationService : IProcessCommunicationService
{
    private readonly ILogger<FileBasedCommunicationService> _logger;
    private readonly string _commandFilePath;
    private readonly string _statusFilePath;
    private readonly string _communicationDirectory;
    private FileSystemWatcher? _commandWatcher;
    private Func<ProcessCommand, Task>? _commandHandler;

    public FileBasedCommunicationService(
        ILogger<FileBasedCommunicationService> logger,
        IOptions<CommunicationOptions> options)
    {
        _logger = logger;
        _communicationDirectory = options.Value.CommunicationDirectory;
        _commandFilePath = Path.Combine(_communicationDirectory, "command.json");
        _statusFilePath = Path.Combine(_communicationDirectory, "status.json");
        
        // Ensure communication directory exists
        Directory.CreateDirectory(_communicationDirectory);
    }

    public async Task SendCommandAsync(ProcessCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var commandData = new
            {
                Command = command.ToString(),
                Timestamp = DateTime.UtcNow,
                CommandId = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(commandData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_commandFilePath, json, cancellationToken);
            
            _logger.LogInformation("Sent command {Command} to console application", command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {Command}", command);
            throw;
        }
    }

    public async Task<ProcessStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_statusFilePath))
            {
                _logger.LogWarning("Status file not found, console application may not be running");
                return new ProcessStatus
                {
                    IsRunning = false,
                    Status = "Not Running",
                    StatusUpdatedAt = DateTime.UtcNow
                };
            }

            var json = await File.ReadAllTextAsync(_statusFilePath, cancellationToken);
            var status = JsonSerializer.Deserialize<ProcessStatus>(json);
            
            if (status == null)
            {
                _logger.LogWarning("Failed to deserialize status file");
                return new ProcessStatus { IsRunning = false, Status = "Unknown" };
            }

            // Check if status is stale (older than 30 seconds)
            if ((DateTime.UtcNow - status.StatusUpdatedAt).TotalSeconds > 30)
            {
                _logger.LogWarning("Status file is stale, console application may not be responding");
                status.IsRunning = false;
                status.Status = "Not Responding";
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read status file");
            return new ProcessStatus { IsRunning = false, Status = "Error" };
        }
    }

    public async Task<bool> IsConsoleAppHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await GetStatusAsync(cancellationToken);
            return status.IsRunning && (DateTime.UtcNow - status.StatusUpdatedAt).TotalSeconds <= 30;
        }
        catch
        {
            return false;
        }
    }

    public Task StartListeningAsync(Func<ProcessCommand, Task> commandHandler, CancellationToken cancellationToken = default)
    {
        _commandHandler = commandHandler;
        
        // Set up file system watcher for command file
        _commandWatcher = new FileSystemWatcher(_communicationDirectory)
        {
            Filter = "command.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _commandWatcher.Changed += OnCommandFileChanged;
        _commandWatcher.Created += OnCommandFileChanged;
        _commandWatcher.EnableRaisingEvents = true;

        _logger.LogInformation("Started listening for commands in {Directory}", _communicationDirectory);
        
        return Task.CompletedTask;
    }

    public async Task UpdateStatusAsync(ProcessStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            status.StatusUpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_statusFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status file");
        }
    }

    private async void OnCommandFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_commandHandler == null)
            return;

        try
        {
            // Wait a bit to ensure file write is complete
            await Task.Delay(100);

            var json = await File.ReadAllTextAsync(_commandFilePath);
            var commandData = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (commandData.TryGetProperty("Command", out var commandElement))
            {
                var commandString = commandElement.GetString();
                if (Enum.TryParse<ProcessCommand>(commandString, out var command))
                {
                    _logger.LogInformation("Received command: {Command}", command);
                    await _commandHandler(command);
                    
                    // Delete command file after processing
                    try
                    {
                        File.Delete(_commandFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete command file after processing");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command file");
        }
    }

    public void Dispose()
    {
        _commandWatcher?.Dispose();
    }
}
