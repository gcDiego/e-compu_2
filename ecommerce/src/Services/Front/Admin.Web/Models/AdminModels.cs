using System.ComponentModel.DataAnnotations;

namespace Admin.Web.Models;

public sealed class LoginModel { [Required,EmailAddress,StringLength(254)] public string Email { get; set; }=""; [Required,StringLength(128)] public string Password { get; set; }=""; public string? Error { get; set; } }
public sealed class ResetModel { [Required,EmailAddress] public string Email { get; set; }=""; [Required] public string Token { get; set; }=""; [Required,StringLength(128,MinimumLength=5)] public string NewPassword { get; set; }=""; }
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
public sealed class OrderFilterInput
{
    public static readonly (string Value,string Label)[] Statuses=[("confirmado","Confirmado"),("en_proceso","En preparación"),("enviado","Enviado"),("entregado","Entregado"),("cancelado","Cancelado")];
    public int Page {get;set;}=1;
    public string? Status {get;set;}
    public DateTime? From {get;set;}
    public DateTime? To {get;set;}
    [StringLength(120)] public string? TransactionId {get;set;}
    // Única fuente de los filtros: alimenta tanto la consulta a Order.Api como los enlaces de paginación.
    public Dictionary<string,string> Fields(int page)
    {
        var fields=new Dictionary<string,string>{["page"]=Math.Max(1,page).ToString()};
        if(!string.IsNullOrWhiteSpace(Status)&&Statuses.Any(x=>x.Value==Status))fields["status"]=Status;
        if(From.HasValue)fields["from"]=From.Value.ToString("yyyy-MM-dd");
        if(To.HasValue)fields["to"]=To.Value.ToString("yyyy-MM-dd");
        if(!string.IsNullOrWhiteSpace(TransactionId))fields["transactionId"]=TransactionId.Trim();
        return fields;
    }
    public string Query(int pageSize)=>string.Join('&',Fields(Page).Select(x=>$"{x.Key}={Uri.EscapeDataString(x.Value)}"))+$"&pageSize={pageSize}";
}
public sealed record OrdersViewModel(PagedOrders Orders,OrderFilterInput Filter);
public sealed record OrderItemModel(int ProductId,string ProductName,decimal UnitPrice,int Quantity,decimal Total);
public sealed record OrderDetailModel(int Id,string TransactionId,string CustomerName,string CustomerEmail,string Contact,string Phone,string Address,decimal Total,int ProductCount,DateTimeOffset Date,string Status,IReadOnlyList<OrderItemModel> Items,string? Courier=null,string? TrackingNumber=null);
public sealed class UpdateOrderStatusInput { [Required] public string Status { get; set; } = "confirmado"; [StringLength(100)] public string? Courier { get; set; } [StringLength(100)] public string? TrackingNumber { get; set; } [StringLength(250)] public string? Comment { get; set; } }
public sealed record DashboardOrders(long Sales,decimal Revenue);
public sealed record CustomerCount(long Count);
public sealed record ReportRow(DateTimeOffset Date,string Customer,string Product,decimal UnitPrice,int Quantity,decimal Total,string TransactionId);
public sealed record DashboardViewModel(long Customers,long Sales,decimal Revenue,int Products,int LowStock,IReadOnlyList<OrderModel> RecentOrders);
public sealed record CatalogViewModel(IReadOnlyList<ProductModel> Products,IReadOnlyList<CategoryModel> Categories,IReadOnlyList<BrandModel> Brands);
