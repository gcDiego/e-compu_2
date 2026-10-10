using Administration.Api.Models;
using Administration.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Administration.Api.UnitTests;

public sealed class SecurityTests
{
    [Fact] public void Pbkdf2_round_trip_succeeds(){var h=new AdminPasswordHasher();var hash=h.Hash("A-secure-password-123");Assert.Equal(PasswordVerificationResult.Success,h.Verify(hash,"A-secure-password-123"));Assert.Equal(PasswordVerificationResult.Failed,h.Verify(hash,"wrong"));}
    [Fact] public void Legacy_sha256_requests_rehash(){var h=new AdminPasswordHasher();var legacy=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("legacy"))).ToLowerInvariant();Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,h.Verify(legacy,"legacy"));}
    [Theory][InlineData("")][InlineData("$ne")][InlineData("not-email")] public void Invalid_email_is_rejected(string email){var model=new LoginRequest(email,"password");var results=new List<ValidationResult>();Assert.False(Validator.TryValidateObject(model,new ValidationContext(model),results,true));}
    [Fact] public void Weak_reset_password_is_rejected(){var model=new ResetRequest("admin@example.com","token","short");var results=new List<ValidationResult>();Assert.False(Validator.TryValidateObject(model,new ValidationContext(model),results,true));}
}
