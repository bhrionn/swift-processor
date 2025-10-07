using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Core.Interfaces;

namespace SwiftMessageProcessor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(IMessageProcessingService messageService, ILogger<SystemController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current system status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusDto>> GetStatus()
    {
        var status = await _messageService.GetSystemStatusAsync();
        var dto = new SystemStatusDto
        {
            IsProcessing = status.IsProcessing,
            MessagesProcessed = status.MessagesProcessed,
            MessagesFailed = status.MessagesFailed,
            LastProcessedAt = status.LastProcessedAt,
            Status = status.Status
        };
        
        return Ok(dto);
    }

    /// <summary>
    /// Restarts the message processor
    /// </summary>
    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RestartProcessor()
    {
        try
        {
            await _messageService.StopProcessingAsync();
            await _messageService.StartProcessingAsync(CancellationToken.None);
            
            _logger.LogInformation("Processor restarted successfully");
            return Ok(new { message = "Processor restarted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart processor");
            return StatusCode(500, new { error = "Failed to restart processor", details = ex.Message });
        }
    }
}

public class SystemStatusDto
{
    public bool IsProcessing { get; set; }
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public DateTime LastProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}