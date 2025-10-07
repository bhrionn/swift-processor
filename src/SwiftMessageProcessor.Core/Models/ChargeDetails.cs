using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Represents charge details in a SWIFT message (Field 71A)
/// </summary>
public class ChargeDetails
{
    /// <summary>
    /// Charge bearer indicating who bears the charges
    /// Valid values: BEN (beneficiary), OUR (ordering customer), SHA (shared)
    /// </summary>
    public ChargeBearer ChargeBearer { get; set; } = ChargeBearer.SHA;
    
    /// <summary>
    /// Optional charge amount
    /// Format: 15d (max 15 digits including decimals)
    /// </summary>
    public decimal? ChargeAmount { get; set; }
    
    /// <summary>
    /// Optional charge currency (ISO 4217)
    /// Format: 3!a (exactly 3 letters)
    /// </summary>
    public string? ChargeCurrency { get; set; }
    
    /// <summary>
    /// Validates the charge details according to SWIFT standards
    /// </summary>
    /// <returns>ValidationResult indicating success or failure with error details</returns>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // Charge bearer is always valid since it's an enum
        
        // If charge amount is specified, it must be valid
        if (ChargeAmount.HasValue && !IsValidChargeAmount(ChargeAmount.Value))
        {
            errors.Add("Charge amount must be greater than zero if specified");
        }
        
        // If charge currency is specified, it must be valid
        if (!string.IsNullOrEmpty(ChargeCurrency) && !IsValidCurrency(ChargeCurrency))
        {
            errors.Add("Charge currency must be a valid 3-letter ISO 4217 code if specified");
        }
        
        // If charge amount is specified, currency should also be specified
        if (ChargeAmount.HasValue && string.IsNullOrEmpty(ChargeCurrency))
        {
            errors.Add("Charge currency is required when charge amount is specified");
        }
        
        return errors.Count == 0 
            ? ValidationResult.Success! 
            : new ValidationResult(string.Join("; ", errors));
    }
    

    
    /// <summary>
    /// Validates the charge amount
    /// </summary>
    /// <param name="amount">The charge amount to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidChargeAmount(decimal amount)
    {
        return amount > 0 && amount <= 999999999999.99m; // SWIFT maximum amount
    }
    
    /// <summary>
    /// Validates a currency code (ISO 4217)
    /// </summary>
    /// <param name="currency">The currency code to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return false;
            
        // ISO 4217 currency codes are 3 uppercase letters
        var currencyPattern = @"^[A-Z]{3}$";
        return Regex.IsMatch(currency, currencyPattern);
    }
}