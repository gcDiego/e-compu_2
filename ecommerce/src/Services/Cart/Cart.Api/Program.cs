using Cart.Api.Models;
using Cart.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"];
if (string.IsNullOrWhiteSpace(mongoConnectionString))
    throw new InvalidOperationException("MongoDb:ConnectionString is required.");

var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(_ => mongoClient.GetDatabase(builder.Configuration["MongoDb:DatabaseName"] ?? "ecommerce"));
builder.Services.AddScoped<CartStore>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/cart", async (CartStore store, ClaimsPrincipal user, CancellationToken ct) =>
{
    var customerId = CustomerIdentity.GetCustomerId(user);
    if (customerId is null) return Results.Unauthorized();
    return Results.Ok(await store.GetAsync(customerId.Value, ct));
}).RequireAuthorization();

app.MapPost("/api/cart/items", async (
    [FromBody] AddCartItemRequest request,
    CartStore store,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var customerId = CustomerIdentity.GetCustomerId(user);
    if (customerId is null) return Results.Unauthorized();

    return await store.AddAsync(customerId.Value, request.ProductId, ct) is { } cart
        ? Results.Ok(cart)
        : Results.BadRequest("No se pudo agregar el producto al carrito.");
}).RequireAuthorization();

app.MapPost("/api/cart/items/{productId:int}/change", async (
    int productId,
    [FromBody] ChangeCartItemRequest request,
    CartStore store,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var customerId = CustomerIdentity.GetCustomerId(user);
    if (customerId is null) return Results.Unauthorized();

    return await store.ChangeAsync(customerId.Value, productId, request.Increase, ct) is { } cart
        ? Results.Ok(cart)
        : Results.BadRequest("No se pudo actualizar la cantidad.");
}).RequireAuthorization();

app.MapDelete("/api/cart/items/{productId:int}", async (
    int productId,
    CartStore store,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var customerId = CustomerIdentity.GetCustomerId(user);
    if (customerId is null) return Results.Unauthorized();

    return Results.Ok(await store.RemoveAsync(customerId.Value, productId, ct));
}).RequireAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

public partial class Program { }
