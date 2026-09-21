using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CapaEntidad;
using Newtonsoft.Json;

namespace CapaPresentacionTienda
{
    public class IdentityApiClient
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public async Task<IdentityLoginResult> LoginCustomerAsync(string email, string password)
        {
            EnsureConfigured();
            var body = JsonConvert.SerializeObject(new
            {
                email = email,
                password = password,
                accountType = "Customer"
            });
            var response = await Client.PostAsync(
                "api/v1/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"));
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return null;

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<LoginResponse>(json);
            if (dto == null || string.IsNullOrWhiteSpace(dto.AccessToken))
                throw new InvalidOperationException("Identity Service devolvió una respuesta de login inválida.");

            return new IdentityLoginResult
            {
                Cliente = new Cliente
                {
                    IdCliente = dto.Id,
                    Nombres = dto.FirstName,
                    Apellidos = dto.LastName,
                    Correo = dto.Email,
                    Restablecer = dto.MustResetPassword
                },
                AccessToken = dto.AccessToken,
                ExpiresAt = dto.ExpiresAt
            };
        }

        private static void EnsureConfigured()
        {
            if (Client.BaseAddress != null)
                return;

            var baseUrl = ConfigurationManager.AppSettings["IdentityApi:BaseUrl"];
            Uri uri;
            if (string.IsNullOrWhiteSpace(baseUrl) ||
                !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ConfigurationErrorsException(
                    "IdentityApi:BaseUrl debe contener una URL HTTP o HTTPS absoluta cuando Features:UseIdentityApi está habilitado.");
            }

            Client.BaseAddress = uri;
        }

        private class LoginResponse
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("firstName")] public string FirstName { get; set; }
            [JsonProperty("lastName")] public string LastName { get; set; }
            [JsonProperty("email")] public string Email { get; set; }
            [JsonProperty("mustResetPassword")] public bool MustResetPassword { get; set; }
            [JsonProperty("accessToken")] public string AccessToken { get; set; }
            [JsonProperty("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
        }
    }

    public class IdentityLoginResult
    {
        public Cliente Cliente { get; set; }
        public string AccessToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
