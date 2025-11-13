using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Parsers;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Application.Services;

/// <summary>
/// Service responsible for processing SWIFT messages from queue to database
/// </summary>
public class MessageProcessingService : IMessageProcessingService
{
    private readonly ILogger<MessageProcessingService> _logger;
    private readonly IQueueService _queueService;
    private readonly IMessageRepository _messageRepository;
    private readonly ISwiftMessageParser<MT103Message> _mt103Parser;
    private readonly QueueOptions _queueOptions;
    private readonly ProcessingOptions _processingOptions;
    private readonly SystemStatus _systemStatus = new();
    private readonly ProcessingMetrics _metrics = new() { MetricsStartTime = DateTime.UtcNow };
    private readonly List<double> _processingTimes = new();
    private readonly object _metricsLock = new();
    private bool _isProcessing;

    public MessageProcessingService(
        ILogger<MessageProcessingService> logger,
        IQueueService queueService,
        IMessageRepository messageRepository,
        ISwiftMessageParser<MT103Message> mt103Parser,
        IOptions<QueueOptions> queueOptions,
        IOptions<ProcessingOptions> processingOptions)
    {
        _logger = logger;
        _queueService = queueService;
        _messageRepository = messageRepository;
        _mt103Parser = mt103Parser;
        _queueOptions = queueOptions.Value;
        _processingOptions = processingOptions.Value;
    }

