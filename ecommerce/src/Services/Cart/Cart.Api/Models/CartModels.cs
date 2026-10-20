using System.ComponentModel.DataAnnotations;

namespace Cart.Api.Models;

public sealed record AddCartItemRequest([property: Range(1, int.MaxValue)] int ProductId);
public sealed record ChangeCartItemRequest(bool Increase);
public sealed record CartItemResponse(int ProductId, string ProductName, string BrandName, decimal UnitPrice, int Quantity);
public sealed record CartSnapshotResponse(IReadOnlyList<CartItemResponse> Items);
