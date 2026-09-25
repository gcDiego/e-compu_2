using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Models;

public sealed record CustomerProfile(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email = "",
    [Required, StringLength(128, MinimumLength = 1)] string Password = "");

public sealed record RegisterRequest(
    [Required, StringLength(80, MinimumLength = 1)] string FirstName = "",
    [Required, StringLength(100, MinimumLength = 1)] string LastName = "",
    [Required, EmailAddress, StringLength(254)] string Email = "",
    [Required, StringLength(128, MinimumLength = 12)] string Password = "");

public sealed record LoginResponse(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword, string AccessToken, DateTimeOffset ExpiresAt);

public sealed record RecoverRequest(
    [Required, EmailAddress, StringLength(254)] string Email = "");

public sealed record ResetRequest(
    [Required, EmailAddress, StringLength(254)] string Email = "",
    [Required, StringLength(128, MinimumLength = 1)] string Token = "",
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword = "");
