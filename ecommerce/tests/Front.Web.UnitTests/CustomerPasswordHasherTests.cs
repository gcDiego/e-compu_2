using System.Security.Cryptography;
using System.Text;
using Front.Web.Services;

namespace Front.Web.UnitTests;

public sealed class CustomerPasswordHasherTests
{
    private readonly CustomerPasswordHasher _hasher = new();

    [Fact]
    public void Hash_CreatesAdaptiveHashThatCanBeVerified()
    {
        var hash = _hasher.Hash("Correct-Horse-42");

        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify(hash, "Correct-Horse-42"));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = _hasher.Hash("Correct-Horse-42");

        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(hash, "wrong-password"));
    }

    [Fact]
    public void Verify_AcceptsLegacySha256AndRequiresRehash()
    {
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Legacy-Password"))).ToLowerInvariant();

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify(legacyHash, "Legacy-Password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-password-hash")]
    [InlineData("pbkdf2-sha256$invalid$salt$key")]
    [InlineData("pbkdf2-sha256$210000$invalid$key")]
    public void Verify_RejectsMalformedHashes(string hash)
    {
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(hash, "password"));
    }
}
