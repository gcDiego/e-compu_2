using System.ComponentModel.DataAnnotations;

namespace Order.Api.Models;

public sealed record CheckoutRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Contacto = "",
    [Required, RegularExpression(@"^[0-9+() .-]{7,25}$")] string Telefono = "",
    [Required, StringLength(250, MinimumLength = 5)] string Direccion = "",
    [Required, StringLength(40, MinimumLength = 1)] string IdLocalidad = "");

public sealed record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal Total);
public sealed record OrderResponse(int Id, string CustomerEmail, decimal Total, int ProductCount, DateTimeOffset Date, string Status);
