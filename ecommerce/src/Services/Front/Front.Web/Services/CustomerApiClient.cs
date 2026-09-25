using Front.Web.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Front.Web.Services;

public sealed class CustomerApiClient(HttpClient httpClient)
{
    public async Task<LoginResponseDto?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers/login",
            new { email, password },
            ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LoginResponseDto>(ct);
    }

    public async Task<CustomerDto?> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers/register",
            new { firstName, lastName, email, password },
            ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(ct);
        return result is null
            ? null
            : new CustomerDto(result.Id, result.FirstName, result.LastName, result.Email, result.MustResetPassword);
    }

    public async Task<string?> RecoverPasswordAsync(string email, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers/recover",
            new { Email = email },
            ct);

        if (!response.IsSuccessStatusCode)
            return null;

        if (response.StatusCode == HttpStatusCode.NoContent)
            return string.Empty;

        var result = await response.Content.ReadFromJsonAsync<RecoveryTokenResponse>(ct);
        return result?.recoveryToken;
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers/reset",
            new { Email = email, Token = token, NewPassword = newPassword },
            ct);

        if (response.IsSuccessStatusCode)
            return (true, null);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
                return (false, content.Trim('"', '[', ']'));
        }

        return (false, "No fue posible restablecer la contraseña.");
    }

    private sealed record CustomerProfileResponse(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword);

    private sealed class RecoveryTokenResponse
    {
        [JsonPropertyName("recoveryToken")]
        public string? recoveryToken { get; set; }
    }
}
