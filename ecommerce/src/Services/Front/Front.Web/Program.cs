using Front.Web.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddHttpClient<CatalogApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!));
builder.Services.AddHttpClient<IdentityApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Identity"]!));
builder.Services.AddHttpClient<CartApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Cart"]!));

var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"];

if (!string.IsNullOrWhiteSpace(mongoConnectionString) && !string.IsNullOrWhiteSpace(mongoDatabaseName))
{
    var mongoClient = new MongoClient(mongoConnectionString);
    builder.Services.AddSingleton<IMongoClient>(mongoClient);
    builder.Services.AddSingleton(_ => mongoClient.GetDatabase(mongoDatabaseName));
    builder.Services.AddScoped<MongoCatalogService>();
    builder.Services.AddScoped<MongoCustomerService>();
    builder.Services.AddScoped<MongoLocationService>();
    builder.Services.AddScoped<MongoCartService>();
    builder.Services.AddScoped<MongoOrderService>();
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Ecommerce.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Ecommerce.Services";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
var jwtLifetimeMinutes = builder.Configuration.GetValue<int?>("Jwt:LifetimeMinutes") ?? 30;

if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters and configured in user secrets or environment variables.");

builder.Services.AddSingleton(new JwtTokenIssuer(jwtIssuer, jwtAudience, jwtSigningKey, TimeSpan.FromMinutes(jwtLifetimeMinutes)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
