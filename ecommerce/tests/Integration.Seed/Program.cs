using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/Services/Customer/Customer.Api/appsettings.Local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var customerUrl = config["CustomerUrl"] ?? "http://localhost:5161";
var orderUrl = config["OrderUrl"] ?? "http://localhost:5162";
var administrationUrl = config["AdministrationUrl"] ?? "http://localhost:5164";
var cartUrl = config["CartUrl"] ?? "http://localhost:5166";
var mongoConnection = config["MongoDb:ConnectionString"];
var mongoDatabaseName = config["MongoDb:DatabaseName"] ?? "ecommerce";

if (string.IsNullOrWhiteSpace(mongoConnection))
{
    Console.WriteLine("ERROR: MongoDb:ConnectionString no configurado.");
    return 1;
}

var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var email = $"seed-{now}@example.com";
var password = "SeedPassword!";
var productName = $"Seed Product {now}";

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Add("Accept", "application/json");

var mongoClient = new MongoClient(mongoConnection);
var db = mongoClient.GetDatabase(mongoDatabaseName);
var clientes = db.GetCollection<BsonDocument>("clientes");
var productos = db.GetCollection<BsonDocument>("productos");
var carritos = db.GetCollection<BsonDocument>("carritos");
var ventas = db.GetCollection<BsonDocument>("ventas");
var usuarios = db.GetCollection<BsonDocument>("usuarios");

// Limpieza previa
var emailPattern = new Regex("^seed-.*@example\\.com$", RegexOptions.Compiled);
var oldCustomers = await clientes.Find(Builders<BsonDocument>.Filter.Regex("correo", emailPattern.ToString())).ToListAsync();
var oldCustomerIds = oldCustomers.Select(d => ToInt32(d["idSqlOriginal"])).ToHashSet();

await clientes.DeleteManyAsync(Builders<BsonDocument>.Filter.In("idSqlOriginal", oldCustomerIds));
await carritos.DeleteManyAsync(Builders<BsonDocument>.Filter.In("idClienteSqlOriginal", oldCustomerIds));
await ventas.DeleteManyAsync(Builders<BsonDocument>.Filter.Regex("cliente.correo", emailPattern.ToString()));
await productos.DeleteManyAsync(Builders<BsonDocument>.Filter.Regex("nombre", "^Seed Product "));

Console.WriteLine("=== 1. Crear producto ===");
var maxProductId = await productos.Find(_ => true)
    .SortByDescending(d => d["idSqlOriginal"])
    .Project(d => d["idSqlOriginal"])
    .FirstOrDefaultAsync();
var productId = (maxProductId.IsBsonNull ? 0 : ToInt32(maxProductId)) + 1;
var productOid = ObjectId.GenerateNewId();
var product = new BsonDocument
{
    ["_id"] = productOid,
    ["idSqlOriginal"] = productId,
    ["nombre"] = productName,
    ["precio"] = 50.0,
    ["stock"] = 100,
    ["activo"] = true,
    ["marca"] = new BsonDocument { ["idRef"] = ObjectId.GenerateNewId(), ["descripcion"] = "SeedBrand" },
    ["categoria"] = new BsonDocument { ["idRef"] = ObjectId.GenerateNewId(), ["descripcion"] = "SeedCategory" }
};
await productos.InsertOneAsync(product);
Console.WriteLine($"Producto creado: idSqlOriginal={productId}, _id={productOid}");

Console.WriteLine("=== 2. Registrar cliente ===");
var registerPayload = new { FirstName = "Seed", LastName = "User", Email = email, Password = password };
var registerResponse = await http.PostAsJsonAsync($"{customerUrl}/api/customers/register", registerPayload);
Console.WriteLine($"POST {customerUrl}/api/customers/register -> {(int)registerResponse.StatusCode}");
if (!registerResponse.IsSuccessStatusCode)
{
    Console.WriteLine(await registerResponse.Content.ReadAsStringAsync());
    return 1;
}

var customerJson = await registerResponse.Content.ReadAsStringAsync();
using var customerDoc = JsonDocument.Parse(customerJson);
var customerId = customerDoc.RootElement.GetProperty("id").GetInt32();
Console.WriteLine($"Cliente creado: idSqlOriginal={customerId}, email={email}");

Console.WriteLine("=== 3. Crear carrito ===");
var cart = new BsonDocument
{
    ["_id"] = ObjectId.GenerateNewId(),
    ["idClienteSqlOriginal"] = customerId,
    ["items"] = new BsonArray
    {
        new BsonDocument
        {
            ["idProductoRef"] = productOid,
            ["idSqlOriginal"] = productId,
            ["cantidad"] = 2
        }
    }
};
await carritos.InsertOneAsync(cart);
Console.WriteLine($"Carrito creado: _id={cart["_id"].AsObjectId} para cliente {customerId}");

