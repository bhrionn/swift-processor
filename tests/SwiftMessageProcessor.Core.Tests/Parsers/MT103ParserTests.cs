using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Core.Parsers;
using Xunit;

namespace SwiftMessageProcessor.Core.Tests.Parsers;

public class MT103ParserTests
{
    private readonly MT103Parser _parser;

    public MT103ParserTests()
    {
        _parser = new MT103Parser();
    }

    #region CanParse Tests

    [Fact]
    public void CanParse_WithMT103MessageType_ReturnsTrue()
    {
        // Act
        var result = _parser.CanParse("MT103");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_With103MessageType_ReturnsTrue()
    {
        // Act
        var result = _parser.CanParse("103");

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("mt103")]
    [InlineData("Mt103")]
    [InlineData("mT103")]
    public void CanParse_WithCaseInsensitiveMT103_ReturnsTrue(string messageType)
    {
        // Act
        var result = _parser.CanParse(messageType);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("MT102")]
    [InlineData("MT104")]
    [InlineData("MT200")]
    [InlineData("102")]
    [InlineData("104")]
    [InlineData("")]
    [InlineData(null)]
    public void CanParse_WithOtherMessageTypes_ReturnsFalse(string messageType)
    {
        // Act
        var result = _parser.CanParse(messageType);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ParseAsync_WithNullOrEmptyMessage_ThrowsArgumentException(string invalidMessage)
    {
        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(invalidMessage))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Raw message cannot be null or empty*");
    }

    #endregion

    #region Valid Message Parsing Tests

    [Fact]
    public async Task ParseAsync_WithValidMT103Message_ReturnsCorrectlyParsedMessage()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.MessageType.Should().Be("MT103");
        result.TransactionReference.Should().Be("REFERENCE12345");
        result.BankOperationCode.Should().Be("CRED");
        result.ValueDate.Should().Be(new DateTime(2024, 12, 15));
        result.Currency.Should().Be("EUR");
        result.Amount.Should().Be(123456.78m);
        result.OrderingCustomer.Should().NotBeNull();
        result.OrderingCustomer.Name.Should().Be("JOHN DOE");
        result.BeneficiaryCustomer.Should().NotBeNull();
        result.BeneficiaryCustomer.Name.Should().Be("JANE SMITH");
        result.RemittanceInformation.Should().Be("PAYMENT FOR INVOICE 12345");
        result.ChargeDetails.Should().NotBeNull();
        result.ChargeDetails.ChargeBearer.Should().Be(ChargeBearer.SHA);
        result.RawMessage.Should().Be(rawMessage);
        result.Status.Should().Be(MessageStatus.Pending);
        result.ReceivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ParseAsync_WithMinimalValidMessage_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMinimalValidMT103Message();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.TransactionReference.Should().Be("MIN123");
        result.BankOperationCode.Should().Be("CRED");
        result.ValueDate.Should().Be(new DateTime(2024, 1, 15));
        result.Currency.Should().Be("USD");
        result.Amount.Should().Be(1000.00m);
        result.OrderingCustomer.Should().NotBeNull();
        result.OrderingCustomer.Name.Should().Be("MINIMAL CUSTOMER");
        result.BeneficiaryCustomer.Should().NotBeNull();
        result.BeneficiaryCustomer.Name.Should().Be("BENEFICIARY NAME");
        
        // Optional fields should be null/empty
        result.OriginalCurrency.Should().BeNull();
        result.OriginalAmount.Should().BeNull();
        result.OrderingInstitution.Should().BeNull();
        result.SendersCorrespondent.Should().BeNull();
        result.IntermediaryInstitution.Should().BeNull();
        result.AccountWithInstitution.Should().BeNull();
        result.RemittanceInformation.Should().BeNull();
        result.ChargeDetails.Should().BeNull();
        result.SenderToReceiverInfo.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_WithAllOptionalFields_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithAllOptionalFields();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.TransactionReference.Should().Be("FULL123456");
        result.BankOperationCode.Should().Be("CRED");
        result.ValueDate.Should().Be(new DateTime(2024, 6, 30));
        result.Currency.Should().Be("EUR");
        result.Amount.Should().Be(50000.00m);
        
        // Optional fields should be populated
        result.OriginalCurrency.Should().Be("USD");
        result.OriginalAmount.Should().Be(55000.00m);
        result.OrderingInstitution.Should().Be("DEUTDEFF");
        result.SendersCorrespondent.Should().Be("CHASUS33");
        result.ReceiversCorrespondent.Should().Be("BNPAFRPP");
        result.IntermediaryInstitution.Should().Be("CRESCHZZ");
        result.AccountWithInstitution.Should().Be("HBUKGB4B");
        result.RemittanceInformation.Should().Be("PAYMENT FOR SERVICES\nINVOICE 2024-001");
        result.ChargeDetails.Should().NotBeNull();
        result.ChargeDetails.ChargeBearer.Should().Be(ChargeBearer.OUR);
        result.SendersCharges.Should().Be("EUR25.00");
        result.ReceiversCharges.Should().Be("EUR15.00");
        result.SenderToReceiverInfo.Should().Be("URGENT PAYMENT\nPLEASE PROCESS SAME DAY");
    }

