using System.ComponentModel.DataAnnotations;

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
public sealed class RegisterViewModel
{
    [Required, StringLength(80, MinimumLength = 1)] public string FirstName { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 1)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public RegisterViewModel() { }

    public RegisterViewModel(string firstName = "", string lastName = "", string email = "", string password = "", string? error = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Password = password;
        Error = error;
    }
}
public sealed record LoginViewModel(string Email = "", string? Error = null);

public sealed class ResetViewModel
{
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public string? Error { get; set; }

    public ResetViewModel() { }

    public ResetViewModel(string email = "", string token = "", string newPassword = "", string confirmPassword = "", string? error = null)
    {
        Email = email;
        Token = token;
        NewPassword = newPassword;
        ConfirmPassword = confirmPassword;
        Error = error;
    }
}

public sealed class LoginInputModel
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 1)] public string Password { get; set; } = "";

    public LoginInputModel() { }

    public LoginInputModel(string email = "", string password = "")
    {
        Email = email;
        Password = password;
    }
}
public sealed class CheckoutInputModel
{
    [Required, StringLength(120, MinimumLength = 2)] public string Contacto { get; set; } = "";
    [Required, RegularExpression(@"^[0-9+() .-]{7,25}$")] public string Telefono { get; set; } = "";
    [Required, StringLength(250, MinimumLength = 5)] public string Direccion { get; set; } = "";
    [Required, RegularExpression(@"^[A-Za-z0-9_-]{1,40}$")] public string IdLocalidad { get; set; } = "";

    public CheckoutInputModel() { }

    public CheckoutInputModel(string contacto = "", string telefono = "", string direccion = "", string idLocalidad = "")
    {
        Contacto = contacto;
        Telefono = telefono;
        Direccion = direccion;
        IdLocalidad = idLocalidad;
    }
}
public sealed record CatalogViewModel(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<BrandDto> Brands, IReadOnlyList<ProductDto> Products, int? CategoryId, int? BrandId);
public sealed record StateDto(string Id, string Description);
public sealed record MunicipalityDto(string Id, string Description);
public sealed record LocalityDto(string Id, string Description);
public sealed record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal Total);
public sealed record OrderDto(int Id, string CustomerEmail, decimal Total, int ProductCount, DateTimeOffset Date, string Status);
