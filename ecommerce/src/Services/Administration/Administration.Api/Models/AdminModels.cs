using System.ComponentModel.DataAnnotations;

namespace Administration.Api.Models;

public sealed record AdminProfile(int Id, string FirstName, string LastName, string Email, bool IsActive);
public sealed record AdminLoginResponse(int Id, string FirstName, string LastName, string Email, string AccessToken, DateTimeOffset ExpiresAt);
public sealed record LoginRequest([property: Required, EmailAddress, StringLength(254)] string Email = "", [property: Required, StringLength(128)] string Password = "");
public sealed record RecoverRequest([property: Required, EmailAddress, StringLength(254)] string Email = "");
public sealed record ResetRequest([property: Required, EmailAddress, StringLength(254)] string Email = "", [property: Required, StringLength(128)] string Token = "", [property: Required, StringLength(128, MinimumLength = 5)] string NewPassword = "");
public sealed record SaveAdminRequest([property: Required, StringLength(80)] string FirstName = "", [property: Required, StringLength(100)] string LastName = "", [property: Required, EmailAddress, StringLength(254)] string Email = "", bool IsActive = true);
