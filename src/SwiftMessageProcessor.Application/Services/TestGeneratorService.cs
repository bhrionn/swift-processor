using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Configuration;

namespace SwiftMessageProcessor.Application.Services;

/// <summary>
/// Service for generating test MT103 messages for development and testing purposes
/// </summary>
public class TestGeneratorService : ITestGeneratorService
{
    private readonly IQueueService _queueService;
    private readonly ILogger<TestGeneratorService> _logger;
    private readonly TestModeOptions _testModeOptions;
    private readonly QueueOptions _queueOptions;
    private readonly Random _random = new();
    private CancellationTokenSource? _generationCts;
    private Task? _generationTask;
    private bool _isGenerating;

    private static readonly string[] Currencies = { "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "SGD" };
    private static readonly string[] BankCodes = { "CRED", "CRTS", "SPAY", "SPRI", "SSTD" };
    private static readonly string[] ChargeBearers = { "BEN", "OUR", "SHA" };
    
    private static readonly string[] BankNames = 
    {
        "CHASUS33", "DEUTDEFF", "HSBCHKHH", "BNPAFRPP", "CITIUS33",
        "BARCGB22", "UBSWCHZH", "RBOSGB2L", "NATAAU33", "SCBLSGSG"
    };
    
    private static readonly string[] CustomerNames = 
    {
        "ACME CORPORATION", "GLOBAL TRADING LTD", "TECH INNOVATIONS INC",
        "INTERNATIONAL EXPORTS", "FINANCIAL SERVICES GROUP", "MANUFACTURING CO",
        "RETAIL SOLUTIONS", "LOGISTICS PARTNERS", "ENERGY SYSTEMS", "HEALTHCARE PROVIDERS"
    };
    
    private static readonly string[] Addresses = 
    {
        "123 MAIN STREET\nNEW YORK NY 10001\nUSA",
        "45 OXFORD STREET\nLONDON W1D 1BS\nUNITED KINGDOM",
        "78 RUE DE RIVOLI\nPARIS 75001\nFRANCE",
        "12 BAHNHOFSTRASSE\nZURICH 8001\nSWITZERLAND",
        "56 ORCHARD ROAD\nSINGAPORE 238883\nSINGAPORE"
    };
    
    private static readonly string[] RemittanceInfo = 
    {
        "INVOICE 2024-001 PAYMENT",
        "CONTRACT SETTLEMENT Q4 2024",
        "MONTHLY SERVICE FEES",
        "GOODS SHIPMENT REF 12345",
        "CONSULTING SERVICES RENDERED"
    };

    public bool IsGenerating => _isGenerating;

    public TestGeneratorService(
        IQueueService queueService,
        ILogger<TestGeneratorService> logger,
        IOptions<TestModeOptions> testModeOptions,
        IOptions<QueueOptions> queueOptions)
    {
        _queueService = queueService;
        _logger = logger;
        _testModeOptions = testModeOptions.Value;
        _queueOptions = queueOptions.Value;
    }

    /// <summary>
    /// Generates a valid MT103 message with random but compliant data
    /// </summary>
    public async Task<MT103Message> GenerateValidMessageAsync()
    {
        await Task.CompletedTask; // Placeholder for async operations
        
        var message = new MT103Message
        {
            TransactionReference = GenerateTransactionReference(),
            BankOperationCode = BankCodes[_random.Next(BankCodes.Length)],
            ValueDate = DateTime.Today.AddDays(_random.Next(-30, 30)),
            Currency = Currencies[_random.Next(Currencies.Length)],
            Amount = GenerateRandomAmount(),
            OrderingCustomer = GenerateOrderingCustomer(),
            BeneficiaryCustomer = GenerateBeneficiaryCustomer(),
            RemittanceInformation = RemittanceInfo[_random.Next(RemittanceInfo.Length)],
            ChargeDetails = new ChargeDetails 
            { 
                ChargeBearer = Enum.Parse<ChargeBearer>(ChargeBearers[_random.Next(ChargeBearers.Length)])
            }
        };
        
        // Randomly add optional fields
        if (_random.Next(100) < 30) // 30% chance
        {
            message.OrderingInstitution = BankNames[_random.Next(BankNames.Length)];
        }
        
        if (_random.Next(100) < 20) // 20% chance
        {
            message.SendersCorrespondent = BankNames[_random.Next(BankNames.Length)];
        }
        
        if (_random.Next(100) < 20) // 20% chance
        {
            message.IntermediaryInstitution = BankNames[_random.Next(BankNames.Length)];
        }
        
        if (_random.Next(100) < 40) // 40% chance
        {
            message.AccountWithInstitution = BankNames[_random.Next(BankNames.Length)];
        }
        
        return message;
    }

    /// <summary>
    /// Generates an invalid MT103 message with a specific validation error
    /// </summary>
    public async Task<MT103Message> GenerateInvalidMessageAsync(ValidationError errorType)
    {
        await Task.CompletedTask;
        
        var message = await GenerateValidMessageAsync();
        
        // Introduce specific validation errors
        switch (errorType)
        {
            case ValidationError.MissingTransactionReference:
                message.TransactionReference = string.Empty;
                break;
                
            case ValidationError.InvalidAmount:
                message.Amount = -100.00m;
                break;
                
            case ValidationError.MissingCurrency:
                message.Currency = string.Empty;
                break;
                
            case ValidationError.InvalidBankCode:
                message.BankOperationCode = "XXXX";
                break;
                
            case ValidationError.MissingBeneficiary:
                message.BeneficiaryCustomer = new BeneficiaryCustomer();
                break;
        }
        
        return message;
    }

    /// <summary>
    /// Generates a raw SWIFT message string from an MT103Message object
    /// </summary>
    public async Task<string> GenerateRawMessageAsync(MT103Message message)
    {
        await Task.CompletedTask;
        
        var senderBic = BankNames[_random.Next(BankNames.Length)];
        var receiverBic = BankNames[_random.Next(BankNames.Length)];
        var sessionNumber = _random.Next(1000, 9999);
        var sequenceNumber = _random.Next(100000, 999999);
        
        var rawMessage = $@"{{1:F01{senderBic}{sessionNumber}{sequenceNumber:D6}}}
{{2:I103{receiverBic}N}}
{{4:
:20:{message.TransactionReference}
:23B:{message.BankOperationCode}
:32A:{message.ValueDate:yyMMdd}{message.Currency}{message.Amount:F2}";

        // Add optional field 33B if present
        if (!string.IsNullOrEmpty(message.OriginalCurrency) && message.OriginalAmount.HasValue)
        {
            rawMessage += $"\n:33B:{message.OriginalCurrency}{message.OriginalAmount:F2}";
        }
        
        // Add ordering customer (Field 50K)
        rawMessage += $"\n:50K:";
        if (!string.IsNullOrEmpty(message.OrderingCustomer.Account))
        {
            rawMessage += $"/{message.OrderingCustomer.Account}\n";
        }
        rawMessage += $"{message.OrderingCustomer.Name}";
        if (!string.IsNullOrEmpty(message.OrderingCustomer.Address))
        {
            rawMessage += $"\n{message.OrderingCustomer.Address}";
        }
        
        // Add optional field 52A if present
        if (!string.IsNullOrEmpty(message.OrderingInstitution))
        {
            rawMessage += $"\n:52A:{message.OrderingInstitution}";
        }
        
        // Add optional field 53A if present
        if (!string.IsNullOrEmpty(message.SendersCorrespondent))
        {
            rawMessage += $"\n:53A:{message.SendersCorrespondent}";
        }
        
        // Add optional field 56A if present
        if (!string.IsNullOrEmpty(message.IntermediaryInstitution))
        {
            rawMessage += $"\n:56A:{message.IntermediaryInstitution}";
        }
        
        // Add optional field 57A if present
        if (!string.IsNullOrEmpty(message.AccountWithInstitution))
        {
            rawMessage += $"\n:57A:{message.AccountWithInstitution}";
        }
        
        // Add beneficiary customer (Field 59)
        rawMessage += $"\n:59:";
        if (!string.IsNullOrEmpty(message.BeneficiaryCustomer.Account))
        {
            rawMessage += $"/{message.BeneficiaryCustomer.Account}\n";
        }
        rawMessage += $"{message.BeneficiaryCustomer.Name}";
        if (!string.IsNullOrEmpty(message.BeneficiaryCustomer.Address))
        {
            rawMessage += $"\n{message.BeneficiaryCustomer.Address}";
        }
        
        // Add optional field 70 if present
        if (!string.IsNullOrEmpty(message.RemittanceInformation))
        {
            rawMessage += $"\n:70:{message.RemittanceInformation}";
        }
        
        // Add charge details (Field 71A)
        if (message.ChargeDetails != null)
        {
            rawMessage += $"\n:71A:{message.ChargeDetails.ChargeBearer}";
        }
        
        // Add optional field 72 if present
        if (!string.IsNullOrEmpty(message.SenderToReceiverInfo))
        {
            rawMessage += $"\n:72:{message.SenderToReceiverInfo}";
        }
        
        rawMessage += "\n-}";
        
        return rawMessage;
    }

    /// <summary>
    /// Generates a batch of MT103 messages
    /// </summary>
    public async Task<IEnumerable<MT103Message>> GenerateBatchAsync(int count)
    {
        var messages = new List<MT103Message>();
        
        for (int i = 0; i < count; i++)
        {
            // Determine if this should be a valid or invalid message based on configuration
            var shouldBeValid = _random.Next(100) < _testModeOptions.ValidMessagePercentage;
            
            MT103Message message;
            if (shouldBeValid)
            {
                message = await GenerateValidMessageAsync();
            }
            else
            {
                // Generate invalid message with random error type
                var errorTypes = Enum.GetValues<ValidationError>();
                var randomError = errorTypes[_random.Next(errorTypes.Length)];
                message = await GenerateInvalidMessageAsync(randomError);
            }
            
            messages.Add(message);
        }
        
        return messages;
    }

    /// <summary>
    /// Starts periodic message generation and sends to input queue
    /// </summary>
    public async Task StartGenerationAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (_isGenerating)
        {
            _logger.LogWarning("Test message generation is already running");
            return;
        }
        
        _logger.LogInformation("Starting test message generation with interval: {Interval}", interval);
        _isGenerating = true;
        _generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _generationTask = Task.Run(async () =>
        {
            await GenerationLoopAsync(interval, _generationCts.Token);
        }, _generationCts.Token);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops periodic message generation
    /// </summary>
    public async Task StopGenerationAsync()
    {
        if (!_isGenerating)
        {
            _logger.LogWarning("Test message generation is not running");
            return;
        }
        
        _logger.LogInformation("Stopping test message generation");
        _isGenerating = false;
        
        _generationCts?.Cancel();
        
        if (_generationTask != null)
        {
            try
            {
                await _generationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }
        
        _generationCts?.Dispose();
        _generationCts = null;
        _generationTask = null;
        
        _logger.LogInformation("Test message generation stopped");
    }

    /// <summary>
    /// Main generation loop that periodically creates and sends test messages
    /// </summary>
    private async Task GenerationLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Test message generation loop started");
        
        while (!cancellationToken.IsCancellationRequested && _isGenerating)
        {
            try
            {
                // Generate batch of messages
                var messages = await GenerateBatchAsync(_testModeOptions.BatchSize);
                
                foreach (var message in messages)
                {
                    try
                    {
                        // Convert to raw SWIFT format
                        var rawMessage = await GenerateRawMessageAsync(message);
                        
                        // Send to input queue
                        await _queueService.SendMessageAsync(_queueOptions.Settings.InputQueue, rawMessage);
                        
                        _logger.LogDebug("Generated and sent test message: {Reference}", message.TransactionReference);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate or send test message");
                    }
                }
                
                _logger.LogInformation("Generated and sent {Count} test message(s)", messages.Count());
                
                // Wait for next generation interval
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Test message generation loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test message generation loop");
                // Wait before retrying to avoid tight error loop
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        
        _logger.LogInformation("Test message generation loop stopped");
    }

    #region Helper Methods

    private string GenerateTransactionReference()
    {
        // Transaction reference must be max 16 characters
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmm");
        var random = _random.Next(100, 999);
        return $"REF{timestamp}{random}"; // Format: REFyyMMddHHmm### = 16 chars
    }

    private decimal GenerateRandomAmount()
    {
        // Generate amounts between 100 and 1,000,000
        var amount = _random.Next(100, 1000000) + (_random.NextDouble() * 0.99);
        return Math.Round((decimal)amount, 2);
    }

    private OrderingCustomer GenerateOrderingCustomer()
    {
        var useOptionA = _random.Next(100) < 50; // 50% chance of using option A
        
        if (useOptionA)
        {
            return new OrderingCustomer
            {
                Account = GenerateAccountNumber(),
                BIC = BankNames[_random.Next(BankNames.Length)],
                Name = CustomerNames[_random.Next(CustomerNames.Length)]
            };
        }
        else
        {
            return new OrderingCustomer
            {
                Account = GenerateAccountNumber(),
                Name = CustomerNames[_random.Next(CustomerNames.Length)],
                Address = Addresses[_random.Next(Addresses.Length)]
            };
        }
    }

    private BeneficiaryCustomer GenerateBeneficiaryCustomer()
    {
        var useOption59A = _random.Next(100) < 50; // 50% chance of using option 59A
        
        if (useOption59A)
        {
            return new BeneficiaryCustomer
            {
                Account = GenerateAccountNumber(),
                BIC = BankNames[_random.Next(BankNames.Length)],
                Name = CustomerNames[_random.Next(CustomerNames.Length)]
            };
        }
        else
        {
            return new BeneficiaryCustomer
            {
                Account = GenerateAccountNumber(),
                Name = CustomerNames[_random.Next(CustomerNames.Length)],
                Address = Addresses[_random.Next(Addresses.Length)]
            };
        }
    }

    private string GenerateAccountNumber()
    {
        var accountType = _random.Next(3);
        return accountType switch
        {
            0 => $"ACC{_random.Next(10000000, 99999999)}",
            1 => $"IBAN{_random.Next(1000000000, int.MaxValue)}",
            _ => $"{_random.Next(100000, 999999)}-{_random.Next(1000, 9999)}"
        };
    }

    #endregion
}