    [Theory]
    [InlineData("USD", 1000.50)]
    [InlineData("EUR", 999999.99)]
    [InlineData("GBP", 0.01)]
    [InlineData("JPY", 1000000)]
    public async Task ParseAsync_WithDifferentCurrenciesAndAmounts_ParsesCorrectly(string currency, decimal amount)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithCurrencyAndAmount(currency, amount);

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Currency.Should().Be(currency);
        result.Amount.Should().Be(amount);
    }

    [Theory]
    [InlineData("240101", 2024, 1, 1)]
    [InlineData("241231", 2024, 12, 31)]
    [InlineData("250630", 2025, 6, 30)]
    public async Task ParseAsync_WithDifferentValueDates_ParsesCorrectly(string dateString, int year, int month, int day)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithValueDate(dateString);

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.ValueDate.Should().Be(new DateTime(year, month, day));
    }

    [Fact]
    public async Task ParseAsync_WithField50A_ParsesOrderingCustomerWithBIC()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithField50A();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.OrderingCustomer.Should().NotBeNull();
        result.OrderingCustomer.Account.Should().Be("12345678901234567890");
        result.OrderingCustomer.BIC.Should().Be("DEUTDEFF");
        result.OrderingCustomer.Name.Should().BeEmpty(); // BIC format doesn't include name
    }

    [Fact]
    public async Task ParseAsync_WithField59A_ParsesBeneficiaryCustomerWithBIC()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithField59A();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.BeneficiaryCustomer.Should().NotBeNull();
        result.BeneficiaryCustomer.Account.Should().Be("98765432109876543210");
        result.BeneficiaryCustomer.BIC.Should().Be("CHASUS33");
        result.BeneficiaryCustomer.Name.Should().BeEmpty(); // BIC format doesn't include name
    }

    [Theory]
    [InlineData("SHA")]
    [InlineData("OUR")]
    [InlineData("BEN")]
    public async Task ParseAsync_WithDifferentChargeBearers_ParsesCorrectly(string chargeBearer)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithChargeBearer(chargeBearer);

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.ChargeDetails.Should().NotBeNull();
        result.ChargeDetails.ChargeBearer.Should().Be(Enum.Parse<ChargeBearer>(chargeBearer));
    }

    #endregion

    #region Invalid Message Handling Tests

    [Theory]
    [InlineData("INVALID MESSAGE FORMAT")]
    [InlineData("This is not a SWIFT message")]
    [InlineData("Random text without proper structure")]
    [InlineData("{1:INVALID}")]
    public async Task ParseAsync_WithInvalidMessageFormat_ThrowsSwiftParsingException(string invalidMessage)
    {
        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(invalidMessage))
            .Should().ThrowAsync<SwiftParsingException>();
    }

    [Fact]
    public async Task ParseAsync_WithMissingBlock1_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = @"{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:50K:JOHN DOE
