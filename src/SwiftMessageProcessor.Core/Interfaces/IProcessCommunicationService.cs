namespace SwiftMessageProcessor.Core.Interfaces;

/// <summary>
/// Service for inter-process communication between Web API and Console Application
/// </summary>
public interface IProcessCommunicationService
{
    /// <summary>
    /// Sends a command to the console application
    /// </summary>
    Task SendCommandAsync(ProcessCommand command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current status of the console application
    /// </summary>
    Task<ProcessStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the console application is healthy and responsive
    /// </summary>
    Task<bool> IsConsoleAppHealthyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts listening for commands (Console app side)
    /// </summary>
    Task StartListeningAsync(Func<ProcessCommand, Task> commandHandler, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the current process status (Console app side)
    /// </summary>
    Task UpdateStatusAsync(ProcessStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// Commands that can be sent to the console application
/// </summary>
public enum ProcessCommand
{
    Start,
    Stop,
    Restart,
    GetStatus,
    EnableTestMode,
    DisableTestMode
}

/// <summary>
/// Status information about the console application
/// </summary>
public class ProcessStatus
{
    public bool IsRunning { get; set; }
    public bool IsProcessing { get; set; }
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public int MessagesPending { get; set; }
    public DateTime LastProcessedAt { get; set; }
    public DateTime StatusUpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool TestModeEnabled { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
