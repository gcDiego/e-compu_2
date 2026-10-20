using Administration.Api.Models;
using Administration.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true).AddEnvironmentVariables();
var connection = builder.Configuration["MongoDb:ConnectionString"];
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("MongoDb:ConnectionString is required.");
var key = builder.Configuration["Jwt:SigningKey"] ?? "";
if (key.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? "Ecommerce.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "Ecommerce.Services";
var mongo = new MongoClient(connection);
builder.Services.AddSingleton<IMongoClient>(mongo);
builder.Services.AddSingleton(_ => mongo.GetDatabase(builder.Configuration["MongoDb:DatabaseName"] ?? "ecommerce"));
builder.Services.AddSingleton<AdminPasswordHasher>();
builder.Services.AddScoped<AdminStore>();
builder.Services.AddScoped<MongoSequenceService>();
builder.Services.AddSingleton<AdminEmailSender>();
builder.Services.AddSingleton(new AdminJwtIssuer(issuer, audience, key, TimeSpan.FromMinutes(builder.Configuration.GetValue("Jwt:LifetimeMinutes", 30))));
builder.Services.AddHostedService<AdminDbInitializer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = issuer, ValidAudience = audience, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.FromMinutes(1) });
builder.Services.AddAuthorization(o => o.AddPolicy("admin", p => p.RequireRole("admin")));
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter($"{context.Connection.RemoteIpAddress}:{context.Request.Headers.UserAgent}", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/admin/auth/login", async ([FromBody] LoginRequest request, AdminStore store, AdminJwtIssuer jwt, CancellationToken ct) =>
{
    if (!Valid(request, out var errors)) return Results.ValidationProblem(errors);
    var admin = await store.AuthenticateAsync(request.Email, request.Password, ct);
    return admin is null ? Results.Unauthorized() : Results.Ok(jwt.Issue(admin));
}).RequireRateLimiting("login");
app.MapPost("/api/admin/auth/recover", async ([FromBody] RecoverRequest request, AdminStore store, AdminEmailSender email, CancellationToken ct) =>
{
    if (Valid(request, out _)) { var token = await store.GenerateRecoveryTokenAsync(request.Email, ct); if (token is not null) await email.SendResetAsync(request.Email, token, ct); }
    return Results.NoContent();
}).RequireRateLimiting("login");
app.MapPost("/api/admin/auth/reset", async ([FromBody] ResetRequest request, AdminStore store, CancellationToken ct) =>
{
    if (!Valid(request, out var errors)) return Results.ValidationProblem(errors);
    return await store.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct) ? Results.NoContent() : Results.BadRequest();
}).RequireRateLimiting("login");
app.MapGet("/api/admin/users", (AdminStore store, CancellationToken ct) => store.ListAsync(ct)).RequireAuthorization("admin");
app.MapPost("/api/admin/users", async ([FromBody] SaveAdminRequest request, AdminStore store, AdminEmailSender email, ClaimsPrincipal user, CancellationToken ct) =>
{
    if (!Valid(request, out var errors)) return Results.ValidationProblem(errors);
    var created = await store.CreateAsync(request, Actor(user), ct);
    if (created is null) return Results.Conflict();
    await email.SendResetAsync(created.Profile.Email, created.ResetToken, ct);
    return Results.Created($"/api/admin/users/{created.Profile.Id}", created.Profile);
}).RequireAuthorization("admin");
app.MapPut("/api/admin/users/{id:int}", async (int id, [FromBody] SaveAdminRequest request, AdminStore store, ClaimsPrincipal user, CancellationToken ct) =>
{
    if (!Valid(request, out var errors)) return Results.ValidationProblem(errors);
    var updated = await store.UpdateAsync(id, request, Actor(user), ct);
    return updated is null ? Results.Conflict() : Results.Ok(updated);
}).RequireAuthorization("admin");
app.MapPatch("/api/admin/users/{id:int}/active", async (int id, [FromBody] bool active, AdminStore store, ClaimsPrincipal user, CancellationToken ct) => await store.SetActiveAsync(id, active, Actor(user), ct) ? Results.NoContent() : Results.BadRequest()).RequireAuthorization("admin");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

static int Actor(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : 0;
static bool Valid(object model, out Dictionary<string, string[]> errors) { var results = new List<ValidationResult>(); var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, true); errors = results.GroupBy(x => x.MemberNames.FirstOrDefault() ?? "request").ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Valor inválido").ToArray()); return valid; }
public partial class Program { }