:59:JANE SMITH
-}";

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*Block 1*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingBlock2_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = @"{1:F01DEUTDEFF0123456789012345}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:50K:JOHN DOE
:59:JANE SMITH
-}";

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*Block 2*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingBlock4_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}";

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*Block 4*");
    }

    [Fact]
    public async Task ParseAsync_WithNonMT103MessageType_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = @"{1:F01DEUTDEFF0123456789012345}
{2:I102CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:50K:JOHN DOE
:59:JANE SMITH
-}";

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*MT103*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingField20_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = GetMT103MessageMissingField20();

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*field 20*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingField23B_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = GetMT103MessageMissingField23B();

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*field 23B*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingField32A_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = GetMT103MessageMissingField32A();

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*field 32A*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingField50_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = GetMT103MessageMissingField50();

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*field 50*");
    }

    [Fact]
    public async Task ParseAsync_WithMissingField59_ThrowsSwiftParsingException()
    {
        // Arrange
        var rawMessage = GetMT103MessageMissingField59();

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*field 59*");
    }

    [Theory]
    [InlineData("241301EUR123456,78")] // Invalid month
    [InlineData("240230EUR123456,78")] // Invalid day for February
    [InlineData("24ABCDEUR123456,78")] // Non-numeric date
    public async Task ParseAsync_WithInvalidDateInField32A_ThrowsSwiftParsingException(string invalidField32A)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithCustomField32A(invalidField32A);

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*date*");
    }

    [Theory]
    [InlineData("241215EURXXX")] // Non-numeric amount
    [InlineData("241215EUR")] // Missing amount
    [InlineData("241215EUR-123.45")] // Negative amount
    public async Task ParseAsync_WithInvalidAmountInField32A_ThrowsSwiftParsingException(string invalidField32A)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithCustomField32A(invalidField32A);

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>()
            .WithMessage("*amount*");
    }

    [Theory]
    [InlineData("INVALID")] // Invalid bank operation code
    [InlineData("CR")] // Too short
    [InlineData("CREDIT")] // Too long
    public async Task ParseAsync_WithInvalidBankOperationCode_ThrowsSwiftParsingException(string invalidCode)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithBankOperationCode(invalidCode);

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(rawMessage))
            .Should().ThrowAsync<SwiftParsingException>();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateAsync_WithValidMessage_ReturnsSuccess()
    {
        // Arrange
        var message = CreateValidMT103Message();

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public async Task ValidateAsync_WithNullMessage_ReturnsValidationError()
    {
        // Act
        var result = await _parser.ValidateAsync(null!);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Be("Message cannot be null");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidTransactionReference_ReturnsValidationError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.TransactionReference = ""; // Invalid - empty

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Transaction reference");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidCurrency_ReturnsValidationError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Currency = "XX"; // Invalid - not 3 characters

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Currency");
    }

    [Fact]
    public async Task ValidateAsync_WithZeroAmount_ReturnsValidationError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.Amount = 0; // Invalid - zero amount

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Amount");
    }

    [Fact]
    public async Task ValidateAsync_WithNullOrderingCustomer_ReturnsValidationError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.OrderingCustomer = null!; // Invalid - null

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Ordering customer");
    }

    [Fact]
    public async Task ValidateAsync_WithNullBeneficiaryCustomer_ReturnsValidationError()
    {
        // Arrange
        var message = CreateValidMT103Message();
        message.BeneficiaryCustomer = null!; // Invalid - null

        // Act
        var result = await _parser.ValidateAsync(message);

        // Assert
        result.Should().NotBe(ValidationResult.Success);
        result.ErrorMessage.Should().Contain("Beneficiary customer");
    }

    #endregion

    #region Field Extraction Accuracy Tests

    [Fact]
    public async Task ParseAsync_WithOptionalFields_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithOptionalFields();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Should().NotBeNull();
        result.OriginalCurrency.Should().Be("USD");
        result.OriginalAmount.Should().Be(150000.00m);
        result.OrderingInstitution.Should().Be("DEUTDEFF");
        result.SendersCorrespondent.Should().Be("CHASUS33");
        result.IntermediaryInstitution.Should().Be("BNPAFRPP");
        result.AccountWithInstitution.Should().Be("CRESCHZZ");
        result.SenderToReceiverInfo.Should().Be("ADDITIONAL INSTRUCTIONS");
    }

    [Fact]
    public async Task ParseAsync_ExtractsFieldsIntoDictionary()
    {
        // Arrange
        var rawMessage = GetValidMT103Message();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.Fields.Should().NotBeEmpty();
        result.Fields.Should().ContainKey("20");
        result.Fields["20"].Should().Be("REFERENCE12345");
        result.Fields.Should().ContainKey("23B");
        result.Fields["23B"].Should().Be("CRED");
        result.Fields.Should().ContainKey("32A");
        result.Fields["32A"].Should().Be("241215EUR123456,78");
        result.Fields.Should().ContainKey("70");
        result.Fields["70"].Should().Be("PAYMENT FOR INVOICE 12345");
        result.Fields.Should().ContainKey("71A");
        result.Fields["71A"].Should().Be("SHA");
    }

    [Fact]
    public async Task ParseAsync_WithMultilineRemittanceInfo_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithMultilineRemittanceInfo();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.RemittanceInformation.Should().Be("PAYMENT FOR SERVICES\nINVOICE NUMBER 2024-001\nDUE DATE 2024-12-31");
    }

    [Fact]
    public async Task ParseAsync_WithMultilineSenderToReceiverInfo_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithMultilineSenderToReceiverInfo();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.SenderToReceiverInfo.Should().Be("URGENT PAYMENT REQUIRED\nPLEASE PROCESS SAME DAY\nCONTACT IF ISSUES");
    }

    [Fact]
    public async Task ParseAsync_WithComplexOrderingCustomer_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithComplexOrderingCustomer();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.OrderingCustomer.Should().NotBeNull();
        result.OrderingCustomer.Account.Should().Be("12345678901234567890");
        result.OrderingCustomer.Name.Should().Be("JOHN DOE ENTERPRISES LTD");
        result.OrderingCustomer.Address.Should().Be("123 MAIN STREET\nSUITE 456\nNEW YORK NY 10001\nUSA");
    }

    [Fact]
    public async Task ParseAsync_WithComplexBeneficiaryCustomer_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = GetMT103MessageWithComplexBeneficiaryCustomer();

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.BeneficiaryCustomer.Should().NotBeNull();
        result.BeneficiaryCustomer.Account.Should().Be("98765432109876543210");
        result.BeneficiaryCustomer.Name.Should().Be("JANE SMITH TRADING COMPANY");
        result.BeneficiaryCustomer.Address.Should().Be("456 OAK AVENUE\nFLOOR 2\nLONDON EC1A 1BB\nUNITED KINGDOM");
    }

    [Theory]
    [InlineData("EUR25.00", "EUR", "25.00")]
    [InlineData("USD100.50", "USD", "100.50")]
    [InlineData("GBP15.75", "GBP", "15.75")]
    public async Task ParseAsync_WithSendersCharges_ParsesCorrectly(string chargesField, string expectedCurrency, string expectedAmount)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithSendersCharges(chargesField);

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.SendersCharges.Should().Be($"{expectedCurrency}{expectedAmount}");
    }

    [Theory]
    [InlineData("EUR10.00", "EUR", "10.00")]
    [InlineData("USD20.25", "USD", "20.25")]
    [InlineData("GBP5.50", "GBP", "5.50")]
    public async Task ParseAsync_WithReceiversCharges_ParsesCorrectly(string chargesField, string expectedCurrency, string expectedAmount)
    {
        // Arrange
        var rawMessage = GetMT103MessageWithReceiversCharges(chargesField);

        // Act
        var result = await _parser.ParseAsync(rawMessage);

        // Assert
        result.ReceiversCharges.Should().Be($"{expectedCurrency}{expectedAmount}");
    }

    [Fact]
    public async Task ParseAsync_WithDifferentLineEndings_ParsesCorrectly()
    {
        // Arrange - Test with different line ending formats
        var rawMessageWithCRLF = GetValidMT103Message().Replace("\n", "\r\n");
        var rawMessageWithCR = GetValidMT103Message().Replace("\n", "\r");

        // Act
        var resultCRLF = await _parser.ParseAsync(rawMessageWithCRLF);
        var resultCR = await _parser.ParseAsync(rawMessageWithCR);

        // Assert
        resultCRLF.TransactionReference.Should().Be("REFERENCE12345");
        resultCR.TransactionReference.Should().Be("REFERENCE12345");
        resultCRLF.OrderingCustomer.Name.Should().Be("JOHN DOE");
        resultCR.OrderingCustomer.Name.Should().Be("JOHN DOE");
    }

    #endregion

    #region Test Data Helper Methods

    private static string GetValidMT103Message()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:50K:/12345678901234567890
