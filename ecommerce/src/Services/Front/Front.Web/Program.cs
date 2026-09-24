using Front.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 128;
    options.ValueLengthLimit = 4096;
    options.MultipartBodyLengthLimit = 1_048_576;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("authentication", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment() ? ".Ecommerce.Session" : "__Host-Ecommerce.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"];
var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"];

if (!string.IsNullOrWhiteSpace(mongoConnectionString) && !string.IsNullOrWhiteSpace(mongoDatabaseName))
{
    var mongoClient = new MongoClient(mongoConnectionString);
    builder.Services.AddSingleton<IMongoClient>(mongoClient);
    builder.Services.AddSingleton(_ => mongoClient.GetDatabase(mongoDatabaseName));
    builder.Services.AddScoped<MongoCatalogService>();
    builder.Services.AddScoped<MongoLocationService>();
    builder.Services.AddScoped<MongoCartService>();
    builder.Services.AddHostedService<MongoDbInitializer>();
}

builder.Services.AddHttpClient<CustomerApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Customer"] ?? "http://localhost:5161"));
builder.Services.AddHttpClient<OrderApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Order"] ?? "http://localhost:5162"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers.ContentSecurityPolicy = "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self'";
        if (context.Request.Path.StartsWithSegments("/Account")
            || context.Request.Path.StartsWithSegments("/Cart")
            || context.Request.Path.StartsWithSegments("/Checkout")
            || context.Request.Path.StartsWithSegments("/Order"))
        {
            headers.CacheControl = "no-store, max-age=0";
            headers.Pragma = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
