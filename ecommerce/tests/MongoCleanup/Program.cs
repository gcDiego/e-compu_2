using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/Services/Customer/Customer.Api/appsettings.Local.json", optional: true)
    .Build();

var mongoConnection = config["MongoDb:ConnectionString"];
var mongoDatabaseName = config["MongoDb:DatabaseName"] ?? "ecommerce";

if (string.IsNullOrWhiteSpace(mongoConnection))
{
    Console.WriteLine("ERROR: MongoDb:ConnectionString no configurado.");
    return 1;
}

var client = new MongoClient(mongoConnection);
var db = client.GetDatabase(mongoDatabaseName);
var ventas = db.GetCollection<BsonDocument>("ventas");

var groups = ventas.Aggregate()
    .Group(new BsonDocument
    {
        ["_id"] = "$checkoutKey",
        ["count"] = new BsonDocument("$sum", 1),
        ["ids"] = new BsonDocument("$push", "$_id")
    })
    .Match(Builders<BsonDocument>.Filter.Gt("count", 1))
    .ToList();

if (groups.Count == 0)
{
    Console.WriteLine("No se encontraron duplicados en ventas.checkoutKey.");
    return 0;
}

Console.WriteLine($"Se encontraron {groups.Count} checkoutKeys duplicados.");
int deleted = 0;

foreach (var group in groups)
{
    var checkoutKey = group["_id"].IsBsonNull ? null : group["_id"].AsString;
    var ids = group["ids"].AsBsonArray.Select(v => v.AsObjectId).ToList();
    Console.WriteLine($"checkoutKey='{checkoutKey}' tiene {ids.Count} duplicados.");

    // Ordenar por fechaVenta ascendente y conservar el más antiguo.
    var documents = await ventas
        .Find(Builders<BsonDocument>.Filter.In("_id", ids))
        .SortBy(d => d["fechaVenta"])
        .ToListAsync();

    var toDelete = documents.Skip(1).Select(d => d["_id"].AsObjectId).ToList();
    if (toDelete.Count > 0)
    {
        await ventas.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", toDelete));
        deleted += toDelete.Count;
    }
}

Console.WriteLine($"Eliminados {deleted} documentos duplicados.");
return 0;
