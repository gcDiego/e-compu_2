using Front.Web.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Front.Web.Services;

public sealed class OrderApiClient(HttpClient httpClient)
{
    public async Task<(OrderDto? Order, string? Error)> CreateOrderAsync(string token, CheckoutInputModel input, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(input);

        var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<OrderDto>(ct), null);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
                return (null, content.Trim('"', '[', ']'));
        }

        return (null, "No se pudo crear la orden. Verifica tu carrito y productos.");
    }

    public async Task<IReadOnlyList<OrderDto>?> GetOrdersAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/orders/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<List<OrderDto>>(ct);
    }
}
