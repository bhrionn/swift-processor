using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Parsers;

namespace SwiftMessageProcessor.Core.Examples;

/// <summary>
/// Example demonstrating how to use the MT103Parser
/// </summary>
public static class MT103ParserExample
{
    /// <summary>
    /// Example of parsing a complete MT103 message
    /// </summary>
    public static async Task<MT103Message> ParseExampleMessage()
    {
        var parser = new MT103Parser();
        
        var rawMessage = @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:33B:USD150000,00
:50K:/12345678901234567890
JOHN DOE COMPANY
123 MAIN STREET
NEW YORK NY 10001
:52A:DEUTDEFF
:53A:CHASUS33
:56A:BNPAFRPP
:57A:CRESCHZZ
:59:/98765432109876543210
JANE SMITH
456 OAK AVENUE
LONDON EC1A 1BB
:70:PAYMENT FOR INVOICE 12345
:71A:SHA
:72:ADDITIONAL INSTRUCTIONS FOR PROCESSING
-}";

        var message = await parser.ParseAsync(rawMessage);
        var validationResult = await parser.ValidateAsync(message);
        
        if (validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
        {
            throw new InvalidOperationException($"Message validation failed: {validationResult.ErrorMessage}");
        }
        
        return message;
    }
    
    /// <summary>
    /// Example of parsing a minimal MT103 message with only mandatory fields
    /// </summary>
    public static async Task<MT103Message> ParseMinimalMessage()
    {
        var parser = new MT103Parser();
        
        var rawMessage = @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REF123
:23B:CRED
:32A:241215EUR1000,50
:50K:ORDERING CUSTOMER
123 CUSTOMER STREET
CUSTOMER CITY
:59:BENEFICIARY CUSTOMER
456 BENEFICIARY AVENUE
BENEFICIARY CITY
-}";

        var message = await parser.ParseAsync(rawMessage);
        return message;
    }
}