Console.WriteLine("=== 4. Login ===");
var loginPayload = new { Email = email, Password = password };
var loginResponse = await http.PostAsJsonAsync($"{customerUrl}/api/customers/login", loginPayload);
Console.WriteLine($"POST {customerUrl}/api/customers/login -> {(int)loginResponse.StatusCode}");
if (!loginResponse.IsSuccessStatusCode)
{
    Console.WriteLine(await loginResponse.Content.ReadAsStringAsync());
    return 1;
}

var loginJson = await loginResponse.Content.ReadAsStringAsync();
using var loginDoc = JsonDocument.Parse(loginJson);
var token = loginDoc.RootElement.GetProperty("accessToken").GetString();
Console.WriteLine($"Token recibido (len={token?.Length ?? 0})");

Console.WriteLine("=== 5. Carrito via Cart.Api ===");
HttpRequestMessage CartRequest(HttpMethod method, string url, object? body = null)
{
    var request = new HttpRequestMessage(method, $"{cartUrl}{url}");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    if (body is not null)
        request.Content = JsonContent.Create(body);
    return request;
}

var cartGetResponse = await http.SendAsync(CartRequest(HttpMethod.Get, "/api/cart"));
Console.WriteLine($"GET {cartUrl}/api/cart -> {(int)cartGetResponse.StatusCode}");
if (!cartGetResponse.IsSuccessStatusCode) { Console.WriteLine(await cartGetResponse.Content.ReadAsStringAsync()); return 1; }
using var cartGetDoc = JsonDocument.Parse(await cartGetResponse.Content.ReadAsStringAsync());
Console.WriteLine($"Items iniciales: {cartGetDoc.RootElement.GetProperty("items").GetArrayLength()}");

var cartAddResponse = await http.SendAsync(CartRequest(HttpMethod.Post, "/api/cart/items", new { ProductId = productId }));
Console.WriteLine($"POST {cartUrl}/api/cart/items -> {(int)cartAddResponse.StatusCode}");
if (!cartAddResponse.IsSuccessStatusCode) { Console.WriteLine(await cartAddResponse.Content.ReadAsStringAsync()); return 1; }
using var cartAddDoc = JsonDocument.Parse(await cartAddResponse.Content.ReadAsStringAsync());
Console.WriteLine($"Cantidad tras agregar: {cartAddDoc.RootElement.GetProperty("items")[0].GetProperty("quantity").GetInt32()}");

var cartChangeResponse = await http.SendAsync(CartRequest(HttpMethod.Post, $"/api/cart/items/{productId}/change", new { Increase = false }));
Console.WriteLine($"POST {cartUrl}/api/cart/items/{productId}/change -> {(int)cartChangeResponse.StatusCode}");
if (!cartChangeResponse.IsSuccessStatusCode) { Console.WriteLine(await cartChangeResponse.Content.ReadAsStringAsync()); return 1; }

Console.WriteLine("=== 6. Checkout via Order.Api ===");
var checkoutPayload = new { Contacto = "Seed", Telefono = "1234567", Direccion = "Calle 123", IdLocalidad = "loc1" };
HttpRequestMessage OrderCheckoutRequest()
{
    var request = new HttpRequestMessage(HttpMethod.Post, $"{orderUrl}/api/orders");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(checkoutPayload);
    return request;
}

using var checkoutRequest = OrderCheckoutRequest();
var checkoutResponse = await http.SendAsync(checkoutRequest);
Console.WriteLine($"POST {orderUrl}/api/orders -> {(int)checkoutResponse.StatusCode}");
if (!checkoutResponse.IsSuccessStatusCode)
{
    Console.WriteLine(await checkoutResponse.Content.ReadAsStringAsync());
    return 1;
}

var checkoutJson = await checkoutResponse.Content.ReadAsStringAsync();
using var checkoutDoc = JsonDocument.Parse(checkoutJson);
var orderId = checkoutDoc.RootElement.GetProperty("id").GetInt32();
var orderTotal = checkoutDoc.RootElement.GetProperty("total").GetDecimal();
var productCount = checkoutDoc.RootElement.GetProperty("productCount").GetInt32();
var status = checkoutDoc.RootElement.GetProperty("status").GetString();
Console.WriteLine($"Orden creada: id={orderId}, total={orderTotal}, productCount={productCount}, status={status}");

Console.WriteLine("=== 7. Ver historial ===");
using var historyRequest = new HttpRequestMessage(HttpMethod.Get, $"{orderUrl}/api/orders/me");
historyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

var historyResponse = await http.SendAsync(historyRequest);
Console.WriteLine($"GET {orderUrl}/api/orders/me -> {(int)historyResponse.StatusCode}");
if (!historyResponse.IsSuccessStatusCode)
{
    Console.WriteLine(await historyResponse.Content.ReadAsStringAsync());
    return 1;
}

