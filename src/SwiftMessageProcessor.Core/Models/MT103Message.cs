using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Represents a SWIFT MT103 Single Customer Credit Transfer message
/// </summary>
public class MT103Message : SwiftMessage
{
    public MT103Message()
    {
        MessageType = "MT103";
    }
    
    #region Mandatory Fields
    
    /// <summary>
    /// Field 20: Transaction Reference Number
    /// Sender's unique reference for the transaction
    /// Format: 16x (max 16 characters)
    /// </summary>
    public string TransactionReference { get; set; } = string.Empty;
    
    /// <summary>
    /// Field 23B: Bank Operation Code
    /// Typically 'CRED' for credit transfers
    /// Format: 4!c (exactly 4 characters)
    /// </summary>
    public string BankOperationCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Field 32A: Value Date from the transaction
    /// Format: YYMMDD
    /// </summary>
    public DateTime ValueDate { get; set; }
    
    /// <summary>
    /// Field 32A: Currency Code (ISO 4217)
    /// Format: 3!a (exactly 3 letters)
    /// </summary>
    public string Currency { get; set; } = string.Empty;
    
    /// <summary>
    /// Field 32A: Interbank Settled Amount
    /// Format: 15d (max 15 digits including decimals)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Field 50A/K: Ordering Customer
    /// The customer ordering the transaction
    /// </summary>
    public OrderingCustomer OrderingCustomer { get; set; } = new();
    
    /// <summary>
    /// Field 59/59A: Beneficiary Customer
    /// The ultimate recipient of the funds
    /// </summary>
    public BeneficiaryCustomer BeneficiaryCustomer { get; set; } = new();
    
    #endregion
    
    #region Optional Fields
    
    /// <summary>
    /// Field 33B: Currency/Original Ordered Amount
    /// Original currency if different from settlement currency
    /// </summary>
    public string? OriginalCurrency { get; set; }
    
    /// <summary>
    /// Field 33B: Original Amount
    /// Original amount if different from settlement amount
    /// </summary>
    public decimal? OriginalAmount { get; set; }
    
    /// <summary>
    /// Field 52A: Ordering Institution
    /// The institution of the ordering customer
    /// </summary>
    public string? OrderingInstitution { get; set; }
    
    /// <summary>
    /// Field 53A/B: Sender's Correspondent
    /// Correspondent bank of the sender
    /// </summary>
    public string? SendersCorrespondent { get; set; }
    
    /// <summary>
    /// Field 54A: Receiver's Correspondent
    /// Correspondent bank of the receiver
    /// </summary>
    public string? ReceiversCorrespondent { get; set; }
    
    /// <summary>
    /// Field 56A/C/D: Intermediary Institution
    /// Intermediary bank in the payment chain
    /// </summary>
    public string? IntermediaryInstitution { get; set; }
    
    /// <summary>
    /// Field 57A/B/C/D: Account With Institution
    /// The institution where the beneficiary has their account
    /// </summary>
    public string? AccountWithInstitution { get; set; }
    
    /// <summary>
    /// Field 70: Remittance Information
    /// Payment details and purpose
    /// Format: 4*35x (max 4 lines of 35 characters each)
    /// </summary>
    public string? RemittanceInformation { get; set; }
    
    /// <summary>
    /// Field 71A: Details of Charges
    /// Who bears the charges (BEN/OUR/SHA)
    /// </summary>
    public ChargeDetails? ChargeDetails { get; set; }
    
    /// <summary>
    /// Field 71F: Sender's Charges
    /// Charges deducted by sender and previous banks
    /// </summary>
    public string? SendersCharges { get; set; }
    
    /// <summary>
    /// Field 71G: Receiver's Charges
    /// Charges due to the receiver
    /// </summary>
    public string? ReceiversCharges { get; set; }
    
    /// <summary>
    /// Field 72: Sender to Receiver Information
    /// Additional instructions for the receiving bank
    /// Format: 6*35x (max 6 lines of 35 characters each)
    /// </summary>
    public string? SenderToReceiverInfo { get; set; }
    
    #endregion
    
