using System.ComponentModel.DataAnnotations;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Interfaces;

public interface ISwiftMessageParser<T> where T : SwiftMessage
{
    Task<T> ParseAsync(string rawMessage);
    Task<ValidationResult> ValidateAsync(T message);
    bool CanParse(string messageType);
}