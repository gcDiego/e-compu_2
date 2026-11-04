using Customer.Api.Models;
using Customer.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

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
    builder.Services.AddHostedService<CustomerDbInitializer>();

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
    });
builder.Services.AddAuthorization(options => options.AddPolicy("admin", policy => policy.RequireRole("admin")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        $"{context.Connection.RemoteIpAddress}:{context.Request.Headers.UserAgent}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
var isDevelopment = app.Environment.IsDevelopment();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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
}).RequireRateLimiting("login");

app.MapPost("/api/customers/login", async (
    [FromBody] LoginRequest request,
    CustomerStore store,
    JwtTokenIssuer jwt,
    CancellationToken ct) =>
{
    var auth = await store.AuthenticateAsync(request.Email, request.Password, ct);
    if (auth.Status == CustomerAuthStatus.Locked)
        return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Cuenta bloqueada temporalmente por demasiados intentos fallidos. Intenta más tarde.");

    if (auth.Status != CustomerAuthStatus.Success || auth.Customer is null)
        return Results.Unauthorized();

    var customer = auth.Customer;
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
}).RequireRateLimiting("login");

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
}).RequireRateLimiting("login");

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
}).RequireRateLimiting("login");

app.MapGet("/api/admin/customers/count", async (CustomerStore store, CancellationToken ct) =>
    Results.Ok(new { count = await store.CountAsync(ct) })).RequireAuthorization("admin");

app.MapGet("/api/customers/me", async (
    ClaimsPrincipal user,
    CustomerStore store,
    CancellationToken ct) =>
{
    var customerId = GetCustomerId(user);
    if (customerId is null)
        return Results.Unauthorized();

    var profile = await store.GetCustomerByIdAsync(customerId.Value, ct);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
}).RequireAuthorization();

app.MapPut("/api/customers/me", async (
    [FromBody] UpdateProfileRequest request,
    ClaimsPrincipal user,
    CustomerStore store,
    CancellationToken ct) =>
{
    var customerId = GetCustomerId(user);
    if (customerId is null)
        return Results.Unauthorized();

    var mustReset = user.FindFirstValue("must_reset_password");
    if (string.Equals(mustReset, "true", StringComparison.OrdinalIgnoreCase))
        return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Debes cambiar tu contraseña antes de realizar cambios en tu perfil.");

    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var updated = await store.UpdateProfileAsync(customerId.Value, request.FirstName, request.LastName, ct);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).RequireAuthorization();

app.MapPost("/api/customers/change-password", async (
    [FromBody] ChangePasswordRequest request,
    ClaimsPrincipal user,
    CustomerStore store,
    JwtTokenIssuer jwt,
    CancellationToken ct) =>
{
    var customerId = GetCustomerId(user);
    if (customerId is null)
        return Results.Unauthorized();

    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        return Results.BadRequest(results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));

    var result = await store.ChangePasswordAsync(customerId.Value, request.CurrentPassword, request.NewPassword, ct);
    if (result.Status == ChangePasswordStatus.Success && result.UpdatedProfile is not null)
    {
        var refreshedToken = jwt.Issue(result.UpdatedProfile);
        return Results.Ok(new ChangePasswordResponse(refreshedToken.Token, refreshedToken.ExpiresAt));
    }

    return result.Status switch
    {
        ChangePasswordStatus.InvalidCurrentPassword => Results.BadRequest("La contraseña actual no es correcta."),
        ChangePasswordStatus.ConcurrencyConflict => Results.Conflict("La contraseña fue modificada concurrentemente. Intenta de nuevo."),
        _ => Results.NotFound()
    };
}).RequireAuthorization().RequireRateLimiting("login");

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

static int? GetCustomerId(ClaimsPrincipal user)
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    return int.TryParse(sub, out var id) ? id : null;
}
