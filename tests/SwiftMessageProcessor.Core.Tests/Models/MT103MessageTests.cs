using FluentAssertions;
using SwiftMessageProcessor.Core.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace SwiftMessageProcessor.Core.Tests.Models;

public class MT103MessageTests
{
    [Fact]
    public void Validate_ValidMT103Message_ReturnsSuccess()
    {
        // Arrange
        var message = CreateValidMT103Message();
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
    }
    
    [Fact]
    public void Validate_MissingTransactionReference_ReturnsError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.TransactionReference = "";
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Transaction reference");
    }
    
    [Fact]
    public void Validate_InvalidCurrency_ReturnsError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Currency = "INVALID";
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Currency code");
    }
    
    [Fact]
    public void Validate_InvalidAmount_ReturnsError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Amount = -100;
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Amount must be greater than zero");
    }
    
    [Fact]
    public void Validate_InvalidBankOperationCode_ReturnsError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.BankOperationCode = "INVALID";
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Bank operation code");
    }
    
    [Fact]
    public void Validate_OptionalFieldsWithValidValues_ReturnsSuccess()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.OriginalCurrency = "GBP";
        message.OriginalAmount = 1500.00m;
        message.OrderingInstitution = "DEUTDEFFXXX";
        message.RemittanceInformation = "Payment for invoice 12345";
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
    }
    
    [Fact]
    public void Validate_OriginalCurrencySameAsSettlement_ReturnsError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Currency = "EUR";
        message.OriginalCurrency = "EUR"; // Same as settlement currency
        message.OriginalAmount = 1000.00m;
        
        // Act
        var result = message.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Original currency should be different from settlement currency");
    }
    
    private static MT103Message CreateValidMT103Message()
    {
        return new MT103Message
        {
            TransactionReference = "REF123456789",
            BankOperationCode = "CRED",
            ValueDate = DateTime.Today,
            Currency = "EUR",
            Amount = 1000.50m,
            OrderingCustomer = new OrderingCustomer
            {
                Name = "JOHN DOE",
                Address = "123 MAIN STREET\nANYTOWN",
                Account = "12345678901234567890"
            },
            BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Name = "JANE SMITH",
                Address = "456 OAK AVENUE\nOTHERTOWN",
                Account = "09876543210987654321"
            },
            ChargeDetails = new ChargeDetails
            {
                ChargeBearer = ChargeBearer.SHA
            }
        };
    }
}