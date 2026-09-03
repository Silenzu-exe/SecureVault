using System.Security.Cryptography;

namespace SecureVault.Services;

/// <summary>
/// Handles all cryptographic operations: AES-256 encryption/decryption,
/// PBKDF2 key derivation, and secure random generation.
/// Stateless — safe to use as a singleton or instantiate freely.
/// </summary>
public class EncryptionService
{
    private const int SaltSizeBytes = 16;
    private const int IvSizeBytes = 16;
    private const int KeySizeBytes = 32; // 256-bit
    private const int Pbkdf2Iterations = 100_000;

    public string GenerateSalt()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        return Convert.ToBase64String(salt);
    }

    private string GenerateIV()
    {
        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        return Convert.ToBase64String(iv);
    }

    /// <summary>
    /// Derives a 256-bit AES key from a password and salt using PBKDF2.
    /// </summary>
    public byte[] DeriveKey(string password, string base64Salt)
    {
        var salt = Convert.FromBase64String(base64Salt);
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    /// <summary>
    /// Hashes a master password for storage (never store plaintext).
    /// </summary>
    public string HashMasterPassword(string password, string base64Salt)
    {
        var hash = DeriveKey(password, base64Salt);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a password against a stored hash using constant-time comparison.
    /// </summary>
    public bool VerifyMasterPassword(string password, string base64Salt, string storedHash)
    {
        var computedHash = HashMasterPassword(password, base64Salt);
        var computedBytes = Convert.FromBase64String(computedHash);
        var storedBytes = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    /// <summary>
    /// Encrypts plaintext with AES-256-CBC using a freshly generated IV.
    /// Returns (ciphertext, IV) both Base64-encoded.
    /// </summary>
    public (string CipherText, string IV) Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(aes.IV));
    }

    /// <summary>
    /// Decrypts AES-256-CBC ciphertext using the provided key and IV.
    /// </summary>
    public string Decrypt(string cipherTextBase64, string ivBase64, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(ivBase64);

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}