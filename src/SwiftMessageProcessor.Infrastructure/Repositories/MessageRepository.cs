using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Data;
using SwiftMessageProcessor.Infrastructure.Entities;
using SwiftMessageProcessor.Infrastructure.Mappers;

namespace SwiftMessageProcessor.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<MessageRepository> _logger;
    private readonly IAsyncPolicy _retryPolicy;
    
    public MessageRepository(SwiftMessageContext context, ILogger<MessageRepository> logger)
    {
        _context = context;
        _logger = logger;
        
        // Configure retry policy for database operations
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Database operation retry {RetryCount} after {Delay}ms due to: {Exception}",
                        retryCount, timespan.TotalMilliseconds, outcome?.Message);
                });
    }
    
    public async Task<Guid> SaveMessageAsync(ProcessedMessage message)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Saving message {MessageId} of type {MessageType}", message.Id, message.MessageType);
            
            var entity = MessageMapper.ToEntity(message);
            
            // Check if message already exists
            var existingEntity = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == message.Id);
                
            if (existingEntity != null)
            {
                // Update existing message
                existingEntity.Status = entity.Status;
                existingEntity.ProcessedAt = entity.ProcessedAt;
                existingEntity.ErrorDetails = entity.ErrorDetails;
                existingEntity.ParsedData = entity.ParsedData;
                existingEntity.Metadata = entity.Metadata;
                existingEntity.UpdatedAt = DateTime.UtcNow;
                
                _context.Messages.Update(existingEntity);
            }
            else
            {
                // Add new message
                await _context.Messages.AddAsync(entity);
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Successfully saved message {MessageId}", message.Id);
            return message.Id;
        });
    }
    
    public async Task<ProcessedMessage?> GetByIdAsync(Guid id)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Retrieving message {MessageId}", id);
            
            var entity = await _context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (entity == null)
            {
                _logger.LogDebug("Message {MessageId} not found", id);
                return null;
            }
            
            var message = MessageMapper.ToDomain(entity);
            _logger.LogDebug("Successfully retrieved message {MessageId}", id);
            return message;
        });
    }
    
    public async Task<IEnumerable<ProcessedMessage>> GetByFilterAsync(MessageFilter filter)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Retrieving messages with filter: Skip={Skip}, Take={Take}, Status={Status}, MessageType={MessageType}",
                filter.Skip, filter.Take, filter.Status, filter.MessageType);
            
            var query = _context.Messages.AsNoTracking();
            
            // Apply filters
            if (filter.Status.HasValue)
                query = query.Where(m => m.Status == filter.Status.Value);
                
            if (!string.IsNullOrEmpty(filter.MessageType))
                query = query.Where(m => m.MessageType == filter.MessageType);
                
            if (filter.FromDate.HasValue)
                query = query.Where(m => m.ProcessedAt >= filter.FromDate.Value);
                
            if (filter.ToDate.HasValue)
                query = query.Where(m => m.ProcessedAt <= filter.ToDate.Value);
            
            // Apply pagination and ordering
            var entities = await query
                .OrderByDescending(m => m.ProcessedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync();
            
            var messages = entities.Select(MessageMapper.ToDomain).ToList();
            
            _logger.LogDebug("Retrieved {Count} messages", messages.Count);
            return messages;
        });
    }
    
    public async Task UpdateStatusAsync(Guid id, MessageStatus status)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Updating status for message {MessageId} to {Status}", id, status);
            
            var entity = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null)
            {
                _logger.LogWarning("Cannot update status: Message {MessageId} not found", id);
                throw new InvalidOperationException($"Message with ID {id} not found");
            }
            
            entity.Status = status;
            entity.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Successfully updated status for message {MessageId}", id);
        });
    }
    
    public async Task<int> GetMessageCountAsync(MessageFilter filter)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Getting message count with filter: Status={Status}, MessageType={MessageType}",
                filter.Status, filter.MessageType);
            
            var query = _context.Messages.AsNoTracking();
            
            // Apply filters (same as GetByFilterAsync but without pagination)
            if (filter.Status.HasValue)
                query = query.Where(m => m.Status == filter.Status.Value);
                
            if (!string.IsNullOrEmpty(filter.MessageType))
                query = query.Where(m => m.MessageType == filter.MessageType);
                
            if (filter.FromDate.HasValue)
                query = query.Where(m => m.ProcessedAt >= filter.FromDate.Value);
                
            if (filter.ToDate.HasValue)
                query = query.Where(m => m.ProcessedAt <= filter.ToDate.Value);
            
            var count = await query.CountAsync();
            
            _logger.LogDebug("Message count: {Count}", count);
            return count;
        });
    }
    
    private static bool IsTransientException(Exception ex)
    {
        // Define which exceptions should trigger a retry
        return ex switch
        {
            DbUpdateException => true,
            InvalidOperationException when ex.Message.Contains("timeout") => true,
            InvalidOperationException when ex.Message.Contains("connection") => true,
            TimeoutException => true,
            _ => false
        };
    }
}