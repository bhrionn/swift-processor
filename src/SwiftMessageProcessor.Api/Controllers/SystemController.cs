using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IProcessCommunicationService _communicationService;
    private readonly IAuditLoggingService _auditService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        IProcessCommunicationService communicationService,
        IAuditLoggingService auditService,
        ILogger<SystemController> logger)
    {
        _communicationService = communicationService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current system status from the console application
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SystemStatusDto>> GetStatus()
    {
        try
        {
            _logger.LogInformation("Retrieving system status from console application");

            var status = await _communicationService.GetStatusAsync();
            var dto = SystemStatusDto.FromProcessStatus(status);

            _logger.LogInformation("System status retrieved: IsRunning={IsRunning}, MessagesProcessed={MessagesProcessed}", 
                status.IsRunning, status.MessagesProcessed);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system status");
            return StatusCode(503, new { error = "Console application unavailable", details = ex.Message });
        }
    }

    /// <summary>
    /// Checks if the console application is healthy
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthCheckDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckDto>> CheckHealth()
    {
        try
        {
            _logger.LogInformation("Checking console application health");

            var isHealthy = await _communicationService.IsConsoleAppHealthyAsync();

            var result = new HealthCheckDto
            {
                IsHealthy = isHealthy,
                CheckedAt = DateTime.UtcNow,
                Status = isHealthy ? "Healthy" : "Unhealthy"
            };

            _logger.LogInformation("Console application health check: {Status}", result.Status);

            return isHealthy ? Ok(result) : StatusCode(503, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new HealthCheckDto
            {
                IsHealthy = false,
                CheckedAt = DateTime.UtcNow,
                Status = "Unavailable",
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Starts the message processor in the console application
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartProcessor()
    {
        try
        {
            _logger.LogInformation("Sending start command to console application");

            await _communicationService.SendCommandAsync(ProcessCommand.Start);
            
            // Log administrative action
            await _auditService.LogAdministrativeActionAsync(
                "StartProcessor",
                "Message processor start command sent",
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            _logger.LogInformation("Start command sent successfully");
            return Ok(new { message = "Processor start command sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start processor");
            return StatusCode(503, new { error = "Failed to communicate with console application", details = ex.Message });
        }
    }

    /// <summary>
    /// Stops the message processor in the console application
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StopProcessor()
    {
        try
        {
            _logger.LogInformation("Sending stop command to console application");

            await _communicationService.SendCommandAsync(ProcessCommand.Stop);
            
            // Log administrative action
            await _auditService.LogAdministrativeActionAsync(
                "StopProcessor",
                "Message processor stop command sent",
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            _logger.LogInformation("Stop command sent successfully");
            return Ok(new { message = "Processor stop command sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop processor");
            return StatusCode(503, new { error = "Failed to communicate with console application", details = ex.Message });
        }
    }

    /// <summary>
    /// Restarts the message processor in the console application
    /// </summary>
    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RestartProcessor()
    {
        try
        {
            _logger.LogInformation("Sending restart command to console application");

            await _communicationService.SendCommandAsync(ProcessCommand.Restart);
            
            // Log administrative action
            await _auditService.LogAdministrativeActionAsync(
                "RestartProcessor",
                "Message processor restart command sent",
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            _logger.LogInformation("Restart command sent successfully");
            return Ok(new { message = "Processor restart command sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart processor");
            return StatusCode(503, new { error = "Failed to communicate with console application", details = ex.Message });
        }
    }

    /// <summary>
    /// Enables test mode in the console application
    /// </summary>
    [HttpPost("test-mode/enable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnableTestMode()
    {
        try
        {
            _logger.LogInformation("Sending enable test mode command to console application");

            await _communicationService.SendCommandAsync(ProcessCommand.EnableTestMode);
            
            // Log administrative action
            await _auditService.LogAdministrativeActionAsync(
                "EnableTestMode",
                "Test mode enabled",
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            _logger.LogInformation("Enable test mode command sent successfully");
            return Ok(new { message = "Test mode enabled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable test mode");
            return StatusCode(503, new { error = "Failed to communicate with console application", details = ex.Message });
        }
    }

    /// <summary>
    /// Disables test mode in the console application
    /// </summary>
    [HttpPost("test-mode/disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DisableTestMode()
    {
        try
        {
            _logger.LogInformation("Sending disable test mode command to console application");

            await _communicationService.SendCommandAsync(ProcessCommand.DisableTestMode);
            
            // Log administrative action
            await _auditService.LogAdministrativeActionAsync(
                "DisableTestMode",
                "Test mode disabled",
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            _logger.LogInformation("Disable test mode command sent successfully");
            return Ok(new { message = "Test mode disabled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable test mode");
            return StatusCode(503, new { error = "Failed to communicate with console application", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current test mode configuration
    /// </summary>
    [HttpGet("test-mode")]
    [ProducesResponseType(typeof(TestModeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TestModeDto>> GetTestMode()
    {
        try
        {
            _logger.LogInformation("Retrieving test mode status");

            var status = await _communicationService.GetStatusAsync();

            var result = new TestModeDto
            {
                Enabled = status.TestModeEnabled,
                RetrievedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Test mode status: {Enabled}", result.Enabled);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve test mode status");
            return StatusCode(503, new { error = "Console application unavailable", details = ex.Message });
        }
    }
}

public class SystemStatusDto
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

    public static SystemStatusDto FromProcessStatus(ProcessStatus status)
    {
        return new SystemStatusDto
        {
            IsRunning = status.IsRunning,
            IsProcessing = status.IsProcessing,
            MessagesProcessed = status.MessagesProcessed,
            MessagesFailed = status.MessagesFailed,
            MessagesPending = status.MessagesPending,
            LastProcessedAt = status.LastProcessedAt,
            StatusUpdatedAt = status.StatusUpdatedAt,
            Status = status.Status,
            TestModeEnabled = status.TestModeEnabled,
            Metadata = status.Metadata
        };
    }
}

public class HealthCheckDto
{
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class TestModeDto
{
    public bool Enabled { get; set; }
    public DateTime RetrievedAt { get; set; }
}