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
public sealed record CustomerDto(int Id, string FirstName, string LastName, string Email, bool MustResetPassword);
public sealed record RegisterViewModel(string FirstName = "", string LastName = "", string Email = "", string Password = "", string? Error = null);
public sealed record CatalogViewModel(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<BrandDto> Brands, IReadOnlyList<ProductDto> Products, int? CategoryId, int? BrandId);
public sealed record LoginViewModel(string Email = "", string? Error = null);
public sealed record StateDto(string Id, string Description);
public sealed record MunicipalityDto(string Id, string Description);
public sealed record LocalityDto(string Id, string Description);
public sealed record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal Total);
public sealed record OrderDto(int Id, string CustomerEmail, decimal Total, int ProductCount, DateTimeOffset Date, string Status);

