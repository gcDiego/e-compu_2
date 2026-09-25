using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/Services/Customer/Customer.Api/appsettings.Local.json", optional: true)
    .Build();

var customerUrl = config["CustomerUrl"] ?? "http://localhost:5161";
var orderUrl = config["OrderUrl"] ?? "http://localhost:5162";
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
    ["activo"] = true
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

Console.WriteLine("=== 5. Checkout via Order.Api ===");
var checkoutPayload = new { Contacto = "Seed", Telefono = "1234567", Direccion = "Calle 123", IdLocalidad = "loc1" };
using var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, $"{orderUrl}/api/orders");
checkoutRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
checkoutRequest.Content = JsonContent.Create(checkoutPayload);

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

Console.WriteLine("=== 6. Ver historial ===");
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

Console.WriteLine("=== 7. Limpieza ===");
await clientes.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId));
await productos.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", productOid));
await carritos.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId));
await ventas.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", orderId));
Console.WriteLine("Datos de prueba eliminados.");

Console.WriteLine("=== OK ===");
return 0;

static int ToInt32(BsonValue value) =>
    value.BsonType == BsonType.Int32 ? value.AsInt32 :
    value.BsonType == BsonType.Int64 ? (int)value.AsInt64 :
    (int)value.AsDouble;
