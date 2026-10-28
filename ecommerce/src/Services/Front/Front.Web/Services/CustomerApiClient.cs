using Front.Web.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Front.Web.Services;

public sealed class CustomerApiClient(HttpClient httpClient)
{
    public async Task<(LoginResponseDto? Response, bool IsLocked, string? Error)> LoginAsync(string email, string password, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers/login",
            new { email, password },
            ct);

        if (response.StatusCode == HttpStatusCode.Locked)
            return (null, true, "La cuenta está bloqueada temporalmente por exceso de intentos fallidos. Intenta dentro de 15 minutos.");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (null, false, "Correo o contraseña incorrectos.");

        if (!response.IsSuccessStatusCode)
            return (null, false, "No fue posible iniciar sesión.");

        var data = await response.Content.ReadFromJsonAsync<LoginResponseDto>(ct);
        return (data, false, null);
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

    public async Task<CustomerDto?> GetProfileAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/customers/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(ct);
        return result is null
            ? null
            : new CustomerDto(result.Id, result.FirstName, result.LastName, result.Email, result.MustResetPassword);
    }

    public async Task<(CustomerDto? Customer, string? Error)> UpdateProfileAsync(string token, string firstName, string lastName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/customers/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { firstName, lastName });

        var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(ct);
            return result is null
                ? (null, "No fue posible actualizar el perfil.")
                : (new CustomerDto(result.Id, result.FirstName, result.LastName, result.Email, result.MustResetPassword), null);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
                return (null, content.Trim('"', '[', ']'));
        }

        return (null, "No fue posible actualizar el perfil.");
    }

    public async Task<(bool Success, string? NewToken, string? Error)> ChangePasswordAsync(string token, string currentPassword, string newPassword, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/customers/change-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { currentPassword, newPassword });

        var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<ChangePasswordTokenResponse>(cancellationToken: ct);
                return (true, body?.AccessToken, null);
            }
            catch
            {
                return (true, null, null);
            }
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
            return (false, null, "La contraseña fue modificada concurrentemente. Intenta de nuevo.");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (false, null, "Sesión vencida. Vuelve a iniciar sesión.");

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
                return (false, null, content.Trim('"', '[', ']'));
        }

        return (false, null, "No fue posible cambiar la contraseña.");
    }

    private sealed record CustomerProfileResponse(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword);
    private sealed record ChangePasswordTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed class RecoveryTokenResponse
    {
        [JsonPropertyName("recoveryToken")]
        public string? recoveryToken { get; set; }
    }
}
