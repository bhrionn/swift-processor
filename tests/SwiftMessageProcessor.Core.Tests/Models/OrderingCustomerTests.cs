using FluentAssertions;
using SwiftMessageProcessor.Core.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace SwiftMessageProcessor.Core.Tests.Models;

public class OrderingCustomerTests
{
    [Fact]
    public void Validate_ValidOptionA_ReturnsSuccess()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Name = "JOHN DOE COMPANY",
            Account = "12345678901234567890",
            BIC = "DEUTDEFFXXX"
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
        customer.IsOptionA.Should().BeTrue();
    }
    
    [Fact]
    public void Validate_ValidOptionK_ReturnsSuccess()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Name = "JOHN DOE COMPANY",
            Address = "123 MAIN STREET\nANYTOWN\nCOUNTRY",
            Account = "12345678901234567890"
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success);
        customer.IsOptionA.Should().BeFalse();
    }
    
    [Fact]
    public void Validate_MissingAccountNumber_ReturnsSuccess()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Name = "JOHN DOE COMPANY",
            BIC = "DEUTDEFFXXX"
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().Be(ValidationResult.Success); // Account is now optional
    }
    
    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Account = "12345678901234567890",
            BIC = "DEUTDEFFXXX"
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Customer name is required");
    }
    
    [Fact]
    public void Validate_InvalidBIC_ReturnsError()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Name = "JOHN DOE COMPANY",
            Account = "12345678901234567890",
            BIC = "INVALID"
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Bank code (BIC) is required and must be valid");
    }
    
    [Fact]
    public void Validate_OptionKMissingAddress_ReturnsError()
    {
        // Arrange
        var customer = new OrderingCustomer
        {
            Name = "JOHN DOE COMPANY",
            Account = "12345678901234567890"
            // No BIC and no Address
        };
        
        // Act
        var result = customer.Validate();
        
        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Address is required for option K");
    }
}