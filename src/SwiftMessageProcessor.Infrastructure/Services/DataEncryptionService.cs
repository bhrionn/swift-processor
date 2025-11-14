using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data
/// </summary>
public interface IDataEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    bool IsEncrypted(string value);
}

public class DataEncryptionService : IDataEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private const string EncryptedPrefix = "ENC:";

    public DataEncryptionService(IConfiguration configuration)
    {
        // In production, these should come from secure key management (Azure Key Vault, AWS KMS, etc.)
        var keyString = configuration["Encryption:Key"] ?? GenerateDefaultKey();
        var ivString = configuration["Encryption:IV"] ?? GenerateDefaultIV();

        _key = Convert.FromBase64String(keyString);
        _iv = Convert.FromBase64String(ivString);

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be 32 bytes (256 bits)");
        if (_iv.Length != 16)
            throw new InvalidOperationException("Encryption IV must be 16 bytes (128 bits)");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (IsEncrypted(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return EncryptedPrefix + Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        if (!IsEncrypted(cipherText))
            return cipherText;

        var actualCipherText = cipherText.Substring(EncryptedPrefix.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(actualCipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix);
    }

    private static string GenerateDefaultKey()
    {
        // Generate a random 256-bit key for development
        // In production, this should NEVER be generated - use secure key management
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    private static string GenerateDefaultIV()
    {
        // Generate a random 128-bit IV for development
        using var aes = Aes.Create();
        aes.GenerateIV();
        return Convert.ToBase64String(aes.IV);
    }
}
