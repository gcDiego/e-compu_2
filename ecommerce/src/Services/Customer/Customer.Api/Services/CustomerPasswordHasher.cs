using System.Security.Cryptography;
using System.Text;

namespace Customer.Api.Services;

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public sealed class CustomerPasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public PasswordVerificationResult Verify(string storedHash, string password)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrEmpty(password))
            return PasswordVerificationResult.Failed;

        if (IsLegacySha256(storedHash))
        {
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var expected = Convert.FromHexString(storedHash);
            return CryptographicOperations.FixedTimeEquals(actual, expected)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return PasswordVerificationResult.Failed;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                return PasswordVerificationResult.Failed;

            return iterations < Iterations
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Success;
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static bool IsLegacySha256(string hash) =>
        hash.Length == 64 && hash.All(Uri.IsHexDigit);
}