var historyJson = await historyResponse.Content.ReadAsStringAsync();
using var historyDoc = JsonDocument.Parse(historyJson);
Console.WriteLine($"Ordenes en historial: {historyDoc.RootElement.GetArrayLength()}");

Console.WriteLine("=== 8. Carrito vaciado tras checkout ===");
var cartAfterResponse = await http.SendAsync(CartRequest(HttpMethod.Get, "/api/cart"));
using var cartAfterDoc = JsonDocument.Parse(await cartAfterResponse.Content.ReadAsStringAsync());
var remaining = cartAfterDoc.RootElement.GetProperty("items").GetArrayLength();
Console.WriteLine($"Items tras checkout: {remaining}");
if (remaining != 0) return 1;

Console.WriteLine("=== 9. Checkout concurrente ===");
var concurrentCart = new BsonDocument
{
    ["_id"] = ObjectId.GenerateNewId(),
    ["idClienteSqlOriginal"] = customerId,
    ["items"] = new BsonArray
    {
        new BsonDocument
        {
            ["idProductoRef"] = productOid,
            ["idSqlOriginal"] = productId,
            ["cantidad"] = 2
        }
    }
};
await carritos.InsertOneAsync(concurrentCart);
var concurrentKey = concurrentCart["_id"].AsObjectId.ToString();
var stockBefore = ToInt32((await productos.Find(Builders<BsonDocument>.Filter.Eq("_id", productOid)).FirstAsync())["stock"]);

var concurrentResponses = await Task.WhenAll(
    http.SendAsync(OrderCheckoutRequest()),
    http.SendAsync(OrderCheckoutRequest()));
Console.WriteLine($"POST /api/orders x2 -> {string.Join(", ", concurrentResponses.Select(r => (int)r.StatusCode))}");

if (concurrentResponses.Any(r => (int)r.StatusCode >= 500))
{
    foreach (var failed in concurrentResponses.Where(r => !r.IsSuccessStatusCode))
        Console.WriteLine(await failed.Content.ReadAsStringAsync());
    return 1;
}

var concurrentOrderIds = new List<int>();
foreach (var response in concurrentResponses.Where(r => r.IsSuccessStatusCode))
{
    using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    concurrentOrderIds.Add(responseDoc.RootElement.GetProperty("id").GetInt32());
}

var concurrentOrderCount = await ventas.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("checkoutKey", concurrentKey));
var stockAfter = ToInt32((await productos.Find(Builders<BsonDocument>.Filter.Eq("_id", productOid)).FirstAsync())["stock"]);
var residualCart = await carritos.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", concurrentCart["_id"].AsObjectId));
Console.WriteLine($"Ordenes con checkoutKey: {concurrentOrderCount}, stock {stockBefore} -> {stockAfter}, carrito residual: {residualCart}");

if (concurrentOrderCount != 1 || concurrentOrderIds.Distinct().Count() > 1 || stockAfter != stockBefore - 2 || residualCart != 0)
{
    Console.WriteLine("ERROR: la concurrencia genero ordenes duplicadas o doble descuento de stock.");
    return 1;
}

Console.WriteLine("=== 10. Rollback ante fallo parcial ===");
var unavailableProductOid = ObjectId.GenerateNewId();
var unavailableProductId = productId + 1;
await productos.InsertOneAsync(new BsonDocument
{
    ["_id"] = unavailableProductOid,
    ["idSqlOriginal"] = unavailableProductId,
    ["nombre"] = $"{productName} Unavailable",
    ["precio"] = 25.0,
    ["stock"] = 0,
    ["activo"] = true,
    ["marca"] = new BsonDocument { ["idRef"] = ObjectId.GenerateNewId(), ["descripcion"] = "SeedBrand" },
    ["categoria"] = new BsonDocument { ["idRef"] = ObjectId.GenerateNewId(), ["descripcion"] = "SeedCategory" }
});

