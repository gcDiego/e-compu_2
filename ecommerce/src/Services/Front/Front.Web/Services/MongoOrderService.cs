using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoOrderService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _ventas = database.GetCollection<BsonDocument>("ventas");
    private readonly IMongoCollection<BsonDocument> _carritos = database.GetCollection<BsonDocument>("carritos");
    private readonly IMongoCollection<BsonDocument> _productos = database.GetCollection<BsonDocument>("productos");
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");

    public async Task<OrderDto?> CreateOrderAsync(int customerId, string contacto, string telefono, string direccion, string idLocalidad, CancellationToken ct)
    {
        var customer = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
        if (customer is null) return null;

        var cart = await _carritos.Find(Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
        if (cart is null || cart["items"].AsBsonArray.Count == 0) return null;

        var detalle = new BsonArray();
        var totalProductos = 0;
        decimal montoTotal = 0;

        foreach (var item in cart["items"].AsBsonArray.Cast<BsonDocument>())
        {
            var product = await _productos.Find(Builders<BsonDocument>.Filter.Eq("_id", item["idProductoRef"].AsObjectId)).FirstOrDefaultAsync(ct);
            if (product is null || !product["activo"].AsBoolean) return null;

            var cantidad = item["cantidad"].AsInt32;
            if (product["stock"].AsInt32 < cantidad) return null;

            var precio = ToDecimal(item["precio"]);
            var total = precio * cantidad;
            detalle.Add(new BsonDocument
            {
                ["idProductoRef"] = item["idProductoRef"].AsObjectId,
                ["nombreProducto"] = item["nombreProducto"].AsString,
                ["cantidad"] = cantidad,
                ["total"] = total
            });

            totalProductos += cantidad;
            montoTotal += total;
        }

        var maxId = await _ventas.Find(_ => true)
            .SortByDescending(d => d["idSqlOriginal"])
            .Project(d => d["idSqlOriginal"])
            .FirstOrDefaultAsync(ct);
        int newId = (maxId.IsInt32 ? maxId.AsInt32 : 0) + 1;

        var now = DateTime.UtcNow;
        var order = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["idSqlOriginal"] = newId,
            ["cliente"] = new BsonDocument
            {
                ["idRef"] = customer["_id"].AsObjectId,
                ["nombres"] = customer["nombres"].AsString,
                ["apellidos"] = customer["apellidos"].AsString,
                ["correo"] = customer["correo"].AsString
            },
            ["contacto"] = contacto,
            ["telefono"] = telefono,
            ["direccion"] = direccion,
            ["idLocalidad"] = idLocalidad,
            ["fechaVenta"] = now,
            ["montoTotal"] = montoTotal,
            ["totalProducto"] = totalProductos,
            ["detalle"] = detalle,
            ["idTransaccion"] = $"code{newId:D4}",
            ["seguimiento"] = new BsonDocument
            {
                ["estadoActual"] = "confirmado",
                ["numeroGuia"] = BsonNull.Value,
                ["paqueteria"] = BsonNull.Value,
                ["ubicacionActual"] = new BsonDocument
                {
                    ["ciudad"] = "",
                    ["municipio"] = "",
                    ["estado"] = ""
                },
                ["historial"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["estado"] = "confirmado",
                        ["fecha"] = now,
                        ["descripcion"] = "Compra confirmada",
                        ["ubicacion"] = new BsonDocument
                        {
                            ["ciudad"] = "",
                            ["municipio"] = "",
                            ["estado"] = ""
                        }
                    }
                }
            }
        };

        await _ventas.InsertOneAsync(order, cancellationToken: ct);
        await _carritos.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cancellationToken: ct);

        return new OrderDto(newId, customer["correo"].AsString, montoTotal, totalProductos, new DateTimeOffset(now), "confirmado");
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersByCustomerAsync(int customerId, CancellationToken ct)
    {
        var customer = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
        if (customer is null) return [];

        var filter = Builders<BsonDocument>.Filter.Eq("cliente.idRef", customer["_id"].AsObjectId);
        var docs = await _ventas.Find(filter).SortByDescending(d => d["fechaVenta"]).ToListAsync(ct);
        return docs.Select(d => new OrderDto(
            ToInt32(d["idSqlOriginal"]),
            d["cliente"]["correo"].AsString,
            ToDecimal(d["montoTotal"]),
            d["totalProducto"].AsInt32,
            new DateTimeOffset(d["fechaVenta"].ToUniversalTime()),
            d["seguimiento"]["estadoActual"].AsString)).ToList();
    }

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;

    private static decimal ToDecimal(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (decimal)value.AsInt64 : (decimal)value.AsDouble;
}
