using Customer.Api.Models;
using Customer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton<CustomerPasswordHasher>();

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
    builder.Services.AddScoped<CustomerStore>();
    builder.Services.AddScoped<MongoSequenceService>();

    var smtpHost = builder.Configuration["Email:SmtpHost"];
    if (string.IsNullOrWhiteSpace(smtpHost))
        builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
    else
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Ecommerce.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Ecommerce.Services";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
var jwtLifetimeMinutes = builder.Configuration.GetValue<int?>("Jwt:LifetimeMinutes") ?? 30;

if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

builder.Services.AddSingleton(new JwtTokenIssuer(jwtIssuer, jwtAudience, jwtSigningKey, TimeSpan.FromMinutes(jwtLifetimeMinutes)));

var app = builder.Build();
var isDevelopment = app.Environment.IsDevelopment();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.MapPost("/api/customers/register", async (
    [FromBody] RegisterRequest request,
    CustomerStore store,
    CancellationToken ct) =>
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var created = await store.CreateCustomerAsync(request.FirstName, request.LastName, request.Email, request.Password, ct);
    if (created is null)
        return Results.Conflict("The email is already in use.");

    return Results.Ok(created);
});

app.MapPost("/api/customers/login", async (
    [FromBody] LoginRequest request,
    CustomerStore store,
    JwtTokenIssuer jwt,
    CancellationToken ct) =>
{
    var customer = await store.VerifyAndRehashAsync(request.Email, request.Password, ct);
    if (customer is null)
        return Results.Unauthorized();

    var token = jwt.Issue(customer);
    var response = new LoginResponse(
        customer.Id,
        customer.FirstName,
        customer.LastName,
        customer.Email,
        customer.AccountType,
        customer.MustResetPassword,
        token.Token,
        token.ExpiresAt);

    return Results.Ok(response);
});

app.MapPost("/api/customers/recover", async (
    [FromBody] RecoverRequest request,
    CustomerStore store,
    IEmailSender emailSender,
    IConfiguration config,
    CancellationToken ct) =>
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var token = await store.GenerateRecoveryTokenAsync(request.Email, ct);

    if (!string.IsNullOrWhiteSpace(token))
    {
        var resetBaseUrl = config["Email:ResetBaseUrl"] ?? string.Empty;
        var resetLink = string.IsNullOrWhiteSpace(resetBaseUrl)
            ? string.Empty
            : $"{resetBaseUrl}/Account/Reset?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";

        var body = $"Tu token de recuperación es: {token}{Environment.NewLine}Válido por 1 hora.";
        if (!string.IsNullOrWhiteSpace(resetLink))
            //body += $"{Environment.NewLine}Restablece tu contraseña aquí: {resetLink}";

        try
        {
            await emailSender.SendAsync(request.Email, "Recuperación de contraseña", body, ct);
        }
        catch (Exception ex)
        {
            if (isDevelopment)
                return Results.Ok(new { recoveryToken = token, warning = ex.Message });
            return Results.NoContent();
        }
    }

    return Results.NoContent();
});

app.MapPost("/api/customers/reset", async (
    [FromBody] ResetRequest request,
    CustomerStore store,
    CancellationToken ct) =>
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var success = await store.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
    if (!success)
        return Results.BadRequest("El token es inválido, ha expirado o el correo no coincide.");

    return Results.Ok("Contraseña actualizada.");
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
