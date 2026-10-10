using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Models;

public sealed record CategoryResponse(int Id, string Description, bool IsActive);
public sealed record BrandResponse(int Id, string Description, bool IsActive);
public sealed record ProductResponse(int Id, string Name, string Description, BrandResponse Brand, CategoryResponse Category, decimal Price, int Stock, bool IsActive, string? ImageUrl, long Version);
public sealed record SaveLookupRequest([property: Required, StringLength(120, MinimumLength = 1)] string Description = "", bool IsActive = true);
public sealed record SaveProductRequest([property: Required, StringLength(160, MinimumLength = 1)] string Name = "", [property: Required, StringLength(2000, MinimumLength = 1)] string Description = "", [property: Range(1, int.MaxValue)] int BrandId = 0, [property: Range(1, int.MaxValue)] int CategoryId = 0, [property: Range(typeof(decimal), "0.01", "999999999")] decimal Price = 0, [property: Range(0, 1000000)] int Stock = 0, bool IsActive = true, long Version = 0);
public sealed record StockRequest([property: Range(0, 1000000)] int Stock = 0, long Version = 0);