JOHN DOE
123 MAIN STREET
NEW YORK NY 10001
:59:/98765432109876543210
JANE SMITH
456 OAK AVENUE
LONDON EC1A 1BB
:70:PAYMENT FOR INVOICE 12345
:71A:SHA
-}";
    }

    private static string GetMinimalValidMT103Message()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:MIN123
:23B:CRED
:32A:240115USD1000,00
:50K:MINIMAL CUSTOMER
:59:BENEFICIARY NAME
-}";
    }

    private static string GetMT103MessageWithAllOptionalFields()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:FULL123456
:23B:CRED
:32A:240630EUR50000,00
:33B:USD55000,00
:50K:/12345678901234567890
ORDERING CUSTOMER NAME
123 CUSTOMER STREET
CUSTOMER CITY
:52A:DEUTDEFF
:53A:CHASUS33
:54A:BNPAFRPP
:56A:CRESCHZZ
:57A:HBUKGB4B
:59:/98765432109876543210
BENEFICIARY CUSTOMER NAME
456 BENEFICIARY AVENUE
BENEFICIARY CITY
:70:PAYMENT FOR SERVICES
INVOICE 2024-001
:71A:OUR
:71F:EUR25,00
:71G:EUR15,00
:72:URGENT PAYMENT
PLEASE PROCESS SAME DAY
-}";
    }

    private static string GetMT103MessageWithCurrencyAndAmount(string currency, decimal amount)
    {
        var amountString = amount.ToString("F2").Replace(".", ",");
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:241215{currency}{amountString}
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
-}}";
    }

    private static string GetMT103MessageWithValueDate(string dateString)
    {
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:{dateString}EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
-}}";
    }

    private static string GetMT103MessageWithField50A()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50A:/12345678901234567890
