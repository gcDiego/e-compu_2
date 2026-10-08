using MongoDB.Bson;
using MongoDB.Driver;
using Order.Api.Models;
using System.Security.Claims;

namespace Order.Api.Services;

public sealed class OrderStore(IMongoDatabase database, IMongoClient mongoClient, MongoSequenceService sequenceService)
{
    private readonly IMongoCollection<BsonDocument> _ventas = database.GetCollection<BsonDocument>("ventas");
    private readonly IMongoCollection<BsonDocument> _carritos = database.GetCollection<BsonDocument>("carritos");
    private readonly IMongoCollection<BsonDocument> _productos = database.GetCollection<BsonDocument>("productos");
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");

    public int? GetCustomerId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }

    public async Task<OrderResponse?> CreateOrderAsync(int customerId, CheckoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Contacto) || string.IsNullOrWhiteSpace(request.Telefono)
            || string.IsNullOrWhiteSpace(request.Direccion) || string.IsNullOrWhiteSpace(request.IdLocalidad))
            return null;

        using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(new TransactionOptions(
            readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority));

        string? checkoutKey = null;

        try
        {
            var customer = await _clientes.Find(session, Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
            if (customer is null)
            {
                await session.AbortTransactionAsync(ct);
                return null;
            }

            var cart = await _carritos.Find(session, Builders<BsonDocument>.Filter.Eq("idClienteSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
            if (cart is null || cart["items"].AsBsonArray.Count == 0)
            {
                await session.AbortTransactionAsync(ct);
                return null;
            }

            checkoutKey = cart["_id"].AsObjectId.ToString();
            var existingOrder = await _ventas.Find(session, Builders<BsonDocument>.Filter.Eq("checkoutKey", checkoutKey)).FirstOrDefaultAsync(ct);
            if (existingOrder is not null)
            {
                await session.AbortTransactionAsync(ct);
                return MapOrder(existingOrder);
            }

            var detalle = new BsonArray();
            var totalProductos = 0;
            decimal montoTotal = 0;

            foreach (var item in cart["items"].AsBsonArray.Cast<BsonDocument>())
            {
                var productRef = item["idProductoRef"].AsObjectId;
                var cantidad = item["cantidad"].AsInt32;
                if (cantidad <= 0)
                {
                    await session.AbortTransactionAsync(ct);
                    return null;
                }

                var product = await _productos.FindOneAndUpdateAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", productRef)
                    & Builders<BsonDocument>.Filter.Eq("activo", true)
                    & Builders<BsonDocument>.Filter.Gte("stock", cantidad),
                    Builders<BsonDocument>.Update.Inc("stock", -cantidad),
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                    ct);

                if (product is null)
                {
                    await session.AbortTransactionAsync(ct);
                    return null;
                }

                var precio = ToDecimal(product["precio"]);
                var total = precio * cantidad;
                detalle.Add(new BsonDocument
                {
                    ["idProductoRef"] = productRef,
                    ["idSqlOriginal"] = ToInt32(product["idSqlOriginal"]),
                    ["nombreProducto"] = product["nombre"].AsString,
                    ["precioUnitario"] = precio,
                    ["cantidad"] = cantidad,
                    ["total"] = total
                });

                totalProductos += cantidad;
                montoTotal += total;
            }

            var maxIdValue = await _ventas.Find(session, FilterDefinition<BsonDocument>.Empty)
                .SortByDescending(d => d["idSqlOriginal"])
                .Project(d => d["idSqlOriginal"])
                .FirstOrDefaultAsync(ct);
            var maxId = maxIdValue.IsBsonNull ? 0 : ToInt32(maxIdValue);
            var newId = await sequenceService.NextAsync("ventas.idSqlOriginal", maxId, session, ct);
            var now = DateTime.UtcNow;
            var order = new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["idSqlOriginal"] = newId,
                ["checkoutKey"] = checkoutKey,
                ["cliente"] = new BsonDocument
                {
                    ["idRef"] = customer["_id"].AsObjectId,
                    ["nombres"] = customer["nombres"].AsString,
                    ["apellidos"] = customer["apellidos"].AsString,
                    ["correo"] = customer["correo"].AsString
                },
                ["contacto"] = request.Contacto.Trim(),
                ["telefono"] = request.Telefono.Trim(),
                ["direccion"] = request.Direccion.Trim(),
                ["idLocalidad"] = request.IdLocalidad.Trim(),
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

            await _ventas.InsertOneAsync(session, order, cancellationToken: ct);
            await _carritos.DeleteOneAsync(session, Builders<BsonDocument>.Filter.Eq("_id", cart["_id"].AsObjectId), cancellationToken: ct);
            await session.CommitTransactionAsync(ct);

            return MapOrder(order);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000 && checkoutKey is not null)
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(ct);

            var existing = await _ventas.Find(Builders<BsonDocument>.Filter.Eq("checkoutKey", checkoutKey))
                .FirstOrDefaultAsync(ct);
            return existing is not null ? MapOrder(existing) : null;
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<OrderResponse>> GetOrdersByCustomerAsync(int customerId, CancellationToken ct)
    {
        var customer = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", customerId)).FirstOrDefaultAsync(ct);
        if (customer is null) return [];

        var filter = Builders<BsonDocument>.Filter.Eq("cliente.idRef", customer["_id"].AsObjectId);
        var docs = await _ventas.Find(filter).SortByDescending(d => d["fechaVenta"]).ToListAsync(ct);
        return docs.Select(MapOrder).ToList();
    }

    public async Task<PagedOrders> GetAdminOrdersAsync(DateTime? from, DateTime? to, string? transactionId, string? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var filter = FilterDefinition<BsonDocument>.Empty;
        if (from.HasValue) filter &= Builders<BsonDocument>.Filter.Gte("fechaVenta", from.Value.ToUniversalTime());
        if (to.HasValue) filter &= Builders<BsonDocument>.Filter.Lt("fechaVenta", to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(transactionId)) filter &= Builders<BsonDocument>.Filter.Eq("idTransaccion", transactionId.Trim());
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<BsonDocument>.Filter.Eq("seguimiento.estadoActual", status.Trim().ToLowerInvariant());
        var total = await _ventas.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await _ventas.Find(filter).SortByDescending(x => x["fechaVenta"]).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return new PagedOrders(docs.Select(MapOrder).ToList(), total, page, pageSize);
    }

    public async Task<AdminOrderDetail?> GetAdminOrderAsync(int id, CancellationToken ct)
    {
        var d = await _ventas.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id)).FirstOrDefaultAsync(ct);
        if (d is null) return null;
        var customer = d["cliente"].AsBsonDocument;
        var items = d["detalle"].AsBsonArray.Cast<BsonDocument>().Select(x => new AdminOrderItem(
            x.TryGetValue("idSqlOriginal", out var sqlId) && sqlId.IsNumeric ? ToInt32(sqlId) : 0,
            x["nombreProducto"].AsString,
            x.TryGetValue("precioUnitario", out var price) && price.IsNumeric ? ToDecimal(price) : ToDecimal(x["total"]) / Math.Max(1, ToInt32(x["cantidad"])),
            ToInt32(x["cantidad"]), ToDecimal(x["total"]))).ToList();
        return new AdminOrderDetail(ToInt32(d["idSqlOriginal"]), d.GetValue("idTransaccion", "").AsString,
            $"{customer.GetValue("nombres", "")} {customer.GetValue("apellidos", "")}".Trim(), customer.GetValue("correo", "").AsString,
            d.GetValue("contacto", "").AsString, d.GetValue("telefono", "").AsString, d.GetValue("direccion", "").AsString,
            ToDecimal(d["montoTotal"]), ToInt32(d["totalProducto"]), new DateTimeOffset(d["fechaVenta"].ToUniversalTime()), d["seguimiento"]["estadoActual"].AsString, items);
    }

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var docs = await _ventas.Find(FilterDefinition<BsonDocument>.Empty).Project(Builders<BsonDocument>.Projection.Include("montoTotal")).ToListAsync(ct);
        return new DashboardResponse(docs.Count, docs.Sum(x => x.TryGetValue("montoTotal", out var value) && value.IsNumeric ? ToDecimal(value) : 0));
    }

    public async Task<IReadOnlyList<ReportRow>> GetReportAsync(DateTime from, DateTime to, string? transactionId, CancellationToken ct)
    {
        if (to.Date < from.Date || to.Date - from.Date > TimeSpan.FromDays(366)) return [];
        var filter = Builders<BsonDocument>.Filter.Gte("fechaVenta", from.Date.ToUniversalTime()) & Builders<BsonDocument>.Filter.Lt("fechaVenta", to.Date.AddDays(1).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(transactionId)) filter &= Builders<BsonDocument>.Filter.Eq("idTransaccion", transactionId.Trim());
        var docs = await _ventas.Find(filter).SortByDescending(x => x["fechaVenta"]).Limit(5000).ToListAsync(ct);
        return docs.SelectMany(d => d["detalle"].AsBsonArray.Cast<BsonDocument>().Select(item =>
        {
            var quantity = ToInt32(item["cantidad"]); var total = ToDecimal(item["total"]);
            return new ReportRow(new DateTimeOffset(d["fechaVenta"].ToUniversalTime()), d["cliente"].AsBsonDocument.GetValue("correo", "").AsString, item["nombreProducto"].AsString, item.TryGetValue("precioUnitario", out var price) && price.IsNumeric ? ToDecimal(price) : total / Math.Max(1, quantity), quantity, total, d.GetValue("idTransaccion", "").AsString);
        })).ToList();
    }

    private static OrderResponse MapOrder(BsonDocument document) =>
        new(
            ToInt32(document["idSqlOriginal"]),
            document["cliente"]["correo"].AsString,
            ToDecimal(document["montoTotal"]),
            ToInt32(document["totalProducto"]),
            new DateTimeOffset(document["fechaVenta"].ToUniversalTime()),
            document["seguimiento"]["estadoActual"].AsString);

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;

    private static decimal ToDecimal(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 :
        value.BsonType == BsonType.Int64 ? (decimal)value.AsInt64 :
        value.BsonType == BsonType.Decimal128 ? value.AsDecimal :
        (decimal)value.AsDouble;
}
