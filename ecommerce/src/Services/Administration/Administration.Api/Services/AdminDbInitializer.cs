using MongoDB.Bson;
using MongoDB.Driver;

namespace Administration.Api.Services;

public sealed class AdminDbInitializer(IMongoDatabase database, ILogger<AdminDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var users = database.GetCollection<BsonDocument>("usuarios");
        await EnsureUniqueIndexAsync(users, "correo", "ux_usuarios_correo", ct);
        await EnsureUniqueIndexAsync(users, "idSqlOriginal", "ux_usuarios_id", ct);
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
            logger.LogInformation("El índice único de usuarios sobre {Field} ya existe", field);
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
            logger.LogWarning("El índice único de usuarios sobre {Field} ya existe con otra configuración nominal", field);
        }
        catch (MongoCommandException ex)
        {
            logger.LogError(ex, "No se pudo crear el índice administrativo {Index}: {Code}", name, ex.Code);
        }
    }
}
