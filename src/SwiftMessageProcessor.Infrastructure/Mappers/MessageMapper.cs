using System.Text.Json;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Entities;

namespace SwiftMessageProcessor.Infrastructure.Mappers;

public static class MessageMapper
{
    public static ProcessedMessageEntity ToEntity(ProcessedMessage message)
    {
        return new ProcessedMessageEntity
        {
            Id = message.Id,
            MessageType = message.MessageType,
            RawMessage = message.RawMessage,
            ParsedData = message.ParsedMessage != null ? JsonSerializer.Serialize(message.ParsedMessage) : null,
            Status = message.Status,
            ProcessedAt = message.ProcessedAt,
            ErrorDetails = message.ErrorDetails,
            Metadata = message.Metadata.Count > 0 ? JsonSerializer.Serialize(message.Metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    public static ProcessedMessage ToDomain(ProcessedMessageEntity entity)
    {
        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            try
            {
                var deserializedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata);
                if (deserializedMetadata != null)
                {
                    metadata = deserializedMetadata;
                }
            }
            catch (JsonException)
            {
                // If deserialization fails, leave metadata empty
            }
        }
        
        SwiftMessage? parsedMessage = null;
        if (!string.IsNullOrEmpty(entity.ParsedData))
        {
            try
            {
                // For now, we'll deserialize as a generic SwiftMessage
                // In the future, this could be enhanced to deserialize to specific message types
                parsedMessage = JsonSerializer.Deserialize<SwiftMessage>(entity.ParsedData);
            }
            catch (JsonException)
            {
                // If deserialization fails, leave parsed message null
            }
        }
        
        return new ProcessedMessage
        {
            Id = entity.Id,
            MessageType = entity.MessageType,
            RawMessage = entity.RawMessage,
            ParsedMessage = parsedMessage,
            Status = entity.Status,
            ProcessedAt = entity.ProcessedAt,
            ErrorDetails = entity.ErrorDetails,
            Metadata = metadata
        };
    }
}