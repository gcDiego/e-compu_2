using Front.Web.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Front.Web.Services;

public sealed class CartApiClient(HttpClient httpClient)
{
    private HttpRequestMessage Request(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    public async Task<CartSnapshotDto?> GetAsync(string token, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, "api/cart", token);
        var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CartSnapshotDto>(ct)
            : null;
    }

    public async Task<CartSnapshotDto?> AddAsync(string token, int productId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, "api/cart/items", token, new { ProductId = productId });
        var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CartSnapshotDto>(ct)
            : null;
    }

    public async Task<CartSnapshotDto?> ChangeAsync(string token, int productId, bool increase, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, $"api/cart/items/{productId}/change", token, new { Increase = increase });
        var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CartSnapshotDto>(ct)
            : null;
    }

    public async Task<CartSnapshotDto?> RemoveAsync(string token, int productId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Delete, $"api/cart/items/{productId}", token);
        var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CartSnapshotDto>(ct)
            : null;
    }
}
