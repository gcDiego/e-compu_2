using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoCartService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _carritos = database.GetCollection<BsonDocument>("carritos");
    private readonly IMongoCollection<BsonDocument> _productos = database.GetCollection<BsonDocument>("productos");
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");

    public async Task<CartSnapshotDto> GetAsync(int customerId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId);
        var cart = await _carritos.Find(filter).FirstOrDefaultAsync(ct);
        if (cart is null) return new CartSnapshotDto([]);

        var mapped = new List<CartItemDto>();
        foreach (var item in cart["items"].AsBsonArray.Cast<BsonDocument>())
        {
            var product = await _productos.Find(Builders<BsonDocument>.Filter.Eq("_id", item["idProductoRef"].AsObjectId)).FirstOrDefaultAsync(ct);
            var productId = product is not null ? ToInt32(product["idSqlOriginal"]) : (item.Contains("idSqlOriginal") ? ToInt32(item["idSqlOriginal"]) : 0);
            var brandName = product is not null ? product["marca"]["descripcion"].AsString : string.Empty;
            mapped.Add(new CartItemDto(productId, item["nombreProducto"].AsString, brandName, ToDecimal(item["precio"]), item["cantidad"].AsInt32));
        }
        return new CartSnapshotDto(mapped);
    }

    public async Task<CartSnapshotDto?> AddAsync(int customerId, int productId, CancellationToken ct)
    {
        var product = await _productos.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", productId)).FirstOrDefaultAsync(ct);
        if (product is null) return null;

        var cart = await EnsureCartAsync(customerId, ct);
        var items = cart["items"].AsBsonArray;
        var existing = items.Cast<BsonDocument>().FirstOrDefault(i => i["idProductoRef"].AsObjectId == product["_id"].AsObjectId);
        if (existing is not null)
        {
            existing["cantidad"] = existing["cantidad"].AsInt32 + 1;
        }
        else
        {
            items.Add(new BsonDocument
            {
                ["idProductoRef"] = product["_id"].AsObjectId,
                ["idSqlOriginal"] = productId,
                ["nombreProducto"] = product["nombre"].AsString,
                ["precio"] = product["precio"].ToDouble(),
                ["cantidad"] = 1
            });
        }

        await _carritos.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cart, cancellationToken: ct);
        return await GetAsync(customerId, ct);
    }

    public async Task<CartSnapshotDto> ChangeAsync(int customerId, int productId, bool increase, CancellationToken ct)
    {
        var product = await _productos.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", productId)).FirstOrDefaultAsync(ct);
        if (product is null) return new CartSnapshotDto([]);

        var filter = Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId);
        var cart = await _carritos.Find(filter).FirstOrDefaultAsync(ct);
        if (cart is null) return new CartSnapshotDto([]);

        var item = cart["items"].AsBsonArray.Cast<BsonDocument>().FirstOrDefault(i => i["idProductoRef"].AsObjectId == product["_id"].AsObjectId);
        if (item is null) return await GetAsync(customerId, ct);

        var newQty = item["cantidad"].AsInt32 + (increase ? 1 : -1);
        if (newQty <= 0)
            cart["items"].AsBsonArray.Remove(item);
        else
            item["cantidad"] = newQty;

        await _carritos.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cart, cancellationToken: ct);
        return await GetAsync(customerId, ct);
    }

    public async Task<CartSnapshotDto> RemoveAsync(int customerId, int productId, CancellationToken ct)
    {
        var product = await _productos.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", productId)).FirstOrDefaultAsync(ct);
        if (product is null) return new CartSnapshotDto([]);

        var filter = Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId);
        var cart = await _carritos.Find(filter).FirstOrDefaultAsync(ct);
        if (cart is null) return new CartSnapshotDto([]);

        var item = cart["items"].AsBsonArray.Cast<BsonDocument>().FirstOrDefault(i => i["idProductoRef"].AsObjectId == product["_id"].AsObjectId);
        if (item is not null)
            cart["items"].AsBsonArray.Remove(item);

        await _carritos.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cart, cancellationToken: ct);
        return await GetAsync(customerId, ct);
    }

    private async Task<BsonDocument> EnsureCartAsync(int customerId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId);
        var cart = await _carritos.Find(filter).FirstOrDefaultAsync(ct);
        if (cart is not null) return cart;

        var customer = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
        cart = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["idClienteRef"] = customer?["_id"].AsObjectId ?? ObjectId.GenerateNewId(),
            ["idClienteSqlOriginal"] = customerId,
            ["items"] = new BsonArray()
        };
        await _carritos.InsertOneAsync(cart, cancellationToken: ct);
        return cart;
    }

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;

    private static decimal ToDecimal(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 :
        value.BsonType == BsonType.Int64 ? (decimal)value.AsInt64 :
        value.BsonType == BsonType.Decimal128 ? value.AsDecimal :
        (decimal)value.AsDouble;
}