DEUTDEFF
:59:TEST BENEFICIARY
-}";
    }

    private static string GetMT103MessageWithField59A()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59A:/98765432109876543210
CHASUS33
-}";
    }

    private static string GetMT103MessageWithChargeBearer(string chargeBearer)
    {
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
:71A:{chargeBearer}
-}}";
    }

    private static string GetMT103MessageMissingField20()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:23B:CRED
:32A:241215EUR123456,78
:50K:/12345678901234567890
JOHN DOE
123 MAIN STREET
NEW YORK NY 10001
:59:/98765432109876543210
JANE SMITH
456 OAK AVENUE
LONDON EC1A 1BB
:70:PAYMENT FOR INVOICE 12345
:71A:SHA
-}";
    }

    private static string GetMT103MessageMissingField23B()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:32A:241215EUR123456,78
:50K:JOHN DOE
:59:JANE SMITH
-}";
    }

    private static string GetMT103MessageMissingField32A()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:50K:JOHN DOE
:59:JANE SMITH
-}";
    }

    private static string GetMT103MessageMissingField50()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:59:JANE SMITH
-}";
    }

    private static string GetMT103MessageMissingField59()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:50K:JOHN DOE
-}";
    }

    private static string GetMT103MessageWithCustomField32A(string field32AValue)
    {
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:{field32AValue}
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
-}}";
    }

    private static string GetMT103MessageWithBankOperationCode(string operationCode)
    {
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:{operationCode}
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
-}}";
    }

    private static string GetMT103MessageWithOptionalFields()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:REFERENCE12345
