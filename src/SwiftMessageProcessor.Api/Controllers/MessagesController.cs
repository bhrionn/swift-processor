using Microsoft.AspNetCore.Mvc;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace SwiftMessageProcessor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageRepository messageRepository, ILogger<MessagesController> logger)
    {
        _messageRepository = messageRepository;
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages([FromQuery] MessageFilterDto filter)
    {
        try
        {
            _logger.LogInformation("Retrieving messages with filter: Skip={Skip}, Take={Take}, Status={Status}", 
                filter.Skip, filter.Take, filter.Status);

            var messages = await _messageRepository.GetByFilterAsync(filter.ToFilter());
            var totalCount = await _messageRepository.GetMessageCountAsync(filter.ToFilter());

            var result = new PagedResult<MessageDto>
            {
                Items = messages.Select(MessageDto.FromMessage),
                TotalCount = totalCount,
                Skip = filter.Skip,
                Take = filter.Take
            };

            _logger.LogInformation("Retrieved {Count} messages out of {Total} total", 
                result.Items.Count(), result.TotalCount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages");
            return StatusCode(500, new { error = "Failed to retrieve messages", details = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a specific message by ID
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <returns>Message details with complete parsed data</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MessageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MessageDetailDto>> GetMessageById(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving message with ID: {MessageId}", id);

            var message = await _messageRepository.GetByIdAsync(id);

            if (message == null)
            {
                _logger.LogWarning("Message not found: {MessageId}", id);
                return NotFound(new { error = "Message not found", messageId = id });
            }

            var result = MessageDetailDto.FromMessage(message);

            _logger.LogInformation("Retrieved message {MessageId} with status {Status}", id, message.Status);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message {MessageId}", id);
            return StatusCode(500, new { error = "Failed to retrieve message", details = ex.Message });
        }
    }

    /// <summary>
    /// Searches for messages by transaction reference
    /// </summary>
    /// <param name="reference">Transaction reference to search for</param>
    /// <returns>List of matching messages</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<MessageDto>>> SearchMessages([FromQuery, Required] string reference)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return BadRequest(new { error = "Reference parameter is required" });
            }

            _logger.LogInformation("Searching messages by reference: {Reference}", reference);

            var startTime = DateTime.UtcNow;

            // Get all messages and filter by reference in parsed data
            var filter = new MessageFilter { Skip = 0, Take = 1000 };
            var messages = await _messageRepository.GetByFilterAsync(filter);

            var matchingMessages = messages
                .Where(m => m.ParsedMessage != null && 
                           m.ParsedMessage is MT103Message mt103 && 
                           mt103.TransactionReference != null &&
                           mt103.TransactionReference.Contains(reference, StringComparison.OrdinalIgnoreCase))
                .Select(MessageDto.FromMessage)
                .ToList();

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogInformation("Search completed in {Elapsed}s, found {Count} matching messages", 
                elapsed, matchingMessages.Count);

            return Ok(matchingMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching messages by reference: {Reference}", reference);
            return StatusCode(500, new { error = "Failed to search messages", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets message statistics
    /// </summary>
    /// <returns>Message statistics by status</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(MessageStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MessageStatisticsDto>> GetStatistics()
    {
        try
        {
            _logger.LogInformation("Retrieving message statistics");

            var allMessages = await _messageRepository.GetByFilterAsync(new MessageFilter { Skip = 0, Take = int.MaxValue });

            var statistics = new MessageStatisticsDto
            {
                TotalMessages = allMessages.Count(),
                ProcessedCount = allMessages.Count(m => m.Status == MessageStatus.Processed),
                PendingCount = allMessages.Count(m => m.Status == MessageStatus.Pending),
                FailedCount = allMessages.Count(m => m.Status == MessageStatus.Failed),
                DeadLetterCount = allMessages.Count(m => m.Status == MessageStatus.DeadLetter),
                LastProcessedAt = allMessages.Any() ? allMessages.Max(m => m.ProcessedAt) : DateTime.MinValue
            };

            _logger.LogInformation("Statistics: Total={Total}, Processed={Processed}, Failed={Failed}", 
                statistics.TotalMessages, statistics.ProcessedCount, statistics.FailedCount);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics", details = ex.Message });
        }
    }
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
    public string? TransactionReference { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    public static MessageDto FromMessage(ProcessedMessage message)
    {
        var dto = new MessageDto
        {
            Id = message.Id,
            MessageType = message.MessageType,
            Status = message.Status,
            ProcessedAt = message.ProcessedAt,
            ErrorDetails = message.ErrorDetails
        };

        // Extract key fields from parsed message if available
        if (message.ParsedMessage is MT103Message mt103)
        {
            dto.TransactionReference = mt103.TransactionReference;
            dto.Amount = mt103.Amount;
            dto.Currency = mt103.Currency;
        }

        return dto;
    }
}

public class MessageDetailDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string RawMessage { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorDetails { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public MT103MessageDto? ParsedData { get; set; }

    public static MessageDetailDto FromMessage(ProcessedMessage message)
    {
        var dto = new MessageDetailDto
        {
            Id = message.Id,
            MessageType = message.MessageType,
            RawMessage = message.RawMessage,
            Status = message.Status,
            ProcessedAt = message.ProcessedAt,
            ErrorDetails = message.ErrorDetails,
            Metadata = message.Metadata
        };

        if (message.ParsedMessage is MT103Message mt103)
        {
            dto.ParsedData = MT103MessageDto.FromMT103Message(mt103);
        }

        return dto;
    }
}

public class MT103MessageDto
{
    public string? TransactionReference { get; set; }
    public string? BankOperationCode { get; set; }
    public DateTime ValueDate { get; set; }
    public string? Currency { get; set; }
    public decimal Amount { get; set; }
    public string? OrderingCustomer { get; set; }
    public string? BeneficiaryCustomer { get; set; }
    public string? OriginalCurrency { get; set; }
    public decimal? OriginalAmount { get; set; }
    public string? OrderingInstitution { get; set; }
    public string? SendersCorrespondent { get; set; }
    public string? IntermediaryInstitution { get; set; }
    public string? AccountWithInstitution { get; set; }
    public string? RemittanceInformation { get; set; }
    public string? ChargeBearer { get; set; }
    public string? SenderToReceiverInfo { get; set; }

    public static MT103MessageDto FromMT103Message(MT103Message message)
    {
        return new MT103MessageDto
        {
            TransactionReference = message.TransactionReference,
            BankOperationCode = message.BankOperationCode,
            ValueDate = message.ValueDate,
            Currency = message.Currency,
            Amount = message.Amount,
            OrderingCustomer = message.OrderingCustomer?.ToString(),
            BeneficiaryCustomer = message.BeneficiaryCustomer?.ToString(),
            OriginalCurrency = message.OriginalCurrency,
            OriginalAmount = message.OriginalAmount,
            OrderingInstitution = message.OrderingInstitution,
            SendersCorrespondent = message.SendersCorrespondent,
            IntermediaryInstitution = message.IntermediaryInstitution,
            AccountWithInstitution = message.AccountWithInstitution,
            RemittanceInformation = message.RemittanceInformation,
            ChargeBearer = message.ChargeDetails?.ChargeBearer.ToString(),
            SenderToReceiverInfo = message.SenderToReceiverInfo
        };
    }
}

public class MessageStatisticsDto
{
    public int TotalMessages { get; set; }
    public int ProcessedCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadLetterCount { get; set; }
    public DateTime LastProcessedAt { get; set; }
}

public class MessageFilterDto
{
    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;

    [Range(1, 100)]
    public int Take { get; set; } = 20;

    public MessageStatus? Status { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? FromDate { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? ToDate { get; set; }

    public string? MessageType { get; set; }

    public MessageFilter ToFilter() => new()
    {
        Skip = Skip,
        Take = Take,
        Status = Status,
        FromDate = FromDate,
        ToDate = ToDate,
        MessageType = MessageType
    };
}