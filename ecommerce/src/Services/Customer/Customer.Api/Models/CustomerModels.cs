using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Models;

public sealed record CustomerProfile(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(128, MinimumLength = 1)] string Password = "");

public sealed record RegisterRequest(
    [property: Required, StringLength(80, MinimumLength = 1), RegularExpression(@"^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)*$", ErrorMessage = "Solo se permiten letras y espacios.")] string FirstName = "",
    [property: Required, StringLength(100, MinimumLength = 1), RegularExpression(@"^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)*$", ErrorMessage = "Solo se permiten letras y espacios.")] string LastName = "",
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(15, MinimumLength = 5)] string Password = "") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return new ValidationResult("El nombre no puede estar vacío.", [nameof(FirstName)]);
        if (string.IsNullOrWhiteSpace(LastName))
            yield return new ValidationResult("El apellido no puede estar vacío.", [nameof(LastName)]);
        if (string.IsNullOrWhiteSpace(Password))
            yield return new ValidationResult("La contraseña no puede contener solo espacios.", [nameof(Password)]);
    }
}

public sealed record LoginResponse(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword, string AccessToken, DateTimeOffset ExpiresAt);

public sealed record RecoverRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "");

public sealed record ResetRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(128, MinimumLength = 1)] string Token = "",
    [property: Required, StringLength(128, MinimumLength = 12)] string NewPassword = "");

public sealed record UpdateProfileRequest(
    [property: Required, StringLength(80, MinimumLength = 1), RegularExpression(@"^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)*$", ErrorMessage = "Solo se permiten letras y espacios.")] string FirstName = "",
    [property: Required, StringLength(100, MinimumLength = 1), RegularExpression(@"^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)*$", ErrorMessage = "Solo se permiten letras y espacios.")] string LastName = "") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return new ValidationResult("El nombre no puede estar vacío.", [nameof(FirstName)]);
        if (string.IsNullOrWhiteSpace(LastName))
            yield return new ValidationResult("El apellido no puede estar vacío.", [nameof(LastName)]);
    }
}

public sealed record ChangePasswordRequest(
    [property: Required, StringLength(128, MinimumLength = 1)] string CurrentPassword = "",
    [property: Required, StringLength(15, MinimumLength = 5)] string NewPassword = "") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
            yield return new ValidationResult("La nueva contraseña no puede contener solo espacios.", [nameof(NewPassword)]);
    }
}

public enum CustomerAuthStatus
{
    Success,
    InvalidCredentials,
    Locked
}

public sealed record CustomerAuthResult(CustomerAuthStatus Status, CustomerProfile? Customer)
{
    public static CustomerAuthResult Success(CustomerProfile customer) => new(CustomerAuthStatus.Success, customer);
    public static CustomerAuthResult Locked() => new(CustomerAuthStatus.Locked, null);
    public static CustomerAuthResult Failed() => new(CustomerAuthStatus.InvalidCredentials, null);
}

public enum ChangePasswordStatus
{
    Success,
    NotFound,
    InvalidCurrentPassword,
    ConcurrencyConflict
}

public sealed record ChangePasswordResult(ChangePasswordStatus Status, CustomerProfile? UpdatedProfile = null);
public sealed record ChangePasswordResponse(string AccessToken, DateTimeOffset ExpiresAt);