:23B:CRED
:32A:241215EUR123456,78
:33B:USD150000,00
:50K:/12345678901234567890
JOHN DOE
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
:72:ADDITIONAL INSTRUCTIONS
-}";
    }

    private static string GetMT103MessageWithMultilineRemittanceInfo()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
:70:PAYMENT FOR SERVICES
INVOICE NUMBER 2024-001
DUE DATE 2024-12-31
-}";
    }

    private static string GetMT103MessageWithMultilineSenderToReceiverInfo()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
:72:URGENT PAYMENT REQUIRED
PLEASE PROCESS SAME DAY
CONTACT IF ISSUES
-}";
    }

    private static string GetMT103MessageWithComplexOrderingCustomer()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:/12345678901234567890
JOHN DOE ENTERPRISES LTD
123 MAIN STREET
SUITE 456
NEW YORK NY 10001
USA
:59:TEST BENEFICIARY
-}";
    }

    private static string GetMT103MessageWithComplexBeneficiaryCustomer()
    {
        return @"{1:F01DEUTDEFF0123456789012345}
{2:I103CHASUS33XXXXN}
{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:/98765432109876543210
JANE SMITH TRADING COMPANY
456 OAK AVENUE
FLOOR 2
LONDON EC1A 1BB
UNITED KINGDOM
-}";
    }

    private static string GetMT103MessageWithSendersCharges(string chargesField)
    {
        var currency = chargesField.Substring(0, 3);
        var amount = chargesField.Substring(3);
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
:71F:{currency}{amount.Replace(".", ",")}
-}}";
    }

    private static string GetMT103MessageWithReceiversCharges(string chargesField)
    {
        var currency = chargesField.Substring(0, 3);
        var amount = chargesField.Substring(3);
        return @$"{{1:F01DEUTDEFF0123456789012345}}
{{2:I103CHASUS33XXXXN}}
{{4:
:20:TEST123
:23B:CRED
:32A:241215EUR1000,00
:50K:TEST CUSTOMER
:59:TEST BENEFICIARY
:71G:{currency}{amount.Replace(".", ",")}
-}}";
    }

    private static MT103Message CreateValidMT103Message()
    {
        return new MT103Message
        {
            TransactionReference = "REFERENCE12345",
            BankOperationCode = "CRED",
            ValueDate = new DateTime(2024, 12, 15),
            Currency = "EUR",
            Amount = 123456.78m,
            OrderingCustomer = new OrderingCustomer
            {
                Account = "12345678901234567890",
                Name = "JOHN DOE",
                Address = "123 MAIN STREET\nNEW YORK NY 10001"
            },
            BeneficiaryCustomer = new BeneficiaryCustomer
            {
                Account = "98765432109876543210",
                Name = "JANE SMITH",
                Address = "456 OAK AVENUE\nLONDON EC1A 1BB"
            },
            RemittanceInformation = "PAYMENT FOR INVOICE 12345",
            ChargeDetails = new ChargeDetails { ChargeBearer = ChargeBearer.SHA }
        };
    }

    #endregion
}