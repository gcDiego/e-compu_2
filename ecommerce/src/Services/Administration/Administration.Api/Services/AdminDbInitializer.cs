using MongoDB.Bson;
using MongoDB.Driver;

namespace Administration.Api.Services;

public sealed class AdminDbInitializer(IMongoDatabase database, ILogger<AdminDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var users = database.GetCollection<BsonDocument>("usuarios");
        try
        {
            await users.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("correo"), new CreateIndexOptions { Unique = true, Name = "ux_usuarios_correo" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("idSqlOriginal"), new CreateIndexOptions { Unique = true, Name = "ux_usuarios_id" })
            }, ct);
        }
        catch (MongoCommandException ex) { logger.LogError("No se pudieron crear índices administrativos: {Code}", ex.Code); }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
