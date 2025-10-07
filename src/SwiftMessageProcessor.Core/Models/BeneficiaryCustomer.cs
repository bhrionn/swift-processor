using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Represents the beneficiary customer in a SWIFT message (Field 59/59A)
/// </summary>
public class BeneficiaryCustomer
{
    /// <summary>
    /// Beneficiary name (required)
    /// Format: 35x (max 35 characters per line, up to 4 lines)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Beneficiary address (required for option without BIC)
    /// Format: 35x (max 35 characters per line, up to 3 lines)
    /// </summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// Beneficiary account number (optional)
    /// Format: 34x (max 34 characters)
    /// </summary>
    public string? Account { get; set; }
    
    /// <summary>
    /// Bank identifier code (BIC) - required for option 59A
    /// Format: 11!c (exactly 8 or 11 characters)
    /// </summary>
    public string? BIC { get; set; }
    
    /// <summary>
    /// Indicates whether this is option 59A (with BIC) or option 59 (with name and address)
    /// </summary>
    public bool IsOption59A => !string.IsNullOrEmpty(BIC);
    
    /// <summary>
    /// Validates the beneficiary customer information according to SWIFT standards
    /// </summary>
    /// <returns>ValidationResult indicating success or failure with error details</returns>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // Account number validation if present
        if (!string.IsNullOrWhiteSpace(Account) && !IsValidAccountNumber(Account))
        {
            errors.Add("Beneficiary account number format is invalid (max 34 characters, alphanumeric and special characters allowed)");
        }
        
        // Name is always required
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Beneficiary name is required");
        }
        else if (!IsValidCustomerName(Name))
        {
            errors.Add("Beneficiary name format is invalid (max 4 lines of 35 characters each)");
        }
        
        if (IsOption59A)
        {
            // Option 59A: Account number and BIC
            if (!IsValidBIC(BIC))
            {
                errors.Add("Bank code (BIC) is required and must be valid for option 59A");
            }
        }
        else
        {
            // Option 59: Account number, name and address
            if (string.IsNullOrWhiteSpace(Address))
            {
                errors.Add("Beneficiary address is required when BIC is not provided (option 59)");
            }
            else if (!IsValidAddress(Address))
            {
                errors.Add("Beneficiary address format is invalid (max 3 lines of 35 characters each)");
            }
        }
        
        return errors.Count == 0 
            ? ValidationResult.Success! 
            : new ValidationResult(string.Join("; ", errors));
    }
    
    /// <summary>
    /// Validates a SWIFT BIC (Bank Identifier Code)
    /// </summary>
    /// <param name="bic">The BIC to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidBIC(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return false;
            
        // BIC format: 4 letters (bank code) + 2 letters (country code) + 2 alphanumeric (location) + optional 3 alphanumeric (branch)
        var bicPattern = @"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$";
        return Regex.IsMatch(bic.ToUpperInvariant(), bicPattern);
    }
    
    /// <summary>
    /// Validates an account number format
    /// </summary>
    /// <param name="accountNumber">The account number to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return false;
            
        // Account numbers can be alphanumeric with special characters, max 34 characters
        var accountPattern = @"^[A-Z0-9/\-\?:\(\)\.,'\+\s]{1,34}$";
        return Regex.IsMatch(accountNumber.ToUpperInvariant(), accountPattern);
    }
    
    /// <summary>
    /// Validates customer name format
    /// </summary>
    /// <param name="name">The customer name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidCustomerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
            
        var lines = name.Split('\n');
        return lines.Length <= 4 && lines.All(line => line.Length <= 35 && IsValidSwiftCharacters(line));
    }
    
    /// <summary>
    /// Validates address format
    /// </summary>
    /// <param name="address">The address to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;
            
        var lines = address.Split('\n');
        return lines.Length <= 3 && lines.All(line => line.Length <= 35 && IsValidSwiftCharacters(line));
    }
    
    /// <summary>
    /// Validates that a string contains only valid SWIFT characters
    /// </summary>
    /// <param name="text">The text to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidSwiftCharacters(string text)
    {
        // SWIFT allows: A-Z, 0-9, and specific special characters
        var swiftPattern = @"^[A-Z0-9/\-\?:\(\)\.,'\+\s]*$";
        return Regex.IsMatch(text.ToUpperInvariant(), swiftPattern);
    }
}