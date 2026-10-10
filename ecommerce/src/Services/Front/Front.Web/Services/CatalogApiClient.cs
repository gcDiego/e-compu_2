using Front.Web.Models;
using System.Net.Http.Json;

namespace Front.Web.Services;

public sealed class CatalogApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) => await client.GetFromJsonAsync<List<CategoryDto>>("api/catalog/categories", ct) ?? [];
    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(int? categoryId, CancellationToken ct)
    {
        _ = categoryId;
        return await client.GetFromJsonAsync<List<BrandDto>>("api/catalog/brands", ct) ?? [];
    }
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? categoryId, int? brandId, CancellationToken ct)
    {
        var query = new List<string>();
        if (categoryId.HasValue) query.Add($"categoryId={categoryId.Value}");
        if (brandId.HasValue) query.Add($"brandId={brandId.Value}");
        return await client.GetFromJsonAsync<List<ProductDto>>($"api/catalog/products{(query.Count == 0 ? "" : "?" + string.Join('&', query))}", ct) ?? [];
    }
    public async Task<ProductDto?> GetProductAsync(int id, CancellationToken ct)
    {
        var response = await client.GetAsync($"api/catalog/products/{id}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct) : null;
    }
}
