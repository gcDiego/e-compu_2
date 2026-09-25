using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Order.Api.Models;
using Order.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"];
if (string.IsNullOrWhiteSpace(mongoConnectionString))
    throw new InvalidOperationException("MongoDb:ConnectionString is required.");

var mongoDatabaseName = !string.IsNullOrWhiteSpace(builder.Configuration["MongoDb:DatabaseName"])
    ? builder.Configuration["MongoDb:DatabaseName"]!
    : "ecommerce";

{
    var mongoClient = new MongoClient(mongoConnectionString);
    builder.Services.AddSingleton<IMongoClient>(mongoClient);
    builder.Services.AddSingleton(_ => mongoClient.GetDatabase(mongoDatabaseName));
    builder.Services.AddScoped<OrderStore>();
    builder.Services.AddScoped<MongoSequenceService>();
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Ecommerce.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Ecommerce.Services";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;

if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/orders", async (
    [FromBody] CheckoutRequest request,
    OrderStore store,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var customerId = store.GetCustomerId(user);
    if (customerId is null)
        return Results.Unauthorized();

    var order = await store.CreateOrderAsync(customerId.Value, request, ct);
    if (order is null)
        return Results.BadRequest("No se pudo crear la orden. Verifica tu carrito y productos.");

    return Results.Ok(order);
}).RequireAuthorization();

app.MapGet("/api/orders/me", async (OrderStore store, ClaimsPrincipal user, CancellationToken ct) =>
{
    var customerId = store.GetCustomerId(user);
    if (customerId is null)
        return Results.Unauthorized();

    var orders = await store.GetOrdersByCustomerAsync(customerId.Value, ct);
    return Results.Ok(orders);
}).RequireAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