    public async Task<ProcessingResult> ProcessMessageAsync(string rawMessage)
    {
        var messageId = Guid.NewGuid();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["Operation"] = "ProcessMessage"
        });

        _logger.LogInformation("Starting message processing for message length {MessageLength}", rawMessage.Length);

        var startTime = DateTime.UtcNow;

        try
        {
            // Step 1: Parse the message
            MT103Message parsedMessage;
            try
            {
                parsedMessage = await ParseMessageWithRetryAsync(rawMessage);
                _logger.LogInformation("Message parsed successfully as {MessageType}", parsedMessage.MessageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse message after retries");
                await HandleFailedMessageAsync(rawMessage, "Parsing failed", ex);
                
                _systemStatus.MessagesFailed++;
                UpdateMetrics((DateTime.UtcNow - startTime).TotalMilliseconds, false, "ParsingError");
                
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = $"Parsing failed: {ex.Message}",
                    Exception = ex
                };
            }

            // Step 2: Validate the parsed message
            ValidationResult validationResult;
            try
            {
                validationResult = await _mt103Parser.ValidateAsync(parsedMessage);
                if (validationResult != ValidationResult.Success)
                {
                    _logger.LogWarning("Message validation failed: {ValidationError}", validationResult.ErrorMessage);
                    await HandleFailedMessageAsync(rawMessage, $"Validation failed: {validationResult.ErrorMessage}", null);
                    
                    _systemStatus.MessagesFailed++;
                    UpdateMetrics((DateTime.UtcNow - startTime).TotalMilliseconds, false, "ValidationError");
                    
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = $"Validation failed: {validationResult.ErrorMessage}"
                    };
                }
                
                _logger.LogInformation("Message validated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message validation");
                await HandleFailedMessageAsync(rawMessage, "Validation error", ex);
                
                _systemStatus.MessagesFailed++;
                UpdateMetrics((DateTime.UtcNow - startTime).TotalMilliseconds, false, "ValidationException");
                
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = $"Validation error: {ex.Message}",
                    Exception = ex
                };
            }

            // Step 3: Store in database
            ProcessedMessage processedMessage;
            try
            {
                processedMessage = new ProcessedMessage
                {
                    Id = messageId,
                    MessageType = "MT103",
                    RawMessage = rawMessage,
                    ParsedMessage = parsedMessage,
                    Status = MessageStatus.Processed,
                    ProcessedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ProcessingDurationMs"] = (DateTime.UtcNow - startTime).TotalMilliseconds,
                        ["TransactionReference"] = parsedMessage.TransactionReference,
                        ["Amount"] = parsedMessage.Amount,
                        ["Currency"] = parsedMessage.Currency
                    }
                };

                await SaveMessageWithRetryAsync(processedMessage);
                _logger.LogInformation("Message saved to database with ID {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save message to database after retries");
                await HandleFailedMessageAsync(rawMessage, "Database save failed", ex);
                
                _systemStatus.MessagesFailed++;
                UpdateMetrics((DateTime.UtcNow - startTime).TotalMilliseconds, false, "DatabaseError");
                
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = $"Database save failed: {ex.Message}",
                    Exception = ex
                };
            }

            // Step 4: Move to completed queue
            try
            {
                await SendToCompletedQueueWithRetryAsync(rawMessage);
                _logger.LogInformation("Message moved to completed queue");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move message to completed queue, but message was saved to database");
                // Don't fail the entire operation if queue operation fails
            }

            // Update statistics and metrics
            var processingDuration = DateTime.UtcNow - startTime;
            UpdateMetrics(processingDuration.TotalMilliseconds, true);
            
            _systemStatus.MessagesProcessed++;
            _systemStatus.LastProcessedAt = DateTime.UtcNow;

            _logger.LogInformation("Message processing completed successfully in {Duration}ms", processingDuration.TotalMilliseconds);

            return new ProcessingResult
            {
                Success = true,
                Message = processedMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during message processing");
            _systemStatus.MessagesFailed++;
            UpdateMetrics((DateTime.UtcNow - startTime).TotalMilliseconds, false, "UnexpectedError");
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Exception = ex
            };
        }
    }

    public Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        _isProcessing = true;
        _systemStatus.IsProcessing = true;
        _systemStatus.Status = "Processing";
        _logger.LogInformation("Message processing service started");
        return Task.CompletedTask;
    }

    public Task StopProcessingAsync()
    {
        _isProcessing = false;
        _systemStatus.IsProcessing = false;
        _systemStatus.Status = "Stopped";
        _logger.LogInformation("Message processing service stopped");
        return Task.CompletedTask;
    }

    public Task<SystemStatus> GetSystemStatusAsync()
    {
        return Task.FromResult(_systemStatus);
    }

    public Task<ProcessingMetrics> GetMetricsAsync()
    {
        lock (_metricsLock)
        {
            _metrics.LastUpdated = DateTime.UtcNow;
            return Task.FromResult(_metrics);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Updates processing metrics
    /// </summary>
    private void UpdateMetrics(double processingTimeMs, bool success, string? errorType = null)
    {
        lock (_metricsLock)
        {
            if (success)
            {
                _metrics.TotalMessagesProcessed++;
                _processingTimes.Add(processingTimeMs);
                
                // Keep only last 100 processing times for average calculation
                if (_processingTimes.Count > 100)
                {
                    _processingTimes.RemoveAt(0);
                }
                
                _metrics.AverageProcessingTimeMs = _processingTimes.Average();
            }
            else
            {
                _metrics.TotalMessagesFailed++;
                
                if (!string.IsNullOrEmpty(errorType))
                {
                    if (_metrics.ErrorsByType.ContainsKey(errorType))
                    {
                        _metrics.ErrorsByType[errorType]++;
                    }
                    else
                    {
                        _metrics.ErrorsByType[errorType] = 1;
                    }
                }
            }
            
            // Calculate messages per minute
            var elapsedMinutes = (DateTime.UtcNow - _metrics.MetricsStartTime).TotalMinutes;
            if (elapsedMinutes > 0)
            {
                _metrics.MessagesPerMinute = _metrics.TotalMessagesProcessed / elapsedMinutes;
            }
            
            _metrics.LastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Parses a message with retry logic
    /// </summary>
    private async Task<MT103Message> ParseMessageWithRetryAsync(string rawMessage)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= _processingOptions.RetryAttempts; attempt++)
        {
            try
            {
                return await _mt103Parser.ParseAsync(rawMessage);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Parse attempt {Attempt} of {MaxAttempts} failed", 
                    attempt, _processingOptions.RetryAttempts);
                
                if (attempt < _processingOptions.RetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_processingOptions.RetryDelaySeconds));
                }
            }
        }
        
        throw lastException ?? new Exception("Failed to parse message");
    }

    /// <summary>
    /// Saves a message to the database with retry logic
    /// </summary>
    private async Task SaveMessageWithRetryAsync(ProcessedMessage message)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= _processingOptions.RetryAttempts; attempt++)
        {
            try
            {
                await _messageRepository.SaveMessageAsync(message);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Database save attempt {Attempt} of {MaxAttempts} failed", 
                    attempt, _processingOptions.RetryAttempts);
                
                if (attempt < _processingOptions.RetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_processingOptions.RetryDelaySeconds));
                }
            }
        }
        
        throw lastException ?? new Exception("Failed to save message to database");
    }

    /// <summary>
    /// Sends a message to the completed queue with retry logic
    /// </summary>
    private async Task SendToCompletedQueueWithRetryAsync(string message)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= _processingOptions.RetryAttempts; attempt++)
        {
            try
            {
                await _queueService.SendMessageAsync(_queueOptions.Settings.CompletedQueue, message);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Completed queue send attempt {Attempt} of {MaxAttempts} failed", 
                    attempt, _processingOptions.RetryAttempts);
                
                if (attempt < _processingOptions.RetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_processingOptions.RetryDelaySeconds));
                }
            }
        }
        
        throw lastException ?? new Exception("Failed to send message to completed queue");
    }

    /// <summary>
    /// Handles a failed message by moving it to the dead letter queue
    /// </summary>
    private async Task HandleFailedMessageAsync(string rawMessage, string errorReason, Exception? exception)
    {
        try
        {
            _logger.LogWarning("Moving failed message to dead letter queue. Reason: {Reason}", errorReason);
            
            // Create error metadata
            var errorMetadata = new
            {
                ErrorReason = errorReason,
                ErrorMessage = exception?.Message,
                ErrorStackTrace = exception?.StackTrace,
                FailedAt = DateTime.UtcNow,
                OriginalMessage = rawMessage
            };

            var errorJson = System.Text.Json.JsonSerializer.Serialize(errorMetadata);
            
            await _queueService.SendMessageAsync(_queueOptions.Settings.DeadLetterQueue, errorJson);
            _logger.LogInformation("Failed message moved to dead letter queue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move message to dead letter queue");
        }
    }

    #endregion
}