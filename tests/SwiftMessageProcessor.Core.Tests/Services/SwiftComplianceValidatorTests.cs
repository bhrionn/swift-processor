using Microsoft.Extensions.Logging;
using NSubstitute;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Services;
using Xunit;

namespace SwiftMessageProcessor.Core.Tests.Services;

public class SwiftComplianceValidatorTests
{
    private readonly ISwiftComplianceValidator _validator;
    private readonly ILogger<SwiftComplianceValidator> _logger;

    public SwiftComplianceValidatorTests()
    {
        _logger = Substitute.For<ILogger<SwiftComplianceValidator>>();
        _validator = new SwiftComplianceValidator(_logger);
    }

    [Fact]
    public async Task ValidateComplianceAsync_ValidMessage_ReturnsCompliant()
    {
        // Arrange
        var message = CreateValidMT103Message();

        // Act
        var result = await _validator.ValidateComplianceAsync(message);

        // Assert
        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations.Where(v => v.Severity >= ComplianceSeverity.High));
    }

    [Fact]
    public async Task ValidateBusinessRulesAsync_SameOrderingAndBeneficiary_AddsViolation()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.OrderingCustomer.Account = "12345678";
        message.BeneficiaryCustomer.Account = "12345678";

        // Act
        var result = await _validator.ValidateBusinessRulesAsync(message);

        // Assert
        Assert.Contains(result.Violations, v => v.Type == ComplianceViolationType.BusinessRuleViolation);
    }

    [Fact]
    public async Task ValidateAmountLimitsAsync_ExcessiveAmount_AddsViolation()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Amount = 15_000_000m; // Exceeds 10 million limit

        // Act
        var result = await _validator.ValidateAmountLimitsAsync(message);

        // Assert
        Assert.Contains(result.Violations, v => 
            v.Type == ComplianceViolationType.AmountLimitViolation &&
            v.Severity == ComplianceSeverity.Critical);
    }

    [Fact]
    public async Task ValidateAmountLimitsAsync_HighValueTransaction_AddsWarning()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Amount = 1_500_000m; // Above 1 million threshold

        // Act
        var result = await _validator.ValidateAmountLimitsAsync(message);

        // Assert
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Field == "Field 32A");
    }

    [Fact]
    public async Task ValidateSanctionsAsync_HighRiskIndicators_AddsViolation()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.BeneficiaryCustomer.Name = "SANCTIONED ENTITY";

        // Act
        var result = await _validator.ValidateSanctionsAsync(message);

        // Assert
        Assert.Contains(result.Violations, v => 
            v.Type == ComplianceViolationType.SanctionsViolation &&
            v.Severity == ComplianceSeverity.Critical);
    }

    private static MT103Message CreateValidMT103Message()
    {
        return new MT103Message
        {
            TransactionReference = "REF123456",
            BankOperationCode = "CRED",
            ValueDate = DateTime.Today,
            Currency = "USD",
            Amount = 50000m,
            OrderingCustomer = new OrderingCustomer
            {
                Account = "ACC001",
                Name = "John Doe",
                Address = "123 Main St"
            },
            BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Account = "ACC002",
                Name = "Jane Smith",
                Address = "456 Oak Ave"
            },
            RemittanceInformation = "Payment for services"
        };
    }
}
