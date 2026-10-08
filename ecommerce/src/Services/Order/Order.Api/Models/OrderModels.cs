using System.ComponentModel.DataAnnotations;

namespace Order.Api.Models;

public sealed record CheckoutRequest([property: Required, StringLength(120, MinimumLength = 2)] string Contacto = "", [property: Required, RegularExpression(@"^[0-9+() .-]{7,25}$")] string Telefono = "", [property: Required, StringLength(250, MinimumLength = 5)] string Direccion = "", [property: Required, StringLength(40, MinimumLength = 1)] string IdLocalidad = "");
public sealed record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal Total);
public sealed record OrderResponse(int Id, string CustomerEmail, decimal Total, int ProductCount, DateTimeOffset Date, string Status);
public sealed record AdminOrderItem(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Total);
public sealed record AdminOrderDetail(int Id, string TransactionId, string CustomerName, string CustomerEmail, string Contact, string Phone, string Address, decimal Total, int ProductCount, DateTimeOffset Date, string Status, IReadOnlyList<AdminOrderItem> Items);
public sealed record PagedOrders(IReadOnlyList<OrderResponse> Items, long Total, int Page, int PageSize);
public sealed record DashboardResponse(long Sales, decimal Revenue);
public sealed record ReportRow(DateTimeOffset Date, string Customer, string Product, decimal UnitPrice, int Quantity, decimal Total, string TransactionId);
