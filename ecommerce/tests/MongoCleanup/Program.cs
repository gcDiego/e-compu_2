using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddInMemoryCollection(DotEnv.Read(".env"))
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

var productos = db.GetCollection<BsonDocument>("productos");
var legacyImageFilter = Builders<BsonDocument>.Filter.Or(
    Builders<BsonDocument>.Filter.Exists("rutaImagen"),
    Builders<BsonDocument>.Filter.Exists("nombreImagen"));
var legacyImageUpdate = await productos.UpdateManyAsync(
    legacyImageFilter,
    Builders<BsonDocument>.Update.Unset("rutaImagen").Unset("nombreImagen"));
Console.WriteLine($"productos: {legacyImageUpdate.ModifiedCount} documentos sin campos de imagen heredados (rutaImagen/nombreImagen).");

// Las ventas heredadas no tienen checkoutKey; sin este filtro todas caerían en el grupo null y se borrarían.
var groups = ventas.Aggregate()
    .Match(Builders<BsonDocument>.Filter.Type("checkoutKey", BsonType.String))
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
    var checkoutKey = group["_id"].AsString;
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
