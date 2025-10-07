using FluentAssertions;
using SwiftMessageProcessor.Core.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace SwiftMessageProcessor.Core.Tests.Models;

public class ChargeDetailsTests
{
    [Theory]
    [InlineData("BEN")]
    [InlineData("OUR")]
    [InlineData("SHA")]
    public void Validate_ValidChargeCode_ReturnsSuccess(string chargeCode)
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = Enum.Parse<ChargeBearer>(chargeCode)
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
    }
    
    [Fact]
    public void Validate_ValidChargeBearer_ReturnsSuccess()
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = ChargeBearer.SHA
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
    }
    
    [Fact]
    public void Validate_ValidChargeAmountAndCurrency_ReturnsSuccess()
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = ChargeBearer.SHA,
            ChargeAmount = 25.50m,
            ChargeCurrency = "EUR"
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
    }
    
    [Fact]
    public void Validate_ChargeAmountWithoutCurrency_ReturnsError()
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = ChargeBearer.SHA,
            ChargeAmount = 25.50m
            // Missing ChargeCurrency
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Charge currency is required when charge amount is specified");
    }
    
    [Fact]
    public void Validate_InvalidChargeAmount_ReturnsError()
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = ChargeBearer.SHA,
            ChargeAmount = -10.00m,
            ChargeCurrency = "EUR"
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Charge amount must be greater than zero");
    }
    
    [Fact]
    public void Validate_InvalidChargeCurrency_ReturnsError()
    {
        // Arrange
        var chargeDetails = new ChargeDetails
        {
            ChargeBearer = ChargeBearer.SHA,
            ChargeAmount = 25.50m,
            ChargeCurrency = "INVALID"
        };
        
        // Act
        var result = chargeDetails.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Charge currency must be a valid 3-letter ISO 4217 code");
    }
}