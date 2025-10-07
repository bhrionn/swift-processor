using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Interfaces;

public interface ITestGeneratorService
{
    Task<MT103Message> GenerateValidMessageAsync();
    Task<MT103Message> GenerateInvalidMessageAsync(ValidationError errorType);
    Task<string> GenerateRawMessageAsync(MT103Message message);
    Task<IEnumerable<MT103Message>> GenerateBatchAsync(int count);
    Task StartGenerationAsync(TimeSpan interval, CancellationToken cancellationToken);
    Task StopGenerationAsync();
    bool IsGenerating { get; }
}

public enum ValidationError
{
    MissingTransactionReference,
    InvalidAmount,
    MissingCurrency,
    InvalidBankCode,
    MissingBeneficiary
}