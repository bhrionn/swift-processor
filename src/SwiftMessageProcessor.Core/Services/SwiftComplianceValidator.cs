using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Models;

namespace SwiftMessageProcessor.Core.Services;

/// <summary>
/// Service for validating SWIFT messages against compliance rules and standards
/// </summary>
public interface ISwiftComplianceValidator
{
    Task<ComplianceValidationResult> ValidateComplianceAsync(MT103Message message);
    Task<ComplianceValidationResult> ValidateBusinessRulesAsync(MT103Message message);
    Task<ComplianceValidationResult> ValidateSanctionsAsync(MT103Message message);
    Task<ComplianceValidationResult> ValidateAmountLimitsAsync(MT103Message message);
}

public class SwiftComplianceValidator : ISwiftComplianceValidator
{
    private readonly ILogger<SwiftComplianceValidator> _logger;
    
    // Configurable limits - in production these would come from configuration
    private const decimal MaxTransactionAmount = 10_000_000m; // 10 million
    private const decimal HighValueThreshold = 1_000_000m; // 1 million
    
    public SwiftComplianceValidator(ILogger<SwiftComplianceValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive compliance validation on an MT103 message
    /// </summary>
    public async Task<ComplianceValidationResult> ValidateComplianceAsync(MT103Message message)
    {
        var result = new ComplianceValidationResult();
        
        // Validate SWIFT format compliance
        var formatValidation = await ValidateFormatComplianceAsync(message);
        result.Merge(formatValidation);
        
        // Validate business rules
        var businessValidation = await ValidateBusinessRulesAsync(message);
        result.Merge(businessValidation);
        
        // Validate amount limits
        var amountValidation = await ValidateAmountLimitsAsync(message);
        result.Merge(amountValidation);
        
        // Validate sanctions (placeholder - would integrate with sanctions screening system)
        var sanctionsValidation = await ValidateSanctionsAsync(message);
        result.Merge(sanctionsValidation);
        
        _logger.LogInformation(
            "Compliance validation completed for transaction {TransactionRef}: {IsCompliant}",
            message.TransactionReference,
            result.IsCompliant);
        
        return result;
    }

    /// <summary>
    /// Validates SWIFT format compliance
    /// </summary>
    private async Task<ComplianceValidationResult> ValidateFormatComplianceAsync(MT103Message message)
    {
        var result = new ComplianceValidationResult();
        
        await Task.Run(() =>
        {
            // Validate transaction reference format
            if (!IsValidSwiftReference(message.TransactionReference))
            {
                result.AddViolation(
                    ComplianceViolationType.FormatViolation,
                    "Field 20",
                    "Transaction reference contains invalid characters or format",
                    ComplianceSeverity.High);
            }
            
            // Validate currency codes
            if (!IsValidIsoCurrency(message.Currency))
            {
                result.AddViolation(
                    ComplianceViolationType.FormatViolation,
                    "Field 32A",
                    $"Invalid ISO 4217 currency code: {message.Currency}",
                    ComplianceSeverity.Critical);
            }
            
            if (!string.IsNullOrEmpty(message.OriginalCurrency) && !IsValidIsoCurrency(message.OriginalCurrency))
            {
                result.AddViolation(
                    ComplianceViolationType.FormatViolation,
                    "Field 33B",
                    $"Invalid ISO 4217 original currency code: {message.OriginalCurrency}",
                    ComplianceSeverity.High);
            }
            
            // Validate BIC codes
            ValidateBicCodes(message, result);
            
            // Validate character sets
            ValidateCharacterSets(message, result);
        });
        
        return result;
    }

    /// <summary>
    /// Validates business rules for financial transactions
    /// </summary>
    public async Task<ComplianceValidationResult> ValidateBusinessRulesAsync(MT103Message message)
    {
        var result = new ComplianceValidationResult();
        
        await Task.Run(() =>
        {
            // Rule: Value date should not be too far in the past or future
            var daysDifference = (message.ValueDate - DateTime.Today).Days;
            if (Math.Abs(daysDifference) > 365)
            {
                result.AddViolation(
                    ComplianceViolationType.BusinessRuleViolation,
                    "Field 32A",
                    $"Value date is {Math.Abs(daysDifference)} days from today, exceeds 365-day limit",
                    ComplianceSeverity.Medium);
            }
            
            // Rule: Ordering and beneficiary customers must be different
            if (IsSameCustomer(message.OrderingCustomer, message.BeneficiaryCustomer))
            {
                result.AddViolation(
                    ComplianceViolationType.BusinessRuleViolation,
                    "Fields 50/59",
                    "Ordering customer and beneficiary customer appear to be the same",
                    ComplianceSeverity.Medium);
            }
            
            // Rule: If original amount is specified, it should differ from settlement amount
            if (message.OriginalAmount.HasValue && message.OriginalAmount.Value == message.Amount)
            {
                result.AddViolation(
                    ComplianceViolationType.BusinessRuleViolation,
                    "Fields 32A/33B",
                    "Original amount equals settlement amount - field 33B may be unnecessary",
                    ComplianceSeverity.Low);
            }
            
            // Rule: Charge bearer validation
            if (message.ChargeDetails != null)
            {
                ValidateChargeBearer(message, result);
            }
            
            // Rule: Remittance information should be meaningful
            if (string.IsNullOrWhiteSpace(message.RemittanceInformation))
            {
                result.AddWarning(
                    "Field 70",
                    "Remittance information is empty - consider adding payment purpose");
            }
        });
        
        return result;
    }

    /// <summary>
    /// Validates sanctions compliance (placeholder for actual sanctions screening)
    /// </summary>
    public async Task<ComplianceValidationResult> ValidateSanctionsAsync(MT103Message message)
    {
        var result = new ComplianceValidationResult();
        
        await Task.Run(() =>
        {
            // In production, this would integrate with sanctions screening systems
            // such as OFAC, EU sanctions lists, UN sanctions, etc.
            
            // Placeholder: Check for high-risk countries (example only)
            var highRiskIndicators = new[] { "SANCTIONED", "BLOCKED", "RESTRICTED" };
            
            // Check ordering customer
            if (ContainsHighRiskIndicators(message.OrderingCustomer?.Name, highRiskIndicators))
            {
                result.AddViolation(
                    ComplianceViolationType.SanctionsViolation,
                    "Field 50",
                    "Ordering customer name contains high-risk indicators",
                    ComplianceSeverity.Critical);
            }
            
            // Check beneficiary customer
            if (ContainsHighRiskIndicators(message.BeneficiaryCustomer?.Name, highRiskIndicators))
            {
                result.AddViolation(
                    ComplianceViolationType.SanctionsViolation,
                    "Field 59",
                    "Beneficiary customer name contains high-risk indicators",
                    ComplianceSeverity.Critical);
            }
            
            _logger.LogInformation(
                "Sanctions screening completed for transaction {TransactionRef}",
                message.TransactionReference);
        });
        
        return result;
    }

    /// <summary>
    /// Validates transaction amount limits
    /// </summary>
    public async Task<ComplianceValidationResult> ValidateAmountLimitsAsync(MT103Message message)
    {
        var result = new ComplianceValidationResult();
        
        await Task.Run(() =>
        {
            // Check maximum transaction limit
            if (message.Amount > MaxTransactionAmount)
            {
                result.AddViolation(
                    ComplianceViolationType.AmountLimitViolation,
                    "Field 32A",
                    $"Transaction amount {message.Amount:N2} {message.Currency} exceeds maximum limit of {MaxTransactionAmount:N2}",
                    ComplianceSeverity.Critical);
            }
            
            // Flag high-value transactions for additional review
            if (message.Amount >= HighValueThreshold)
            {
                result.AddWarning(
                    "Field 32A",
                    $"High-value transaction: {message.Amount:N2} {message.Currency} - may require additional review");
            }
            
            // Validate amount precision (SWIFT allows max 2 decimal places for most currencies)
            var decimalPlaces = BitConverter.GetBytes(decimal.GetBits(message.Amount)[3])[2];
            if (decimalPlaces > 2)
            {
                result.AddViolation(
                    ComplianceViolationType.FormatViolation,
                    "Field 32A",
                    "Amount has more than 2 decimal places",
                    ComplianceSeverity.Medium);
            }
        });
        
        return result;
    }

    #region Helper Methods

    private static bool IsValidSwiftReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 16)
            return false;
        
