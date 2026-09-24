using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoCartService(IMongoDatabase database)
{
    private const int MaximumQuantityPerProduct = 99;
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
            var productFilter = Builders<BsonDocument>.Filter.Eq("_id", item["idProductoRef"].AsObjectId)
                & Builders<BsonDocument>.Filter.Eq("activo", true);
            var product = await _productos.Find(productFilter).FirstOrDefaultAsync(ct);
            if (product is null) continue;

            var productId = ToInt32(product["idSqlOriginal"]);
            var brandName = product["marca"]["descripcion"].AsString;
            mapped.Add(new CartItemDto(productId, product["nombre"].AsString, brandName, ToDecimal(product["precio"]), item["cantidad"].AsInt32));
        }
        return new CartSnapshotDto(mapped);
    }

    public async Task<CartSnapshotDto?> AddAsync(int customerId, int productId, CancellationToken ct)
    {
        var customerExists = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).AnyAsync(ct);
        if (!customerExists) return null;

        var productFilter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", productId)
            & Builders<BsonDocument>.Filter.Eq("activo", true)
            & Builders<BsonDocument>.Filter.Gt("stock", 0);
        var product = await _productos.Find(productFilter).FirstOrDefaultAsync(ct);
        if (product is null) return null;

        var cart = await EnsureCartAsync(customerId, ct);
        var items = cart["items"].AsBsonArray;
        var existing = items.Cast<BsonDocument>().FirstOrDefault(i => i["idProductoRef"].AsObjectId == product["_id"].AsObjectId);
        if (existing is not null)
        {
            var quantity = existing["cantidad"].AsInt32;
            var stock = ToInt32(product["stock"]);
            if (quantity >= stock || quantity >= MaximumQuantityPerProduct) return null;
            existing["cantidad"] = quantity + 1;
            existing["nombreProducto"] = product["nombre"].AsString;
            existing["precio"] = product["precio"];
        }
        else
        {
            items.Add(new BsonDocument
            {
                ["idProductoRef"] = product["_id"].AsObjectId,
                ["idSqlOriginal"] = productId,
                ["nombreProducto"] = product["nombre"].AsString,
                ["precio"] = product["precio"],
                ["cantidad"] = 1
            });
        }

        await _carritos.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cart, cancellationToken: ct);
        return await GetAsync(customerId, ct);
    }

    public async Task<CartSnapshotDto?> ChangeAsync(int customerId, int productId, bool increase, CancellationToken ct)
    {
        var productFilter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", productId)
            & Builders<BsonDocument>.Filter.Eq("activo", true);
        var product = await _productos.Find(productFilter).FirstOrDefaultAsync(ct);
        if (product is null) return null;

        var filter = Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId);
        var cart = await _carritos.Find(filter).FirstOrDefaultAsync(ct);
        if (cart is null) return null;

        var item = cart["items"].AsBsonArray.Cast<BsonDocument>().FirstOrDefault(i => i["idProductoRef"].AsObjectId == product["_id"].AsObjectId);
        if (item is null) return null;

        var currentQuantity = item["cantidad"].AsInt32;
        var newQuantity = currentQuantity + (increase ? 1 : -1);
        if (increase && (newQuantity > ToInt32(product["stock"]) || newQuantity > MaximumQuantityPerProduct)) return null;

        if (newQuantity <= 0)
            cart["items"].AsBsonArray.Remove(item);
        else
        {
            item["cantidad"] = newQuantity;
            item["nombreProducto"] = product["nombre"].AsString;
            item["precio"] = product["precio"];
        }

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

        var customer = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Customer does not exist.");
        cart = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["idClienteRef"] = customer["_id"].AsObjectId,
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