    /// <summary>
    /// Validates the MT103 message according to SWIFT standards
    /// </summary>
    /// <returns>ValidationResult with detailed error information</returns>
    public override ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // Validate mandatory fields
        ValidateMandatoryFields(errors);
        
        // Validate optional fields if present
        ValidateOptionalFields(errors);
        
        // Validate business rules
        ValidateBusinessRules(errors);
        
        return errors.Count == 0 
            ? ValidationResult.Success! 
            : new ValidationResult(string.Join("; ", errors));
    }
    
    /// <summary>
    /// Validates all mandatory fields according to SWIFT MT103 specifications
    /// </summary>
    /// <param name="errors">List to collect validation errors</param>
    private void ValidateMandatoryFields(List<string> errors)
    {
        // Field 20: Transaction Reference
        if (!IsValidTransactionReference(TransactionReference))
        {
            errors.Add("Field 20: Transaction reference is required and must be 1-16 alphanumeric characters");
        }
        
        // Field 23B: Bank Operation Code
        if (!IsValidBankOperationCode(BankOperationCode))
        {
            errors.Add("Field 23B: Bank operation code is required and must be exactly 4 characters (typically 'CRED')");
        }
        
        // Field 32A: Value Date
        if (ValueDate == default || ValueDate < DateTime.Today.AddYears(-1) || ValueDate > DateTime.Today.AddYears(1))
        {
            errors.Add("Field 32A: Value date must be within reasonable range (not more than 1 year in past or future)");
        }
        
        // Field 32A: Currency
        if (!IsValidCurrency(Currency))
        {
            errors.Add("Field 32A: Currency code is required and must be a valid 3-letter ISO 4217 code");
        }
        
        // Field 32A: Amount
        if (!IsValidAmount(Amount))
        {
            errors.Add("Field 32A: Amount must be greater than zero and within SWIFT limits");
        }
        
        // Field 50A/K: Ordering Customer
        ValidateOrderingCustomer(errors);
        
        // Field 59: Beneficiary Customer
        ValidateBeneficiaryCustomer(errors);
    }
    
    /// <summary>
    /// Validates optional fields if they are present
    /// </summary>
    /// <param name="errors">List to collect validation errors</param>
    private void ValidateOptionalFields(List<string> errors)
    {
        // Field 33B: Original Currency/Amount
        if (!string.IsNullOrEmpty(OriginalCurrency) && !IsValidCurrency(OriginalCurrency))
        {
            errors.Add("Field 33B: Original currency must be a valid 3-letter ISO 4217 code if specified");
        }
        
        if (OriginalAmount.HasValue && !IsValidAmount(OriginalAmount.Value))
        {
            errors.Add("Field 33B: Original amount must be greater than zero if specified");
        }
        
        // Field 52A: Ordering Institution
        if (!string.IsNullOrEmpty(OrderingInstitution) && !IsValidBIC(OrderingInstitution))
        {
            errors.Add("Field 52A: Ordering institution must be a valid BIC if specified");
        }
        
        // Field 53A/B: Sender's Correspondent
        if (!string.IsNullOrEmpty(SendersCorrespondent) && !IsValidCorrespondentFormat(SendersCorrespondent))
        {
            errors.Add("Field 53A/B: Sender's correspondent must be in valid format if specified");
        }
        
        // Field 56A/C/D: Intermediary Institution
        if (!string.IsNullOrEmpty(IntermediaryInstitution) && !IsValidBIC(IntermediaryInstitution))
        {
            errors.Add("Field 56A/C/D: Intermediary institution must be a valid BIC if specified");
        }
        
        // Field 57A/B/C/D: Account With Institution
        if (!string.IsNullOrEmpty(AccountWithInstitution) && !IsValidBIC(AccountWithInstitution))
        {
            errors.Add("Field 57A/B/C/D: Account with institution must be a valid BIC if specified");
        }
        
        // Field 70: Remittance Information
        if (!string.IsNullOrEmpty(RemittanceInformation) && !IsValidRemittanceInfo(RemittanceInformation))
        {
            errors.Add("Field 70: Remittance information must not exceed 4 lines of 35 characters each");
        }
        
        // Field 72: Sender to Receiver Information
        if (!string.IsNullOrEmpty(SenderToReceiverInfo) && !IsValidSenderToReceiverInfo(SenderToReceiverInfo))
        {
            errors.Add("Field 72: Sender to receiver information must not exceed 6 lines of 35 characters each");
        }
    }
    
    /// <summary>
    /// Validates business rules and cross-field dependencies
    /// </summary>
    /// <param name="errors">List to collect validation errors</param>
    private void ValidateBusinessRules(List<string> errors)
    {
        // If original currency is specified, original amount should also be specified
        if (!string.IsNullOrEmpty(OriginalCurrency) && !OriginalAmount.HasValue)
        {
            errors.Add("Business Rule: If original currency is specified, original amount must also be provided");
        }
        
        // Original currency should be different from settlement currency if specified
        if (!string.IsNullOrEmpty(OriginalCurrency) && OriginalCurrency.Equals(Currency, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Business Rule: Original currency should be different from settlement currency");
        }
        
        // Validate charge details if present
        if (ChargeDetails != null)
        {
            var chargeValidation = ChargeDetails.Validate();
            if (chargeValidation != ValidationResult.Success)
            {
                errors.Add($"Field 71A: {chargeValidation.ErrorMessage}");
            }
        }
    }
    
    /// <summary>
    /// Validates the ordering customer information
    /// </summary>
    /// <param name="errors">List to collect validation errors</param>
    private void ValidateOrderingCustomer(List<string> errors)
    {
        if (OrderingCustomer == null)
        {
            errors.Add("Field 50A/K: Ordering customer information is required");
            return;
        }
        
        var customerValidation = OrderingCustomer.Validate();
        if (customerValidation != ValidationResult.Success)
        {
            errors.Add($"Field 50A/K: {customerValidation.ErrorMessage}");
        }
    }
    
    /// <summary>
    /// Validates the beneficiary customer information
    /// </summary>
    /// <param name="errors">List to collect validation errors</param>
    private void ValidateBeneficiaryCustomer(List<string> errors)
    {
        if (BeneficiaryCustomer == null)
        {
            errors.Add("Field 59: Beneficiary customer information is required");
            return;
        }
        
        var customerValidation = BeneficiaryCustomer.Validate();
        if (customerValidation != ValidationResult.Success)
        {
            errors.Add($"Field 59: {customerValidation.ErrorMessage}");
        }
    }
    
    /// <summary>
    /// Validates bank operation code format
    /// </summary>
    /// <param name="code">The bank operation code to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidBankOperationCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
            
        // Bank operation code should be exactly 4 characters, typically 'CRED'
        var validCodes = new[] { "CRED", "CRTS", "SPAY", "SPRI", "SSTD" };
        return validCodes.Contains(code.ToUpperInvariant());
    }
    
    /// <summary>
    /// Validates correspondent bank format
    /// </summary>
    /// <param name="correspondent">The correspondent to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidCorrespondentFormat(string correspondent)
    {
        if (string.IsNullOrWhiteSpace(correspondent))
            return false;
            
        // Can be BIC or account number format
        return IsValidBIC(correspondent) || IsValidAccountNumber(correspondent);
    }
    
    /// <summary>
    /// Validates remittance information format
    /// </summary>
    /// <param name="remittanceInfo">The remittance information to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidRemittanceInfo(string remittanceInfo)
    {
        if (string.IsNullOrEmpty(remittanceInfo))
            return true; // Optional field
            
        var lines = remittanceInfo.Split('\n');
        return lines.Length <= 4 && lines.All(line => line.Length <= 35);
    }
    
    /// <summary>
    /// Validates sender to receiver information format
    /// </summary>
    /// <param name="senderToReceiverInfo">The sender to receiver information to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidSenderToReceiverInfo(string senderToReceiverInfo)
    {
        if (string.IsNullOrEmpty(senderToReceiverInfo))
            return true; // Optional field
            
        var lines = senderToReceiverInfo.Split('\n');
        return lines.Length <= 6 && lines.All(line => line.Length <= 35);
    }
}