        // SWIFT references should contain only alphanumeric characters and limited special characters
        return Regex.IsMatch(reference, @"^[A-Za-z0-9/\-?:().,'+\s]+$");
    }

    private static bool IsValidIsoCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return false;
        
        // Common ISO 4217 currency codes
        var validCurrencies = new HashSet<string>
        {
            "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "NZD",
            "SEK", "NOK", "DKK", "PLN", "CZK", "HUF", "RON", "BGN",
            "HRK", "RUB", "TRY", "BRL", "MXN", "ZAR", "INR", "CNY",
            "HKD", "SGD", "KRW", "THB", "MYR", "IDR", "PHP", "AED",
            "SAR", "QAR", "KWD", "BHD", "OMR", "JOD", "ILS", "EGP"
        };
        
        return validCurrencies.Contains(currency.ToUpperInvariant());
    }

    private void ValidateBicCodes(MT103Message message, ComplianceValidationResult result)
    {
        // Validate ordering institution BIC
        if (!string.IsNullOrEmpty(message.OrderingInstitution) && !IsValidBic(message.OrderingInstitution))
        {
            result.AddViolation(
                ComplianceViolationType.FormatViolation,
                "Field 52A",
                $"Invalid BIC format for ordering institution: {message.OrderingInstitution}",
                ComplianceSeverity.High);
        }
        
        // Validate intermediary institution BIC
        if (!string.IsNullOrEmpty(message.IntermediaryInstitution) && !IsValidBic(message.IntermediaryInstitution))
        {
            result.AddViolation(
                ComplianceViolationType.FormatViolation,
                "Field 56A",
                $"Invalid BIC format for intermediary institution: {message.IntermediaryInstitution}",
                ComplianceSeverity.High);
        }
        
        // Validate account with institution BIC
        if (!string.IsNullOrEmpty(message.AccountWithInstitution) && !IsValidBic(message.AccountWithInstitution))
        {
            result.AddViolation(
                ComplianceViolationType.FormatViolation,
                "Field 57A",
                $"Invalid BIC format for account with institution: {message.AccountWithInstitution}",
                ComplianceSeverity.High);
        }
    }

    private static bool IsValidBic(string bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return false;
        
        // BIC format: 4 letters (institution code) + 2 letters (country code) + 2 alphanumeric (location) + optional 3 alphanumeric (branch)
        return Regex.IsMatch(bic, @"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$");
    }

    private void ValidateCharacterSets(MT103Message message, ComplianceValidationResult result)
    {
        // SWIFT uses a limited character set (SWIFT X character set)
        var invalidCharsPattern = @"[^\x20-\x7E]"; // Only printable ASCII characters
        
        if (!string.IsNullOrEmpty(message.RemittanceInformation) && 
            Regex.IsMatch(message.RemittanceInformation, invalidCharsPattern))
        {
            result.AddViolation(
                ComplianceViolationType.FormatViolation,
                "Field 70",
                "Remittance information contains invalid characters (non-SWIFT character set)",
                ComplianceSeverity.Medium);
        }
        
        if (!string.IsNullOrEmpty(message.SenderToReceiverInfo) && 
            Regex.IsMatch(message.SenderToReceiverInfo, invalidCharsPattern))
        {
            result.AddViolation(
                ComplianceViolationType.FormatViolation,
                "Field 72",
                "Sender to receiver information contains invalid characters",
                ComplianceSeverity.Medium);
        }
    }

    private void ValidateChargeBearer(MT103Message message, ComplianceValidationResult result)
    {
        // Validate charge bearer is appropriate for the transaction
        if (message.ChargeDetails?.ChargeBearer == ChargeBearer.BEN && message.Amount < 100)
        {
            result.AddWarning(
                "Field 71A",
                "BEN charge bearer on small transaction may result in beneficiary receiving very little");
        }
    }

    private static bool IsSameCustomer(OrderingCustomer? ordering, BeneficiaryCustomer? beneficiary)
    {
        if (ordering == null || beneficiary == null)
            return false;
        
        // Simple check - in production would be more sophisticated
        return !string.IsNullOrEmpty(ordering.Account) && 
               !string.IsNullOrEmpty(beneficiary.Account) &&
               ordering.Account.Equals(beneficiary.Account, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsHighRiskIndicators(string? text, string[] indicators)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        return indicators.Any(indicator => 
            text.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

/// <summary>
/// Result of compliance validation
/// </summary>
public class ComplianceValidationResult
{
    public bool IsCompliant => !Violations.Any(v => v.Severity >= ComplianceSeverity.High);
    public List<ComplianceViolation> Violations { get; } = new();
    public List<ComplianceWarning> Warnings { get; } = new();
    
    public void AddViolation(ComplianceViolationType type, string field, string description, ComplianceSeverity severity)
    {
        Violations.Add(new ComplianceViolation
        {
            Type = type,
            Field = field,
            Description = description,
            Severity = severity,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public void AddWarning(string field, string description)
    {
        Warnings.Add(new ComplianceWarning
        {
            Field = field,
            Description = description,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public void Merge(ComplianceValidationResult other)
    {
        Violations.AddRange(other.Violations);
        Warnings.AddRange(other.Warnings);
    }
    
    public string GetSummary()
    {
        var criticalCount = Violations.Count(v => v.Severity == ComplianceSeverity.Critical);
        var highCount = Violations.Count(v => v.Severity == ComplianceSeverity.High);
        var mediumCount = Violations.Count(v => v.Severity == ComplianceSeverity.Medium);
        var lowCount = Violations.Count(v => v.Severity == ComplianceSeverity.Low);
        
        return $"Compliance: {(IsCompliant ? "PASS" : "FAIL")} - " +
               $"Critical: {criticalCount}, High: {highCount}, Medium: {mediumCount}, Low: {lowCount}, Warnings: {Warnings.Count}";
    }
}

public class ComplianceViolation
{
    public ComplianceViolationType Type { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ComplianceWarning
{
    public string Field { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum ComplianceViolationType
{
    FormatViolation,
    BusinessRuleViolation,
    SanctionsViolation,
    AmountLimitViolation,
    DataQualityIssue
}

public enum ComplianceSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
