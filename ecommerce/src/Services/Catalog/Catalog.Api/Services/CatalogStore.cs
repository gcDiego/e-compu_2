using Catalog.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Catalog.Api.Services;

public sealed class CatalogStore(IMongoDatabase database, MongoSequenceService sequence)
{
    private readonly IMongoCollection<BsonDocument> _categories = database.GetCollection<BsonDocument>("categorias");
    private readonly IMongoCollection<BsonDocument> _brands = database.GetCollection<BsonDocument>("marcas");
    private readonly IMongoCollection<BsonDocument> _products = database.GetCollection<BsonDocument>("productos");
    private readonly IMongoCollection<BsonDocument> _audit = database.GetCollection<BsonDocument>("auditoria");

    public async Task<IReadOnlyList<CategoryResponse>> CategoriesAsync(bool includeInactive, CancellationToken ct) => (await _categories.Find(includeInactive ? FilterDefinition<BsonDocument>.Empty : Builders<BsonDocument>.Filter.Eq("activo", true)).SortBy(x => x["descripcion"]).ToListAsync(ct)).Select(MapCategory).ToList();
    public async Task<IReadOnlyList<BrandResponse>> BrandsAsync(bool includeInactive, CancellationToken ct) => (await _brands.Find(includeInactive ? FilterDefinition<BsonDocument>.Empty : Builders<BsonDocument>.Filter.Eq("activo", true)).SortBy(x => x["descripcion"]).ToListAsync(ct)).Select(MapBrand).ToList();
    public async Task<IReadOnlyList<ProductResponse>> ProductsAsync(int? categoryId, int? brandId, bool includeInactive, CancellationToken ct)
    {
        var categories = await CategoryMap(ct); var brands = await BrandMap(ct);
        var filter = includeInactive ? FilterDefinition<BsonDocument>.Empty : Builders<BsonDocument>.Filter.Eq("activo", true);
        if (categoryId.HasValue && categories.Values.FirstOrDefault(x => GetInt(x, "idSqlOriginal") == categoryId) is { } category) filter &= Builders<BsonDocument>.Filter.Eq("categoria.idRef", category["_id"]);
        if (brandId.HasValue && brands.Values.FirstOrDefault(x => GetInt(x, "idSqlOriginal") == brandId) is { } brand) filter &= Builders<BsonDocument>.Filter.Eq("marca.idRef", brand["_id"]);
        return (await _products.Find(filter).SortBy(x => x["nombre"]).Limit(500).ToListAsync(ct)).Select(x => MapProduct(x, categories, brands)).ToList();
    }
    public async Task<ProductResponse?> ProductAsync(int id, bool includeInactive, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id); if (!includeInactive) filter &= Builders<BsonDocument>.Filter.Eq("activo", true);
        var doc = await _products.Find(filter).FirstOrDefaultAsync(ct); if (doc is null) return null;
        return MapProduct(doc, await CategoryMap(ct), await BrandMap(ct));
    }
    public Task<CategoryResponse?> SaveCategoryAsync(int? id, SaveLookupRequest r, int actor, CancellationToken ct) => SaveLookup(_categories, "categorias", id, r, actor, MapCategory, ct);
    public Task<BrandResponse?> SaveBrandAsync(int? id, SaveLookupRequest r, int actor, CancellationToken ct) => SaveLookup(_brands, "marcas", id, r, actor, MapBrand, ct);

    public async Task<ProductResponse?> SaveProductAsync(int? id, SaveProductRequest r, int actor, CancellationToken ct)
    {
        var category = await _categories.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", r.CategoryId)).FirstOrDefaultAsync(ct);
        var brand = await _brands.Find(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", r.BrandId)).FirstOrDefaultAsync(ct);
        if (category is null || brand is null) return null;
        BsonDocument? doc;
        if (id is null)
        {
            var maximum = await Maximum(_products, ct); var newId = await sequence.NextAsync("productos.idSqlOriginal", maximum, ct);
            doc = new BsonDocument { ["_id"] = ObjectId.GenerateNewId(), ["idSqlOriginal"] = newId, ["nombre"] = r.Name.Trim(), ["descripcion"] = r.Description.Trim(), ["marca"] = Ref(brand), ["categoria"] = Ref(category), ["precio"] = r.Price, ["stock"] = r.Stock, ["activo"] = r.IsActive, ["version"] = 1L, ["fechaRegistro"] = DateTime.UtcNow };
            await _products.InsertOneAsync(doc, cancellationToken: ct); id = newId;
        }
        else
        {
            var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id.Value) & Builders<BsonDocument>.Filter.Eq("version", r.Version);
            var update = Builders<BsonDocument>.Update.Set("nombre", r.Name.Trim()).Set("descripcion", r.Description.Trim()).Set("marca", Ref(brand)).Set("categoria", Ref(category)).Set("precio", r.Price).Set("stock", r.Stock).Set("activo", r.IsActive).Inc("version", 1).Set("fechaModificacion", DateTime.UtcNow);
            doc = await _products.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, ct);
            if (doc is null) return null;
        }
        await Audit(actor, "save", "producto", id!.Value, ct);
        return MapProduct(doc, await CategoryMap(ct), await BrandMap(ct));
    }
    public async Task<bool> SetStockAsync(int id, StockRequest request, int actor, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id) & Builders<BsonDocument>.Filter.Eq("version", request.Version);
        var result = await _products.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("stock", request.Stock).Inc("version", 1).Set("fechaModificacion", DateTime.UtcNow), cancellationToken: ct);
        if (result.ModifiedCount > 0) await Audit(actor, "stock", "producto", id, ct); return result.ModifiedCount > 0;
    }
    public async Task<(string? OldPublicId, bool Success)> SetImageAsync(int id, StoredImage image, int actor, CancellationToken ct)
    {
        var doc = await _products.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id), Builders<BsonDocument>.Update.Set("imageUrl", image.Url).Set("imagePublicId", image.PublicId).Inc("version", 1), new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.Before }, ct);
        if (doc is null) return (null, false); await Audit(actor, "image", "producto", id, ct);
        return (GetString(doc, "imagePublicId", null), true);
    }

    private async Task<T?> SaveLookup<T>(IMongoCollection<BsonDocument> collection, string counter, int? id, SaveLookupRequest r, int actor, Func<BsonDocument,T> map, CancellationToken ct) where T : class
    {
        BsonDocument? doc;
        if (id is null) { var newId = await sequence.NextAsync($"{counter}.idSqlOriginal", await Maximum(collection, ct), ct); doc = new BsonDocument { ["_id"] = ObjectId.GenerateNewId(), ["idSqlOriginal"] = newId, ["descripcion"] = r.Description.Trim(), ["activo"] = r.IsActive, ["fechaRegistro"] = DateTime.UtcNow }; await collection.InsertOneAsync(doc, cancellationToken: ct); id = newId; }
        else doc = await collection.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id.Value), Builders<BsonDocument>.Update.Set("descripcion", r.Description.Trim()).Set("activo", r.IsActive).Set("fechaModificacion", DateTime.UtcNow), new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, ct);
        if (doc is null) return null; await Audit(actor, "save", counter.TrimEnd('s'), id.Value, ct); return map(doc);
    }
    private async Task<Dictionary<ObjectId,BsonDocument>> CategoryMap(CancellationToken ct) => (await _categories.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct)).ToDictionary(x => x["_id"].AsObjectId);
    private async Task<Dictionary<ObjectId,BsonDocument>> BrandMap(CancellationToken ct) => (await _brands.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct)).ToDictionary(x => x["_id"].AsObjectId);
    private async Task Audit(int actor, string action, string entity, int id, CancellationToken ct) => await _audit.InsertOneAsync(new BsonDocument { ["actorId"] = actor, ["action"] = action, ["entity"] = entity, ["entityId"] = id, ["timestamp"] = DateTime.UtcNow }, cancellationToken: ct);
    private static async Task<int> Maximum(IMongoCollection<BsonDocument> c, CancellationToken ct) { var value = await c.Find(FilterDefinition<BsonDocument>.Empty).SortByDescending(x => x["idSqlOriginal"]).Project(x => x["idSqlOriginal"]).FirstOrDefaultAsync(ct); return value?.ToInt32() ?? 0; }
    private static BsonDocument Ref(BsonDocument d) => new() { ["idRef"] = d["_id"], ["descripcion"] = GetString(d, "descripcion", "") };
    private static CategoryResponse MapCategory(BsonDocument d) => new(GetInt(d,"idSqlOriginal"), GetString(d,"descripcion","")!, GetBool(d,"activo",true));
    private static BrandResponse MapBrand(BsonDocument d) => new(GetInt(d,"idSqlOriginal"), GetString(d,"descripcion","")!, GetBool(d,"activo",true));
    private static ProductResponse MapProduct(BsonDocument d, Dictionary<ObjectId,BsonDocument> categories, Dictionary<ObjectId,BsonDocument> brands) { var cr = d["categoria"]["idRef"].AsObjectId; var br = d["marca"]["idRef"].AsObjectId; var c = categories.GetValueOrDefault(cr); var b = brands.GetValueOrDefault(br); return new(GetInt(d,"idSqlOriginal"), GetString(d,"nombre","")!, GetString(d,"descripcion","")!, b is null ? new(0,GetString(d["marca"].AsBsonDocument,"descripcion","")!,true) : MapBrand(b), c is null ? new(0,GetString(d["categoria"].AsBsonDocument,"descripcion","")!,true) : MapCategory(c), GetDecimal(d,"precio"), GetInt(d,"stock"), GetBool(d,"activo",true), GetString(d,"imageUrl",null), d.TryGetValue("version",out var v) && v.IsNumeric ? v.ToInt64() : 0); }
    private static int GetInt(BsonDocument d,string n) => d.TryGetValue(n,out var v)&&v.IsNumeric?v.ToInt32():0;
    private static decimal GetDecimal(BsonDocument d,string n) => d.TryGetValue(n,out var v)&&v.IsNumeric?v.ToDecimal():0;
    private static string? GetString(BsonDocument d,string n,string? fallback) => d.TryGetValue(n,out var v)&&v.IsString?v.AsString:fallback;
    private static bool GetBool(BsonDocument d,string n,bool fallback) => d.TryGetValue(n,out var v)&&v.IsBoolean?v.AsBoolean:fallback;
}
