using System.ComponentModel.DataAnnotations;

namespace Admin.Web.Models;

public sealed class LoginModel { [Required,EmailAddress,StringLength(254)] public string Email { get; set; }=""; [Required,StringLength(128)] public string Password { get; set; }=""; public string? Error { get; set; } }
public sealed class ResetModel { [Required,EmailAddress] public string Email { get; set; }=""; [Required] public string Token { get; set; }=""; [Required,StringLength(128,MinimumLength=12)] public string NewPassword { get; set; }=""; }
public sealed record LoginResponse(int Id,string FirstName,string LastName,string Email,string AccessToken,DateTimeOffset ExpiresAt);
public sealed record AdminProfile(int Id,string FirstName,string LastName,string Email,bool IsActive);
public sealed class SaveAdminModel { public int? Id { get;set; } [Required,StringLength(80)] public string FirstName {get;set;}=""; [Required,StringLength(100)] public string LastName {get;set;}=""; [Required,EmailAddress] public string Email {get;set;}=""; public bool IsActive {get;set;}=true; }
public sealed record CategoryModel(int Id,string Description,bool IsActive);
public sealed record BrandModel(int Id,string Description,bool IsActive);
public sealed record ProductModel(int Id,string Name,string Description,BrandModel Brand,CategoryModel Category,decimal Price,int Stock,bool IsActive,string? ImageUrl,long Version);
public sealed class LookupInput { public int? Id {get;set;} [Required,StringLength(120)] public string Description {get;set;}=""; public bool IsActive {get;set;}=true; }
public sealed class ProductInput { public int? Id {get;set;} [Required,StringLength(160)] public string Name {get;set;}=""; [Required,StringLength(2000)] public string Description {get;set;}=""; [Range(1,int.MaxValue)] public int BrandId {get;set;} [Range(1,int.MaxValue)] public int CategoryId {get;set;} [Range(typeof(decimal),"0.01","999999999")] public decimal Price {get;set;} [Range(0,1000000)] public int Stock {get;set;} public bool IsActive {get;set;}=true; public long Version {get;set;} public IFormFile? Image {get;set;} }
public sealed record OrderModel(int Id,string CustomerEmail,decimal Total,int ProductCount,DateTimeOffset Date,string Status);
public sealed record PagedOrders(IReadOnlyList<OrderModel> Items,long Total,int Page,int PageSize);
public sealed record OrderItemModel(int ProductId,string ProductName,decimal UnitPrice,int Quantity,decimal Total);
public sealed record OrderDetailModel(int Id,string TransactionId,string CustomerName,string CustomerEmail,string Contact,string Phone,string Address,decimal Total,int ProductCount,DateTimeOffset Date,string Status,IReadOnlyList<OrderItemModel> Items);
public sealed record DashboardOrders(long Sales,decimal Revenue);
public sealed record CustomerCount(long Count);
public sealed record ReportRow(DateTimeOffset Date,string Customer,string Product,decimal UnitPrice,int Quantity,decimal Total,string TransactionId);
public sealed record DashboardViewModel(long Customers,long Sales,decimal Revenue,int Products,int LowStock,IReadOnlyList<OrderModel> RecentOrders);
public sealed record CatalogViewModel(IReadOnlyList<ProductModel> Products,IReadOnlyList<CategoryModel> Categories,IReadOnlyList<BrandModel> Brands);
