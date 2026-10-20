using Administration.Api.Models;
using Administration.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;

namespace Administration.Api.UnitTests;

public sealed class SecurityTests
{
    [Fact] public void Pbkdf2_round_trip_succeeds(){var h=new AdminPasswordHasher();var hash=h.Hash("A-secure-password-123");Assert.Equal(PasswordVerificationResult.Success,h.Verify(hash,"A-secure-password-123"));Assert.Equal(PasswordVerificationResult.Failed,h.Verify(hash,"wrong"));}
    [Fact] public void Legacy_sha256_requests_rehash(){var h=new AdminPasswordHasher();var legacy=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("legacy"))).ToLowerInvariant();Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,h.Verify(legacy,"legacy"));}
    [Theory][InlineData("")][InlineData("$ne")][InlineData("not-email")] public void Invalid_email_is_rejected(string email){var model=new LoginRequest(email,"password");var results=new List<ValidationResult>();Assert.False(Validator.TryValidateObject(model,new ValidationContext(model),results,true));}
    [Fact] public void Weak_reset_password_is_rejected(){var model=new ResetRequest("admin@example.com","token","four");var results=new List<ValidationResult>();Assert.False(Validator.TryValidateObject(model,new ValidationContext(model),results,true));}
    [Fact] public void Equivalent_unique_index_is_detected_regardless_of_name(){var indexes=new[]{BsonDocument.Parse("{ key: { correo: 1 }, name: 'correo_1', unique: true }")};Assert.True(AdminDbInitializer.HasEquivalentUniqueIndex(indexes,"correo"));}
    [Fact] public void Non_unique_or_compound_index_is_not_equivalent(){var indexes=new[]{BsonDocument.Parse("{ key: { correo: 1 }, name: 'correo_1' }"),BsonDocument.Parse("{ key: { correo: 1, activo: 1 }, name: 'correo_1_activo_1', unique: true }")};Assert.False(AdminDbInitializer.HasEquivalentUniqueIndex(indexes,"correo"));}
}
