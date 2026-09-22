using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoCatalogService(IMongoDatabase database)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        var collection = database.GetCollection<BsonDocument>("categorias");
        var filter = Builders<BsonDocument>.Filter.Eq("activo", true);
        var docs = await collection.Find(filter).SortBy(d => d["descripcion"]).ToListAsync(ct);
        return docs.Select(MapCategory).ToList();
    }

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(int? categoryId, CancellationToken ct)
    {
        _ = categoryId;
        var collection = database.GetCollection<BsonDocument>("marcas");
        var filter = Builders<BsonDocument>.Filter.Eq("activo", true);
        var docs = await collection.Find(filter).SortBy(d => d["descripcion"]).ToListAsync(ct);
        return docs.Select(MapBrand).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? categoryId, int? brandId, CancellationToken ct)
    {
        var categories = await GetCategoryMapAsync(ct);
        var brands = await GetBrandMapAsync(ct);

        var productCollection = database.GetCollection<BsonDocument>("productos");
        var filter = Builders<BsonDocument>.Filter.Eq("activo", true);
        var docs = await productCollection.Find(filter).ToListAsync(ct);

        if (categoryId.HasValue)
        {
            var categoryRef = categories
                .Where(c => c.Value.Id == categoryId.Value)
                .Select(c => c.Key)
                .FirstOrDefault();
            if (categoryRef != default)
                docs = docs.Where(d => d["categoria"]["idRef"].AsObjectId == categoryRef).ToList();
        }

        if (brandId.HasValue)
        {
            var brandRef = brands
                .Where(b => b.Value.Id == brandId.Value)
                .Select(b => b.Key)
                .FirstOrDefault();
            if (brandRef != default)
                docs = docs.Where(d => d["marca"]["idRef"].AsObjectId == brandRef).ToList();
        }

        return docs.Select(d => MapProduct(d, categories, brands)).ToList();
    }

    public async Task<ProductDto?> GetProductAsync(int id, CancellationToken ct)
    {
        var categories = await GetCategoryMapAsync(ct);
        var brands = await GetBrandMapAsync(ct);
        var collection = database.GetCollection<BsonDocument>("productos");
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id)
            & Builders<BsonDocument>.Filter.Eq("activo", true);
        var doc = await collection.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapProduct(doc, categories, brands);
    }

    private async Task<Dictionary<ObjectId, CategoryDto>> GetCategoryMapAsync(CancellationToken ct)
    {
        var collection = database.GetCollection<BsonDocument>("categorias");
        var filter = Builders<BsonDocument>.Filter.Eq("activo", true);
        var docs = await collection.Find(filter).ToListAsync(ct);
        return docs.ToDictionary(d => d["_id"].AsObjectId, MapCategory);
    }

    private async Task<Dictionary<ObjectId, BrandDto>> GetBrandMapAsync(CancellationToken ct)
    {
        var collection = database.GetCollection<BsonDocument>("marcas");
        var filter = Builders<BsonDocument>.Filter.Eq("activo", true);
        var docs = await collection.Find(filter).ToListAsync(ct);
        return docs.ToDictionary(d => d["_id"].AsObjectId, MapBrand);
    }

    private static CategoryDto MapCategory(BsonDocument d) =>
        new(ToInt32(d["idSqlOriginal"]), d["descripcion"].AsString, d["activo"].AsBoolean);

    private static BrandDto MapBrand(BsonDocument d) =>
        new(ToInt32(d["idSqlOriginal"]), d["descripcion"].AsString, d["activo"].AsBoolean);

    private static ProductDto MapProduct(BsonDocument d, Dictionary<ObjectId, CategoryDto> categories, Dictionary<ObjectId, BrandDto> brands)
    {
        var categoryRef = d["categoria"]["idRef"].AsObjectId;
        var brandRef = d["marca"]["idRef"].AsObjectId;

        var category = categories.TryGetValue(categoryRef, out var c)
            ? c
            : new CategoryDto(0, d["categoria"]["descripcion"].AsString, true);
        var brand = brands.TryGetValue(brandRef, out var b)
            ? b
            : new BrandDto(0, d["marca"]["descripcion"].AsString, true);

        return new ProductDto(
            ToInt32(d["idSqlOriginal"]),
            d["nombre"].AsString,
            d["descripcion"].AsString,
            brand,
            category,
            ToDecimal(d["precio"]),
            ToInt32(d["stock"]),
            d["activo"].AsBoolean);
    }

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;

    private static decimal ToDecimal(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 :
        value.BsonType == BsonType.Int64 ? (decimal)value.AsInt64 :
        value.BsonType == BsonType.Decimal128 ? value.AsDecimal :
        (decimal)value.AsDouble;
}
