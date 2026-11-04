using MongoDB.Bson;
using MongoDB.Driver;

namespace Order.Api.Services;

public sealed class OrderDbInitializer(IMongoDatabase database, ILogger<OrderDbInitializer> logger) : IHostedService
{
    // checkoutKey va sparse: las ventas heredadas no tienen el campo y un índice único normal no se podría crear.
    public static readonly (string Field, string Name, bool Sparse)[] UniqueIndexes =
    [
        ("checkoutKey", "ux_ventas_checkoutkey", true),
        ("idSqlOriginal", "ux_ventas_id", false)
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        var ventas = database.GetCollection<BsonDocument>("ventas");
        foreach (var (field, name, sparse) in UniqueIndexes)
            await EnsureUniqueIndexAsync(ventas, field, name, sparse, ct);
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
        string field,
        string name,
        bool sparse,
        CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(ct);
        if (HasEquivalentUniqueIndex(await cursor.ToListAsync(ct), field))
        {
            logger.LogInformation("El índice único de ventas sobre {Field} ya existe", field);
            return;
        }

        try
        {
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(field),
                    new CreateIndexOptions { Unique = true, Sparse = sparse, Name = name }),
                cancellationToken: ct);
        }
        catch (MongoCommandException ex) when (ex.Code == 85)
        {
            logger.LogWarning("El índice único de ventas sobre {Field} ya existe con otra configuración nominal", field);
        }
        catch (MongoCommandException ex)
        {
            logger.LogError(ex, "No se pudo crear el índice de ventas {Index}: {Code}", name, ex.Code);
        }
    }
}
