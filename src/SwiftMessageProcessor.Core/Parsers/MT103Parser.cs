using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using SwiftMessageProcessor.Core.Interfaces;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Parsers;

/// <summary>
/// Parser for SWIFT MT103 Single Customer Credit Transfer messages
/// </summary>
public class MT103Parser : ISwiftMessageParser<MT103Message>
{
    private static readonly Dictionary<string, Regex> FieldPatterns = new()
    {
        // Basic Header Block (Block 1)
        ["Block1"] = new Regex(@"^\{1:F01([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}[A-Z0-9]{3})(\d{10})(\d{4})\}$", RegexOptions.Compiled),
        
        // Application Header Block (Block 2)
        ["Block2"] = new Regex(@"^\{2:[IO]103([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}[A-Z0-9]{3})?(\d{4})?([NU])?(\d{3})?\}$", RegexOptions.Compiled),
        
        // User Header Block (Block 3) - Optional
        ["Block3"] = new Regex(@"^\{3:(\{[^}]+\})*\}$", RegexOptions.Compiled),
        
        // Text Block (Block 4) - Contains the actual message fields
        ["Block4Start"] = new Regex(@"^\{4:$", RegexOptions.Compiled),
        ["Block4End"] = new Regex(@"^-\}$", RegexOptions.Compiled),
        
        // Trailer Block (Block 5) - Optional
        ["Block5"] = new Regex(@"^\{5:(\{[^}]+\})*\}$", RegexOptions.Compiled),
        
        // Field patterns for MT103
        ["Field20"] = new Regex(@"^:20:(.{1,16})$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field23B"] = new Regex(@"^:23B:([A-Z]{4})$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field32A"] = new Regex(@"^:32A:(\d{6})([A-Z]{3})([\d,\.]+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field33B"] = new Regex(@"^:33B:([A-Z]{3})([\d,\.]+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field50A"] = new Regex(@"^:50A:(/[^\r\n]*)?[\r\n]?([^\r\n]+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field50K"] = new Regex(@"^:50K:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline),
        ["Field52A"] = new Regex(@"^:52A:([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field53A"] = new Regex(@"^:53A:([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field53B"] = new Regex(@"^:53B:(/[^\r\n]*)?[\r\n]?(.+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field54A"] = new Regex(@"^:54A:(/[^\r\n]*)?[\r\n]?([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field56A"] = new Regex(@"^:56A:([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field56C"] = new Regex(@"^:56C:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field56D"] = new Regex(@"^:56D:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline),
        ["Field57A"] = new Regex(@"^:57A:([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field57B"] = new Regex(@"^:57B:(/[^\r\n]*)?[\r\n]?(.+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field57C"] = new Regex(@"^:57C:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field57D"] = new Regex(@"^:57D:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline),
        ["Field59"] = new Regex(@"^:59:(/[^\r\n]*)?[\r\n]?(.+)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline),
        ["Field59A"] = new Regex(@"^:59A:(/[^\r\n]*)?[\r\n]?([A-Z]{4}[A-Z]{2}[A-Z0-9]{2}[A-Z0-9]{3})$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field70"] = new Regex(@"^:70:([^\r\n:]+(?:[\r\n][^\r\n:]+)*)(?=[\r\n]:|$)", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field71A"] = new Regex(@"^:71A:([A-Z]{3})$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field71F"] = new Regex(@"^:71F:([A-Z]{3})([\d,\.]+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field71G"] = new Regex(@"^:71G:([A-Z]{3})([\d,\.]+)$", RegexOptions.Compiled | RegexOptions.Multiline),
        ["Field72"] = new Regex(@"^:72:(.+)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline)
    };

    /// <summary>
    /// Determines if this parser can handle the given message type
    /// </summary>
    /// <param name="messageType">The message type to check</param>
    /// <returns>True if this parser can handle MT103 messages</returns>
    public bool CanParse(string messageType)
    {
        return string.Equals(messageType, "MT103", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(messageType, "103", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a raw SWIFT MT103 message into a structured MT103Message object
    /// </summary>
    /// <param name="rawMessage">The raw SWIFT message string</param>
    /// <returns>Parsed MT103Message object</returns>
    /// <exception cref="ArgumentException">Thrown when the message format is invalid</exception>
    /// <exception cref="SwiftParsingException">Thrown when parsing fails</exception>
    public async Task<MT103Message> ParseAsync(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("Raw message cannot be null or empty", nameof(rawMessage));

        try
        {
            var message = new MT103Message
            {
                RawMessage = rawMessage,
                ReceivedAt = DateTime.UtcNow,
                Status = MessageStatus.Pending
            };

            // Parse SWIFT message blocks
            var blocks = ParseMessageBlocks(rawMessage);
            
            // Validate required blocks
            ValidateRequiredBlocks(blocks);
            
            // Extract and populate fields from Block 4 (Text Block)
            await PopulateMessageFields(message, blocks["Block4"]);
            
            // Store all parsed fields in the Fields dictionary
            PopulateFieldsDictionary(message, blocks);

            return message;
        }
        catch (Exception ex) when (!(ex is SwiftParsingException || ex is ArgumentException))
        {
            throw new SwiftParsingException($"Failed to parse MT103 message: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates a parsed MT103 message
    /// </summary>
    /// <param name="message">The message to validate</param>
    /// <returns>ValidationResult indicating success or failure</returns>
    public async Task<ValidationResult> ValidateAsync(MT103Message message)
    {
        if (message == null)
            return new ValidationResult("Message cannot be null");

        // Use the message's built-in validation
        var result = message.Validate();
        
        // Additional parser-specific validations can be added here
        await Task.CompletedTask; // Placeholder for async validation if needed
        
        return result;
    }

    /// <summary>
    /// Parses the SWIFT message into its constituent blocks
    /// </summary>
    /// <param name="rawMessage">The raw SWIFT message</param>
    /// <returns>Dictionary containing parsed blocks</returns>
    private static Dictionary<string, string> ParseMessageBlocks(string rawMessage)
    {
        var blocks = new Dictionary<string, string>();
        var normalizedMessage = rawMessage.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Parse Block 1 (Basic Header)
        var block1Match = Regex.Match(normalizedMessage, @"\{1:[^}]+\}");
        if (block1Match.Success)
        {
            blocks["Block1"] = block1Match.Value;
        }

        // Parse Block 2 (Application Header)
        var block2Match = Regex.Match(normalizedMessage, @"\{2:[^}]+\}");
        if (block2Match.Success)
        {
            blocks["Block2"] = block2Match.Value;
        }

        // Parse Block 3 (User Header) - Optional
        var block3Match = Regex.Match(normalizedMessage, @"\{3:(\{[^}]+\})*\}");
        if (block3Match.Success)
        {
            blocks["Block3"] = block3Match.Value;
        }

        // Parse Block 4 (Text Block) - Contains the actual message content
        var block4Match = Regex.Match(normalizedMessage, @"\{4:\s*(.*?)\s*-\}", RegexOptions.Singleline);
        if (block4Match.Success)
        {
            blocks["Block4"] = block4Match.Groups[1].Value.Trim();
        }

        // Parse Block 5 (Trailer) - Optional
        var block5Match = Regex.Match(normalizedMessage, @"\{5:(\{[^}]+\})*\}");
        if (block5Match.Success)
        {
            blocks["Block5"] = block5Match.Value;
        }

        return blocks;
    }

    /// <summary>
    /// Validates that required blocks are present
    /// </summary>
    /// <param name="blocks">Dictionary of parsed blocks</param>
    /// <exception cref="SwiftParsingException">Thrown when required blocks are missing</exception>
    private static void ValidateRequiredBlocks(Dictionary<string, string> blocks)
    {
        if (!blocks.ContainsKey("Block1"))
            throw new SwiftParsingException("Missing required Block 1 (Basic Header)");
            
        if (!blocks.ContainsKey("Block2"))
            throw new SwiftParsingException("Missing required Block 2 (Application Header)");
            
        if (!blocks.ContainsKey("Block4"))
            throw new SwiftParsingException("Missing required Block 4 (Text Block)");

        // Validate Block 2 contains MT103 message type
        var block2 = blocks["Block2"];
        if (!block2.Contains("103"))
            throw new SwiftParsingException("Block 2 does not indicate MT103 message type");
    }

    /// <summary>
    /// Populates the MT103Message fields from the parsed text block
    /// </summary>
    /// <param name="message">The message object to populate</param>
    /// <param name="textBlock">The text block content</param>
    private static async Task PopulateMessageFields(MT103Message message, string textBlock)
    {
        await Task.Run(() =>
        {
            // Parse mandatory fields
            ParseField20(message, textBlock); // Transaction Reference
            ParseField23B(message, textBlock); // Bank Operation Code
            ParseField32A(message, textBlock); // Value Date, Currency, Amount
            ParseField50(message, textBlock); // Ordering Customer
            ParseField59(message, textBlock); // Beneficiary Customer

            // Parse optional fields
            ParseField33B(message, textBlock); // Original Currency/Amount
            ParseField52A(message, textBlock); // Ordering Institution
            ParseField53(message, textBlock); // Sender's Correspondent
            ParseField54A(message, textBlock); // Receiver's Correspondent
            ParseField56(message, textBlock); // Intermediary Institution
            ParseField57(message, textBlock); // Account With Institution
            ParseField70(message, textBlock); // Remittance Information
            ParseField71A(message, textBlock); // Details of Charges
            ParseField71F(message, textBlock); // Sender's Charges
            ParseField71G(message, textBlock); // Receiver's Charges
            ParseField72(message, textBlock); // Sender to Receiver Information
        });
    }

    /// <summary>
    /// Populates the Fields dictionary with all parsed field values
    /// </summary>
    /// <param name="message">The message object</param>
    /// <param name="blocks">Dictionary of parsed blocks</param>
    private static void PopulateFieldsDictionary(MT103Message message, Dictionary<string, string> blocks)
    {
        message.Fields.Clear();
        
        // Add block information
        foreach (var block in blocks)
        {
            message.Fields[block.Key] = block.Value;
        }
        
        // Add parsed field values
        message.Fields["20"] = message.TransactionReference;
        message.Fields["23B"] = message.BankOperationCode;
        message.Fields["32A"] = $"{message.ValueDate:yyMMdd}{message.Currency}{message.Amount}";
        
        if (!string.IsNullOrEmpty(message.OriginalCurrency) && message.OriginalAmount.HasValue)
            message.Fields["33B"] = $"{message.OriginalCurrency}{message.OriginalAmount}";
            
        if (!string.IsNullOrEmpty(message.OrderingInstitution))
            message.Fields["52A"] = message.OrderingInstitution;
            
        if (!string.IsNullOrEmpty(message.SendersCorrespondent))
            message.Fields["53A"] = message.SendersCorrespondent;
            
        if (!string.IsNullOrEmpty(message.ReceiversCorrespondent))
            message.Fields["54A"] = message.ReceiversCorrespondent;
            
        if (!string.IsNullOrEmpty(message.IntermediaryInstitution))
            message.Fields["56A"] = message.IntermediaryInstitution;
            
        if (!string.IsNullOrEmpty(message.AccountWithInstitution))
            message.Fields["57A"] = message.AccountWithInstitution;
            
        if (!string.IsNullOrEmpty(message.RemittanceInformation))
            message.Fields["70"] = message.RemittanceInformation;
            
        if (message.ChargeDetails != null)
            message.Fields["71A"] = message.ChargeDetails.ChargeBearer.ToString();
            
        if (!string.IsNullOrEmpty(message.SendersCharges))
            message.Fields["71F"] = message.SendersCharges;
            
        if (!string.IsNullOrEmpty(message.ReceiversCharges))
            message.Fields["71G"] = message.ReceiversCharges;
            
        if (!string.IsNullOrEmpty(message.SenderToReceiverInfo))
            message.Fields["72"] = message.SenderToReceiverInfo;
    }

    #region Field Parsing Methods

    /// <summary>
    /// Parses Field 20: Transaction Reference Number
    /// </summary>
    private static void ParseField20(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field20"].Match(textBlock);
        if (!match.Success)
            throw new SwiftParsingException("Missing mandatory field 20 (Transaction Reference)");
            
        message.TransactionReference = match.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Parses Field 23B: Bank Operation Code
    /// </summary>
    private static void ParseField23B(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field23B"].Match(textBlock);
        if (!match.Success)
            throw new SwiftParsingException("Missing mandatory field 23B (Bank Operation Code)");
            
        message.BankOperationCode = match.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Parses Field 32A: Value Date, Currency, Amount
    /// </summary>
    private static void ParseField32A(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field32A"].Match(textBlock);
        if (!match.Success)
            throw new SwiftParsingException("Missing mandatory field 32A (Value Date/Currency/Amount)");

        // Parse date (YYMMDD format)
        var dateString = match.Groups[1].Value;
        if (!DateTime.TryParseExact(dateString, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var valueDate))
            throw new SwiftParsingException($"Invalid date format in field 32A: {dateString}");
        message.ValueDate = valueDate;

        // Parse currency
        message.Currency = match.Groups[2].Value;

        // Parse amount - SWIFT uses comma as decimal separator
        var amountString = match.Groups[3].Value.Replace(",", ".");
        if (!decimal.TryParse(amountString, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            throw new SwiftParsingException($"Invalid amount format in field 32A: {match.Groups[3].Value}");
        message.Amount = amount;
    }

    /// <summary>
    /// Parses Field 33B: Original Currency/Amount (Optional)
    /// </summary>
    private static void ParseField33B(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field33B"].Match(textBlock);
        if (match.Success)
        {
            message.OriginalCurrency = match.Groups[1].Value;
            var amountString = match.Groups[2].Value.Replace(",", ".");
            if (decimal.TryParse(amountString, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                message.OriginalAmount = amount;
        }
    }

    /// <summary>
    /// Parses Field 50A/K: Ordering Customer
    /// </summary>
    private static void ParseField50(MT103Message message, string textBlock)
    {
        // Try Field 50A first (with BIC)
        var match50A = FieldPatterns["Field50A"].Match(textBlock);
        if (match50A.Success)
        {
            var account = match50A.Groups[1].Success ? match50A.Groups[1].Value.TrimStart('/') : null;
            var bic = match50A.Groups[2].Value;
            
            message.OrderingCustomer = new OrderingCustomer
            {
                Account = account,
                BIC = bic,
                Name = string.Empty // BIC format doesn't include name in this field
            };
            return;
        }

        // Try Field 50K (with name and address)
        var match50K = FieldPatterns["Field50K"].Match(textBlock);
        if (match50K.Success)
        {
            var content = match50K.Groups[1].Value.Trim();
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            string? account = null;
            var nameAndAddress = new List<string>();
            
            // First line might be account number if it starts with '/'
            var startIndex = 0;
            if (lines.Length > 0 && lines[0].StartsWith('/'))
            {
                account = lines[0].TrimStart('/');
                startIndex = 1;
            }
            
            // Remaining lines are name and address
            for (int i = startIndex; i < lines.Length; i++)
            {
                nameAndAddress.Add(lines[i].Trim());
            }
            
            message.OrderingCustomer = new OrderingCustomer
            {
                Account = account,
                Name = nameAndAddress.FirstOrDefault() ?? string.Empty,
                Address = string.Join("\n", nameAndAddress.Skip(1))
            };
            return;
        }

        throw new SwiftParsingException("Missing mandatory field 50A/K (Ordering Customer)");
    }

    /// <summary>
    /// Parses Field 52A: Ordering Institution (Optional)
    /// </summary>
    private static void ParseField52A(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field52A"].Match(textBlock);
        if (match.Success)
        {
            message.OrderingInstitution = match.Groups[1].Value;
        }
    }

    /// <summary>
    /// Parses Field 53A/B: Sender's Correspondent (Optional)
    /// </summary>
    private static void ParseField53(MT103Message message, string textBlock)
    {
        var match53A = FieldPatterns["Field53A"].Match(textBlock);
        if (match53A.Success)
        {
            message.SendersCorrespondent = match53A.Groups[1].Value;
            return;
        }

        var match53B = FieldPatterns["Field53B"].Match(textBlock);
        if (match53B.Success)
        {
            message.SendersCorrespondent = match53B.Groups[2].Value;
        }
    }

    /// <summary>
    /// Parses Field 54A: Receiver's Correspondent (Optional)
    /// </summary>
    private static void ParseField54A(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field54A"].Match(textBlock);
        if (match.Success)
        {
            message.ReceiversCorrespondent = match.Groups[2].Value;
        }
    }

    /// <summary>
    /// Parses Field 56A/C/D: Intermediary Institution (Optional)
    /// </summary>
    private static void ParseField56(MT103Message message, string textBlock)
    {
        var match56A = FieldPatterns["Field56A"].Match(textBlock);
        if (match56A.Success)
        {
            message.IntermediaryInstitution = match56A.Groups[1].Value;
            return;
        }

        var match56C = FieldPatterns["Field56C"].Match(textBlock);
        if (match56C.Success)
        {
            message.IntermediaryInstitution = match56C.Groups[1].Value;
            return;
        }

        var match56D = FieldPatterns["Field56D"].Match(textBlock);
        if (match56D.Success)
        {
            message.IntermediaryInstitution = match56D.Groups[1].Value;
        }
    }

    /// <summary>
    /// Parses Field 57A/B/C/D: Account With Institution (Optional)
    /// </summary>
    private static void ParseField57(MT103Message message, string textBlock)
    {
        var match57A = FieldPatterns["Field57A"].Match(textBlock);
        if (match57A.Success)
        {
            message.AccountWithInstitution = match57A.Groups[1].Value;
            return;
        }

        var match57B = FieldPatterns["Field57B"].Match(textBlock);
        if (match57B.Success)
        {
            message.AccountWithInstitution = match57B.Groups[2].Value;
            return;
        }

        var match57C = FieldPatterns["Field57C"].Match(textBlock);
        if (match57C.Success)
        {
            message.AccountWithInstitution = match57C.Groups[1].Value;
            return;
        }

        var match57D = FieldPatterns["Field57D"].Match(textBlock);
        if (match57D.Success)
        {
            message.AccountWithInstitution = match57D.Groups[1].Value;
        }
    }

    /// <summary>
    /// Parses Field 59/59A: Beneficiary Customer
    /// </summary>
    private static void ParseField59(MT103Message message, string textBlock)
    {
        // Try Field 59A first (with BIC)
        var match59A = FieldPatterns["Field59A"].Match(textBlock);
        if (match59A.Success)
        {
            var account = match59A.Groups[1].Success ? match59A.Groups[1].Value.TrimStart('/') : null;
            var bic = match59A.Groups[2].Value;
            
            message.BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Account = account,
                BIC = bic,
                Name = string.Empty // BIC format doesn't include name in this field
            };
            return;
        }

        // Try Field 59 (with name and address)
        var match59 = FieldPatterns["Field59"].Match(textBlock);
        if (match59.Success)
        {
            var account = match59.Groups[1].Success ? match59.Groups[1].Value.TrimStart('/') : null;
            var content = match59.Groups[2].Value.Trim();
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            message.BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Account = account,
                Name = lines.FirstOrDefault() ?? string.Empty,
                Address = string.Join("\n", lines.Skip(1))
            };
            return;
        }

        throw new SwiftParsingException("Missing mandatory field 59/59A (Beneficiary Customer)");
    }

    /// <summary>
    /// Parses Field 70: Remittance Information (Optional)
    /// </summary>
    private static void ParseField70(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field70"].Match(textBlock);
        if (match.Success)
        {
            message.RemittanceInformation = match.Groups[1].Value.Trim();
        }
    }

    /// <summary>
    /// Parses Field 71A: Details of Charges (Optional)
    /// </summary>
    private static void ParseField71A(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field71A"].Match(textBlock);
        if (match.Success)
        {
            var chargeCode = match.Groups[1].Value;
            if (Enum.TryParse<ChargeBearer>(chargeCode, true, out var chargeBearer))
            {
                message.ChargeDetails = new ChargeDetails { ChargeBearer = chargeBearer };
            }
        }
    }

    /// <summary>
    /// Parses Field 71F: Sender's Charges (Optional)
    /// </summary>
    private static void ParseField71F(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field71F"].Match(textBlock);
        if (match.Success)
        {
            var currency = match.Groups[1].Value;
            var amount = match.Groups[2].Value.Replace(",", ".");
            message.SendersCharges = $"{currency}{amount}";
        }
    }

    /// <summary>
    /// Parses Field 71G: Receiver's Charges (Optional)
    /// </summary>
    private static void ParseField71G(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field71G"].Match(textBlock);
        if (match.Success)
        {
            var currency = match.Groups[1].Value;
            var amount = match.Groups[2].Value.Replace(",", ".");
            message.ReceiversCharges = $"{currency}{amount}";
        }
    }

    /// <summary>
    /// Parses Field 72: Sender to Receiver Information (Optional)
    /// </summary>
    private static void ParseField72(MT103Message message, string textBlock)
    {
        var match = FieldPatterns["Field72"].Match(textBlock);
        if (match.Success)
        {
            message.SenderToReceiverInfo = match.Groups[1].Value.Trim();
        }
    }

    #endregion
}

/// <summary>
/// Exception thrown when SWIFT message parsing fails
/// </summary>
public class SwiftParsingException : Exception
{
    public SwiftParsingException(string message) : base(message) { }
    public SwiftParsingException(string message, Exception innerException) : base(message, innerException) { }
}