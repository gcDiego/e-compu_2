using System.ComponentModel.DataAnnotations;

namespace Front.Web.Models;

public sealed record CategoryDto(int Id, string Description, bool IsActive);
public sealed record BrandDto(int Id, string Description, bool IsActive);
public sealed record ProductDto(int Id, string Name, string Description, BrandDto Brand, CategoryDto Category, decimal Price, int Stock, bool IsActive, string? ImageUrl = null);
public sealed record CartItemDto(int ProductId, string ProductName, string BrandName, decimal UnitPrice, int Quantity, string? ImageUrl = null);
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
    [Required, StringLength(15, MinimumLength = 5)] public string Password { get; set; } = "";
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

public sealed class ProfileViewModel : IValidatableObject
{
    [Required, StringLength(80, MinimumLength = 1)] public string FirstName { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 1)] public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Error { get; set; }
    public string? Success { get; set; }

    public ProfileViewModel() { }

    public ProfileViewModel(string firstName = "", string lastName = "", string email = "", string? error = null, string? success = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Error = error;
        Success = success;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return new ValidationResult("El nombre no puede estar vacío.", [nameof(FirstName)]);
        if (string.IsNullOrWhiteSpace(LastName))
            yield return new ValidationResult("El apellido no puede estar vacío.", [nameof(LastName)]);
    }
}

public sealed class ChangePasswordViewModel : IValidatableObject
{
    [Required, StringLength(128, MinimumLength = 1)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(15, MinimumLength = 5)] public string NewPassword { get; set; } = "";
    public string? Error { get; set; }

    public ChangePasswordViewModel() { }

    public ChangePasswordViewModel(string? error = null)
    {
        Error = error;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
            yield return new ValidationResult("La nueva contraseña no puede contener únicamente espacios.", [nameof(NewPassword)]);
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
    [StringLength(100)] public string? IdTransaccion { get; set; }

    public CheckoutInputModel() { }

    public CheckoutInputModel(string contacto = "", string telefono = "", string direccion = "", string idLocalidad = "", string? idTransaccion = null)
    {
        Contacto = contacto;
        Telefono = telefono;
        Direccion = direccion;
        IdLocalidad = idLocalidad;
        IdTransaccion = idTransaccion;
    }
}
public sealed record CatalogViewModel(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<BrandDto> Brands, IReadOnlyList<ProductDto> Products, int? CategoryId, int? BrandId);
public sealed record StateDto(string Id, string Description);
public sealed record MunicipalityDto(string Id, string Description);
public sealed record LocalityDto(string Id, string Description);
public sealed record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal Total);
public sealed record OrderDto(int Id, string CustomerEmail, decimal Total, int ProductCount, DateTimeOffset Date, string Status);
