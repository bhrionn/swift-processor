using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Abstract base class for all SWIFT message types
/// </summary>
public abstract class SwiftMessage
{
    /// <summary>
    /// The SWIFT message type (e.g., MT103, MT102)
    /// </summary>
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// The original raw SWIFT message content
    /// </summary>
    public string RawMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the message was received
    /// </summary>
    public DateTime ReceivedAt { get; set; }
    
    /// <summary>
    /// Dictionary containing all parsed SWIFT fields
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();
    
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Processing status of the message
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    
    /// <summary>
    /// Validates the message according to SWIFT standards
    /// </summary>
    /// <returns>ValidationResult indicating success or failure with error details</returns>
    public abstract ValidationResult Validate();
    
    /// <summary>
    /// Validates a SWIFT BIC (Bank Identifier Code)
    /// </summary>
    /// <param name="bic">The BIC to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidBIC(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return false;
            
        // BIC format: 4 letters (bank code) + 2 letters (country code) + 2 alphanumeric (location) + optional 3 alphanumeric (branch)
        var bicPattern = @"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$";
        return Regex.IsMatch(bic.ToUpperInvariant(), bicPattern);
    }
    
    /// <summary>
    /// Validates a currency code (ISO 4217)
    /// </summary>
    /// <param name="currency">The currency code to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return false;
            
        // ISO 4217 currency codes are 3 uppercase letters
        var currencyPattern = @"^[A-Z]{3}$";
        return Regex.IsMatch(currency, currencyPattern);
    }
    
    /// <summary>
    /// Validates a SWIFT date format (YYMMDD)
    /// </summary>
    /// <param name="dateString">The date string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidSwiftDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString) || dateString.Length != 6)
            return false;
            
        return DateTime.TryParseExact(dateString, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out _);
    }
    
    /// <summary>
    /// Validates an account number format
    /// </summary>
    /// <param name="accountNumber">The account number to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return false;
            
        // Account numbers can be alphanumeric, typically 1-34 characters
        var accountPattern = @"^[A-Z0-9/\-\?:\(\)\.,'\+\s]{1,34}$";
        return Regex.IsMatch(accountNumber.ToUpperInvariant(), accountPattern);
    }
    
    /// <summary>
    /// Validates an amount format
    /// </summary>
    /// <param name="amount">The amount to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidAmount(decimal amount)
    {
        return amount > 0 && amount <= 999999999999.99m; // SWIFT maximum amount
    }
    
    /// <summary>
    /// Validates a transaction reference format (Field 20)
    /// </summary>
    /// <param name="reference">The reference to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    protected static bool IsValidTransactionReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return false;
            
        // Transaction reference: 1-16 characters, alphanumeric and some special characters
        var referencePattern = @"^[A-Z0-9/\-\?:\(\)\.,'\+\s]{1,16}$";
        return Regex.IsMatch(reference.ToUpperInvariant(), referencePattern);
    }
}