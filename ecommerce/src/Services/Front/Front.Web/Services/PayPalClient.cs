using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Front.Web.Services;

public sealed class PayPalClient(IHttpClientFactory factory, IConfiguration config, ILogger<PayPalClient> logger)
{
    private readonly string _clientId = config["PayPal:ClientId"] ?? "";
    private readonly string _clientSecret = config["PayPal:ClientSecret"] ?? "";
    private readonly string _baseUrl = (config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com").TrimEnd('/');

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);
    public string ClientId => _clientId;

    private HttpClient CreateClient() => factory.CreateClient("PayPal");

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!IsConfigured) return null;

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("No se pudo obtener token de PayPal Sandbox: {StatusCode} {Body}", response.StatusCode, body);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
        return json?["access_token"]?.GetValue<string>();
    }

    public async Task<(string? OrderId, string? Error)> CreateOrderAsync(decimal amount, string currency, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            // Modo demo / simulación cuando aún no se ingresan credenciales de PayPal Developer
            var demoId = $"DEMO-PAYPAL-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
            logger.LogInformation("PayPal no configurado. Creando orden Sandbox Demo {DemoId}.", demoId);
            return (demoId, null);
        }

        var token = await GetAccessTokenAsync(ct);
        if (token is null) return (null, "No se pudo autenticar con PayPal Sandbox.");

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = currency,
                        value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    },
                    description = "Ecommerce Paduki - Compra en tienda"
                }
            }
        };

        request.Content = JsonContent.Create(payload);
        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Error al crear orden en PayPal Sandbox: {StatusCode} {Body}", response.StatusCode, body);
            return (null, "PayPal no pudo procesar la solicitud de orden.");
        }

        var json = JsonNode.Parse(body);
        var orderId = json?["id"]?.GetValue<string>();
        return (orderId, null);
    }

    public async Task<(string? TransactionId, string? Status, string? Error)> CaptureOrderAsync(string paypalOrderId, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            var demoTx = $"PAYPAL-SIM-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
            logger.LogInformation("PayPal no configurado. Capturando orden Sandbox Demo {DemoTx}.", demoTx);
            return (demoTx, "COMPLETED", null);
        }

        var token = await GetAccessTokenAsync(ct);
        if (token is null) return (null, null, "No se pudo autenticar con PayPal Sandbox.");

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders/{paypalOrderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Error al capturar orden en PayPal Sandbox: {StatusCode} {Body}", response.StatusCode, body);
            return (null, null, "No se pudo capturar el pago en PayPal Sandbox.");
        }

        var json = JsonNode.Parse(body);
        var status = json?["status"]?.GetValue<string>();
        var captureId = json?["purchase_units"]?[0]?["payments"]?["captures"]?[0]?["id"]?.GetValue<string>() ?? paypalOrderId;

        return (captureId, status, null);
    }
}
