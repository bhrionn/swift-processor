using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Infrastructure.Services;

namespace SwiftMessageProcessor.Api.Controllers;

/// <summary>
/// Controller for application metrics and monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly MetricsCollectionService _metricsService;
    private readonly ErrorLoggingService _errorLoggingService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        MetricsCollectionService metricsService,
        ErrorLoggingService errorLoggingService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _errorLoggingService = errorLoggingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets current application metrics
    /// </summary>
    [HttpGet]
    public ActionResult<MetricsSnapshot> GetMetrics()
    {
        try
        {
            var snapshot = _metricsService.GetSnapshot();
            return Ok(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metrics");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    /// <summary>
    /// Gets error statistics
    /// </summary>
    [HttpGet("errors")]
    public ActionResult<ErrorStatistics> GetErrorStatistics()
    {
        try
        {
            var statistics = _errorLoggingService.GetErrorStatistics();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve error statistics");
            return StatusCode(500, "Failed to retrieve error statistics");
        }
    }

    /// <summary>
    /// Resets all metrics
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetMetrics()
    {
        try
        {
            _metricsService.Reset();
            _logger.LogInformation("Metrics reset requested");
            return Ok(new { message = "Metrics reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset metrics");
            return StatusCode(500, "Failed to reset metrics");
        }
    }
}
