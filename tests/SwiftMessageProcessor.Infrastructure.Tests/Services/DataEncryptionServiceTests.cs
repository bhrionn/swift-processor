using Microsoft.Extensions.Configuration;
using SwiftMessageProcessor.Infrastructure.Services;
using Xunit;

namespace SwiftMessageProcessor.Infrastructure.Tests.Services;

public class DataEncryptionServiceTests
{
    private readonly IDataEncryptionService _encryptionService;

    public DataEncryptionServiceTests()
    {
        // Generate valid 32-byte key and 16-byte IV for testing
        var key = new byte[32];
        var iv = new byte[16];
        for (int i = 0; i < 32; i++) key[i] = (byte)i;
        for (int i = 0; i < 16; i++) iv[i] = (byte)i;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(key),
                ["Encryption:IV"] = Convert.ToBase64String(iv)
            })
            .Build();

        _encryptionService = new DataEncryptionService(configuration);
    }

    [Fact]
    public void Encrypt_ValidPlainText_ReturnsEncryptedText()
    {
        // Arrange
        var plainText = "Sensitive financial data";

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        Assert.StartsWith("ENC:", encrypted);
    }

    [Fact]
    public void Decrypt_EncryptedText_ReturnsOriginalPlainText()
    {
        // Arrange
        var plainText = "Sensitive financial data";
        var encrypted = _encryptionService.Encrypt(plainText);

        // Act
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var plainText = string.Empty;

        // Act
        var encrypted = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.Equal(string.Empty, encrypted);
    }

    [Fact]
    public void IsEncrypted_EncryptedText_ReturnsTrue()
    {
        // Arrange
        var plainText = "Test data";
        var encrypted = _encryptionService.Encrypt(plainText);

        // Act
        var isEncrypted = _encryptionService.IsEncrypted(encrypted);

        // Assert
        Assert.True(isEncrypted);
    }

    [Fact]
    public void IsEncrypted_PlainText_ReturnsFalse()
    {
        // Arrange
        var plainText = "Test data";

        // Act
        var isEncrypted = _encryptionService.IsEncrypted(plainText);

        // Assert
        Assert.False(isEncrypted);
    }

    [Fact]
    public void Encrypt_AlreadyEncrypted_ReturnsUnchanged()
    {
        // Arrange
        var plainText = "Test data";
        var encrypted = _encryptionService.Encrypt(plainText);

        // Act
        var reEncrypted = _encryptionService.Encrypt(encrypted);

        // Assert
        Assert.Equal(encrypted, reEncrypted);
    }
}
