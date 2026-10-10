using Admin.Web.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Admin.Web.Services;

public sealed class AdminApiClient(IHttpClientFactory factory,IHttpContextAccessor context)
{
    private HttpClient Client(string name,bool authenticated=true) { var client=factory.CreateClient(name); if(authenticated){var token=context.HttpContext?.Session.GetString("AdminToken"); if(!string.IsNullOrWhiteSpace(token))client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);} return client; }
    public async Task<LoginResponse?> Login(string email,string password,CancellationToken ct){var r=await Client("Administration",false).PostAsJsonAsync("api/admin/auth/login",new{email,password},ct);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken:ct):null;}
    public Task<HttpResponseMessage> Recover(string email,CancellationToken ct)=>Client("Administration",false).PostAsJsonAsync("api/admin/auth/recover",new{email},ct);
    public Task<HttpResponseMessage> Reset(ResetModel m,CancellationToken ct)=>Client("Administration",false).PostAsJsonAsync("api/admin/auth/reset",new{m.Email,m.Token,m.NewPassword},ct);
    public async Task<IReadOnlyList<AdminProfile>> Admins(CancellationToken ct)=>await Client("Administration").GetFromJsonAsync<List<AdminProfile>>("api/admin/users",ct)??[];
    public Task<HttpResponseMessage> SaveAdmin(SaveAdminModel m,CancellationToken ct)=>m.Id.HasValue?Client("Administration").PutAsJsonAsync($"api/admin/users/{m.Id}",m,ct):Client("Administration").PostAsJsonAsync("api/admin/users",m,ct);
    public async Task<IReadOnlyList<ProductModel>> Products(CancellationToken ct)=>await Client("Catalog").GetFromJsonAsync<List<ProductModel>>("api/admin/catalog/products",ct)??[];
    public async Task<IReadOnlyList<CategoryModel>> Categories(CancellationToken ct)=>await Client("Catalog").GetFromJsonAsync<List<CategoryModel>>("api/admin/catalog/categories",ct)??[];
    public async Task<IReadOnlyList<BrandModel>> Brands(CancellationToken ct)=>await Client("Catalog").GetFromJsonAsync<List<BrandModel>>("api/admin/catalog/brands",ct)??[];
    public Task<HttpResponseMessage> SaveLookup(string kind,LookupInput m,CancellationToken ct)=>m.Id.HasValue?Client("Catalog").PutAsJsonAsync($"api/admin/catalog/{kind}/{m.Id}",m,ct):Client("Catalog").PostAsJsonAsync($"api/admin/catalog/{kind}",m,ct);
    public async Task<HttpResponseMessage> SaveProduct(ProductInput m,CancellationToken ct){var payload=new{m.Name,m.Description,m.BrandId,m.CategoryId,m.Price,m.Stock,m.IsActive,m.Version};var response=m.Id.HasValue?await Client("Catalog").PutAsJsonAsync($"api/admin/catalog/products/{m.Id}",payload,ct):await Client("Catalog").PostAsJsonAsync("api/admin/catalog/products",payload,ct);if(response.IsSuccessStatusCode&&m.Image is not null){var product=await response.Content.ReadFromJsonAsync<ProductModel>(cancellationToken:ct);using var form=new MultipartFormDataContent();form.Add(new StreamContent(m.Image.OpenReadStream()),"image",m.Image.FileName);response=await Client("Catalog").PostAsync($"api/admin/catalog/products/{product!.Id}/image",form,ct);}return response;}
    public async Task<PagedOrders> Orders(int page,CancellationToken ct)=>await Client("Order").GetFromJsonAsync<PagedOrders>($"api/admin/orders?page={page}&pageSize=50",ct)??new([],0,1,50);
    public async Task<OrderDetailModel?> OrderDetail(int id,CancellationToken ct){var r=await Client("Order").GetAsync($"api/admin/orders/{id}",ct);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<OrderDetailModel>(cancellationToken:ct):null;}
    public async Task<DashboardOrders> OrderDashboard(CancellationToken ct)=>await Client("Order").GetFromJsonAsync<DashboardOrders>("api/admin/orders/dashboard",ct)??new(0,0);
    public async Task<CustomerCount> CustomerCount(CancellationToken ct)=>await Client("Customer").GetFromJsonAsync<CustomerCount>("api/admin/customers/count",ct)??new(0);
    public async Task<IReadOnlyList<ReportRow>> Report(DateTime from,DateTime to,string? transaction,CancellationToken ct)=>await Client("Order").GetFromJsonAsync<List<ReportRow>>($"api/admin/orders/report?from={from:O}&to={to:O}&transactionId={Uri.EscapeDataString(transaction??"")}",ct)??[];
}
