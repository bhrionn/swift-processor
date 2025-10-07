using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MessagesController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageProcessingService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves messages based on filter criteria
    /// </summary>
    /// <param name="filter">Filter criteria for message retrieval</param>
    /// <returns>Paginated list of messages</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages([FromQuery] MessageFilterDto filter)
    {
        // Implementation will be added in later tasks
        var result = new PagedResult<MessageDto>
        {
            Items = new List<MessageDto>(),
            TotalCount = 0
        };
        
        return Ok(result);
    }
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
}

public class MessageFilterDto
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
    public MessageStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    
    public MessageFilter ToFilter() => new()
    {
        Skip = Skip,
        Take = Take,
        Status = Status,
        FromDate = FromDate,
        ToDate = ToDate
    };
}