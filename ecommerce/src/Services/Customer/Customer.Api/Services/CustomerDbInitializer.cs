using MongoDB.Bson;
using MongoDB.Driver;

namespace Customer.Api.Services;

public sealed class CustomerDbInitializer(IMongoDatabase database, ILogger<CustomerDbInitializer> logger) : IHostedService
{
    public static readonly (string Field, string Name)[] UniqueIndexes =
    [
        ("correo", "ux_clientes_correo"),
        ("idSqlOriginal", "ux_clientes_id")
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        var clientes = database.GetCollection<BsonDocument>("clientes");
        foreach (var (field, name) in UniqueIndexes)
            await EnsureUniqueIndexAsync(clientes, field, name, ct);
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
        CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(ct);
        if (HasEquivalentUniqueIndex(await cursor.ToListAsync(ct), field))
        {
            logger.LogInformation("El índice único de clientes sobre {Field} ya existe", field);
            return;
        }

        try
        {
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(field),
                    new CreateIndexOptions { Unique = true, Name = name }),
                cancellationToken: ct);
        }
        catch (MongoCommandException ex) when (ex.Code == 85)
        {
            logger.LogWarning("El índice único de clientes sobre {Field} ya existe con otra configuración nominal", field);
        }
        catch (MongoCommandException ex)
        {
            logger.LogError(ex, "No se pudo crear el índice de clientes {Index}: {Code}", name, ex.Code);
        }
    }
}
