using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Models;

public sealed record CustomerProfile(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(128, MinimumLength = 1)] string Password = "");

public sealed record RegisterRequest(
    [property: Required, StringLength(80, MinimumLength = 1)] string FirstName = "",
    [property: Required, StringLength(100, MinimumLength = 1)] string LastName = "",
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(15, MinimumLength = 5)] string Password = "");

public sealed record LoginResponse(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword, string AccessToken, DateTimeOffset ExpiresAt);

public sealed record RecoverRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "");

public sealed record ResetRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email = "",
    [property: Required, StringLength(128, MinimumLength = 1)] string Token = "",
    [property: Required, StringLength(128, MinimumLength = 12)] string NewPassword = "");
