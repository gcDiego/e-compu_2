using Location.Api.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"];
if (string.IsNullOrWhiteSpace(mongoConnectionString))
    throw new InvalidOperationException("MongoDb:ConnectionString is required.");

var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(_ => mongoClient.GetDatabase(builder.Configuration["MongoDb:DatabaseName"] ?? "ecommerce"));
builder.Services.AddScoped<LocationStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.MapGet("/api/location/states", async (LocationStore store, CancellationToken ct) =>
    Results.Ok(await store.GetStatesAsync(ct)));

app.MapGet("/api/location/municipalities/{stateId}", async (string stateId, LocationStore store, CancellationToken ct) =>
    !LocationIdValidator.IsValid(stateId)
        ? Results.BadRequest()
        : Results.Ok(await store.GetMunicipalitiesByStateAsync(stateId, ct)));

app.MapGet("/api/location/localities/{municipalityId}", async (string municipalityId, LocationStore store, CancellationToken ct) =>
    !LocationIdValidator.IsValid(municipalityId)
        ? Results.BadRequest()
        : Results.Ok(await store.GetLocalitiesByMunicipalityAsync(municipalityId, ct)));

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

public partial class Program { }
