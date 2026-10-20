using MongoDB.Bson;
using MongoDB.Driver;

namespace Catalog.Api.Services;

public sealed class CatalogDbInitializer(IMongoDatabase database, ILogger<CatalogDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var name in new[] { "categorias", "marcas", "productos" })
        {
            await EnsureUniqueIndexAsync(database.GetCollection<BsonDocument>(name), name, ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public static bool HasEquivalentUniqueIndex(IEnumerable<BsonDocument> indexes, string field) =>
        indexes.Any(index =>
            index.TryGetValue("key", out var key) &&
            key.IsBsonDocument &&
            key.AsBsonDocument.ElementCount == 1 &&
            key.AsBsonDocument.TryGetValue(field, out var direction) &&
            direction.IsNumeric &&
            direction.ToDouble() == 1 &&
            index.TryGetValue("unique", out var unique) &&
            unique.ToBoolean());

    private async Task EnsureUniqueIndexAsync(
        IMongoCollection<BsonDocument> collection,
        string collectionName,
        CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(ct);
        if (HasEquivalentUniqueIndex(await cursor.ToListAsync(ct), "idSqlOriginal"))
        {
            logger.LogInformation("El índice único de {Collection}.idSqlOriginal ya existe", collectionName);
            return;
        }

        try
        {
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("idSqlOriginal"),
                    new CreateIndexOptions { Unique = true, Name = $"ux_{collectionName}_id" }),
                cancellationToken: ct);
        }
        catch (MongoCommandException ex) when (ex.Code == 85)
        {
            logger.LogWarning("El índice único de {Collection}.idSqlOriginal ya existe con otra configuración nominal", collectionName);
        }
        catch (MongoCommandException ex)
        {
            logger.LogError(ex, "No se pudo crear el índice de {Collection}: {Code}", collectionName, ex.Code);
        }
    }
}
