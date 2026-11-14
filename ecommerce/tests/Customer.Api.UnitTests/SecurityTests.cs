using Customer.Api.Models;
using Customer.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Customer.Api.UnitTests;

public sealed class SecurityTests
{
    [Fact]
    public void Pbkdf2_round_trip_succeeds()
    {
        var hasher = new CustomerPasswordHasher();
        var hash = hasher.Hash("A-secure-password-123");
        Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(hash, "A-secure-password-123"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(hash, "wrong"));
    }

    [Fact]
    public void Legacy_sha256_requests_rehash()
    {
        var hasher = new CustomerPasswordHasher();
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("legacy")));
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, hasher.Verify(legacy, "legacy"));
    }

    [Fact]
    public void UpdateProfileRequest_rejects_empty_first_name()
    {
        var model = new UpdateProfileRequest("", "Apellido");
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void UpdateProfileRequest_rejects_whitespace_names(string whitespace)
    {
        var modelFirst = new UpdateProfileRequest(whitespace, "Apellido");
        var resultsFirst = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(modelFirst, new ValidationContext(modelFirst), resultsFirst, true));

        var modelLast = new UpdateProfileRequest("Nombre", whitespace);
        var resultsLast = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(modelLast, new ValidationContext(modelLast), resultsLast, true));
    }

    [Theory]
    [InlineData("Nombre1", "Apellido")]
    [InlineData("Nombre", "Apellido;")]
    [InlineData("Robert'); DROP TABLE clientes;--", "Apellido")]
    public void UpdateProfileRequest_rejects_non_letter_names(string firstName, string lastName)
    {
        var model = new UpdateProfileRequest(firstName, lastName);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Fact]
    public void UpdateProfileRequest_accepts_valid_names()
    {
        var model = new UpdateProfileRequest("María José", "Muñoz Álvarez");
        var results = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Theory]
    [InlineData("four")]
    [InlineData("sixteen-chars-long")]
    public void ChangePasswordRequest_rejects_password_outside_length_limits(string password)
    {
        var model = new ChangePasswordRequest("current-password", password);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Theory]
    [InlineData("     ")]
    [InlineData("        ")]
    public void ChangePasswordRequest_rejects_whitespace_new_password(string whitespace)
    {
        var model = new ChangePasswordRequest("current-password", whitespace);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Fact]
    public void ChangePasswordRequest_accepts_valid_password()
    {
        var model = new ChangePasswordRequest("current-password", "valid1");
        var results = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Fact]
    public void ChangePasswordRequest_rejects_empty_current_password()
    {
        var model = new ChangePasswordRequest("", "valid1");
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }

    [Theory]
    [InlineData("   ", "Apellido", "user@example.com", "valid1")]
    [InlineData("Nombre", "   ", "user@example.com", "valid1")]
    [InlineData("Nombre", "Apellido", "user@example.com", "     ")]
    public void RegisterRequest_rejects_whitespace_fields(string firstName, string lastName, string email, string password)
    {
        var model = new RegisterRequest(firstName, lastName, email, password);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, true));
    }
}

public sealed class CustomerIndexTests
{
    [Fact]
    public void Email_and_legacy_id_are_unique()
    {
        Assert.Contains(CustomerDbInitializer.UniqueIndexes, x => x.Field == "correo");
        Assert.Contains(CustomerDbInitializer.UniqueIndexes, x => x.Field == "idSqlOriginal");
    }

    [Fact]
    public void Equivalent_unique_index_is_detected_regardless_of_name()
    {
        var indexes = new[] { MongoDB.Bson.BsonDocument.Parse("{ key: { correo: 1 }, name: 'correo_unique', unique: true }") };
        Assert.True(CustomerDbInitializer.HasEquivalentUniqueIndex(indexes, "correo"));
        Assert.False(CustomerDbInitializer.HasEquivalentUniqueIndex(indexes, "idSqlOriginal"));
    }
}
