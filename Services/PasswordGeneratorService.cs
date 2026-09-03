using System.Security.Cryptography;
using System.Text;

namespace SecureVault.Services;

public enum StrengthLevel { VeryWeak, Weak, Fair, Good, Strong, Excellent }

public class PasswordStrength
{
    public StrengthLevel Level { get; set; }
    public int Score { get; set; } // 0-100, for progress bars
    public double EntropyBits { get; set; }
}

/// <summary>
/// Generates cryptographically secure passwords and rates password strength
/// based on Shannon entropy given the character pool in use.
/// </summary>
public class PasswordGeneratorService
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}<>?";

    public string GeneratePassword(
        int length,
        bool useLowercase = true,
        bool useUppercase = true,
        bool useDigits = true,
        bool useSymbols = true)
    {
        var pool = new StringBuilder();
        if (useLowercase) pool.Append(Lowercase);
        if (useUppercase) pool.Append(Uppercase);
        if (useDigits) pool.Append(Digits);
        if (useSymbols) pool.Append(Symbols);

        if (pool.Length == 0)
            throw new ArgumentException("At least one character set must be selected.");

        var result = new StringBuilder(length);
        var poolString = pool.ToString();

        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(poolString.Length);
            result.Append(poolString[index]);
        }

        return result.ToString();
    }

    public double CalculateEntropy(string password)
    {
        if (string.IsNullOrEmpty(password))
            return 0;

        var poolSize = 0;
        if (password.Any(char.IsLower)) poolSize += 26;
        if (password.Any(char.IsUpper)) poolSize += 26;
        if (password.Any(char.IsDigit)) poolSize += 10;
        if (password.Any(c => Symbols.Contains(c))) poolSize += Symbols.Length;

        if (poolSize == 0)
            poolSize = 26; // fallback

        return password.Length * Math.Log2(poolSize);
    }

    public bool IsCommonPattern(string password)
    {
        var lower = password.ToLowerInvariant();
        string[] commonPatterns =
        [
            "123456", "password", "qwerty", "abc123", "letmein",
            "welcome", "admin", "111111", "12345678"
        ];
        return commonPatterns.Any(lower.Contains);
    }

    public PasswordStrength RatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrength { Level = StrengthLevel.VeryWeak, Score = 0, EntropyBits = 0 };
        }

        var entropy = CalculateEntropy(password);
        var isCommon = IsCommonPattern(password);

        var (level, score) = entropy switch
        {
            _ when isCommon => (StrengthLevel.VeryWeak, 10),
            < 28 => (StrengthLevel.Weak, 25),
            < 36 => (StrengthLevel.Fair, 45),
            < 60 => (StrengthLevel.Good, 65),
            < 80 => (StrengthLevel.Strong, 85),
            _ => (StrengthLevel.Excellent, 100)
        };

        return new PasswordStrength { Level = level, Score = score, EntropyBits = entropy };
    }
}