var rollbackCart = new BsonDocument
{
    ["_id"] = ObjectId.GenerateNewId(),
    ["idClienteSqlOriginal"] = customerId,
    ["items"] = new BsonArray
    {
        new BsonDocument { ["idProductoRef"] = productOid, ["idSqlOriginal"] = productId, ["cantidad"] = 1 },
        new BsonDocument { ["idProductoRef"] = unavailableProductOid, ["idSqlOriginal"] = unavailableProductId, ["cantidad"] = 1 }
    }
};
await carritos.InsertOneAsync(rollbackCart);
var rollbackKey = rollbackCart["_id"].AsObjectId.ToString();
var rollbackStockBefore = ToInt32((await productos.Find(Builders<BsonDocument>.Filter.Eq("_id", productOid)).FirstAsync())["stock"]);
using var rollbackResponse = await http.SendAsync(OrderCheckoutRequest());
var rollbackStockAfter = ToInt32((await productos.Find(Builders<BsonDocument>.Filter.Eq("_id", productOid)).FirstAsync())["stock"]);
var rollbackOrderCount = await ventas.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("checkoutKey", rollbackKey));
var rollbackCartCount = await carritos.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", rollbackCart["_id"].AsObjectId));
Console.WriteLine($"POST /api/orders -> {(int)rollbackResponse.StatusCode}; ordenes: {rollbackOrderCount}, stock {rollbackStockBefore} -> {rollbackStockAfter}, carrito: {rollbackCartCount}");

if (rollbackResponse.IsSuccessStatusCode || rollbackOrderCount != 0 || rollbackStockAfter != rollbackStockBefore || rollbackCartCount != 1)
{
    Console.WriteLine("ERROR: el fallo parcial no revirtio por completo la reserva de stock.");
    return 1;
}

Console.WriteLine("=== 11. Limpieza ===");
await clientes.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId));
await productos.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", new[] { productOid, unavailableProductOid }));
await carritos.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId));
await ventas.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("cliente.correo", email));
Console.WriteLine("Datos de prueba eliminados.");

if (string.Equals(config["RUN_EMAIL_E2E"], "true", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("=== 12. Correo de establecimiento de administrador ===");
    var recipient = config["Email:From"];
    if (string.IsNullOrWhiteSpace(recipient))
    {
        Console.WriteLine("ERROR: Email:From no está configurado.");
        return 1;
    }

    var bootstrapEmail = $"seed-admin-{now}@example.com";
    var bootstrapPassword = "SeedAdminPassword!";
    var normalizedRecipient = recipient.Trim().ToLowerInvariant();
    var at = normalizedRecipient.LastIndexOf('@');
    if (at <= 0)
    {
        Console.WriteLine("ERROR: Email:From no contiene una dirección válida.");
        return 1;
    }
    var targetEmail = $"{normalizedRecipient[..at]}+seed-{now}{normalizedRecipient[at..]}";
    var maximumAdmin = await usuarios.Find(_ => true).SortByDescending(x => x["idSqlOriginal"]).Project(x => x["idSqlOriginal"]).FirstOrDefaultAsync();
    var bootstrapId = (maximumAdmin.IsBsonNull ? 0 : ToInt32(maximumAdmin)) + 1;
    var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bootstrapPassword))).ToLowerInvariant();
    await usuarios.InsertOneAsync(new BsonDocument
    {
        ["_id"] = ObjectId.GenerateNewId(), ["idSqlOriginal"] = bootstrapId,
        ["nombres"] = "Seed", ["apellidos"] = "Administrator", ["correo"] = bootstrapEmail,
        ["clave"] = legacyHash, ["activo"] = true, ["restablecer"] = false,
        ["fechaRegistro"] = DateTime.UtcNow, ["failedLoginCount"] = 0
    });

    try
    {
        var adminLogin = await http.PostAsJsonAsync($"{administrationUrl}/api/admin/auth/login", new { Email = bootstrapEmail, Password = bootstrapPassword });
        if (!adminLogin.IsSuccessStatusCode)
        {
            Console.WriteLine($"Login administrativo -> {(int)adminLogin.StatusCode}");
            return 1;
        }
        using var adminLoginDoc = JsonDocument.Parse(await adminLogin.Content.ReadAsStringAsync());
        var adminToken = adminLoginDoc.RootElement.GetProperty("accessToken").GetString();
        using var createAdminRequest = new HttpRequestMessage(HttpMethod.Post, $"{administrationUrl}/api/admin/users");
        createAdminRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        createAdminRequest.Content = JsonContent.Create(new { FirstName = "Seed", LastName = "Email Test", Email = targetEmail, IsActive = true });
        using var createAdminResponse = await http.SendAsync(createAdminRequest);
        Console.WriteLine($"POST /api/admin/users -> {(int)createAdminResponse.StatusCode}");
        if (!createAdminResponse.IsSuccessStatusCode) return 1;
        Console.WriteLine("SMTP aceptó el mensaje sin revelar el destinatario.");
    }
    finally
    {
        await usuarios.DeleteManyAsync(Builders<BsonDocument>.Filter.In("correo", new[] { bootstrapEmail, targetEmail }));
    }
}

Console.WriteLine("=== OK ===");
return 0;

static int ToInt32(BsonValue value) =>
    value.BsonType == BsonType.Int32 ? value.AsInt32 :
    value.BsonType == BsonType.Int64 ? (int)value.AsInt64 :
    (int)value.AsDouble;
