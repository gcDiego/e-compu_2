namespace Front.Web.Models;

public sealed record CategoryDto(int Id, string Description, bool IsActive);
public sealed record BrandDto(int Id, string Description, bool IsActive);
public sealed record ProductDto(int Id, string Name, string Description, BrandDto Brand, CategoryDto Category, decimal Price, int Stock, bool IsActive);
public sealed record CartItemDto(int ProductId, string ProductName, string BrandName, decimal UnitPrice, int Quantity);
public sealed record CartSnapshotDto(IReadOnlyList<CartItemDto> Items)
{
    public int DistinctItemCount => Items.Count;
    public decimal Total => Items.Sum(item => item.UnitPrice * item.Quantity);
}
public sealed record LoginResponseDto(int Id, string FirstName, string LastName, string Email, string AccountType, bool MustResetPassword, string AccessToken, DateTimeOffset ExpiresAt);
public sealed record CatalogViewModel(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<BrandDto> Brands, IReadOnlyList<ProductDto> Products, int? CategoryId, int? BrandId);
public sealed record LoginViewModel(string Email = "", string? Error = null);
