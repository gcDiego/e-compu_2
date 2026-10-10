using MongoDB.Bson;
using MongoDB.Driver;

namespace Catalog.Api.Services;

public sealed class CatalogDbInitializer(IMongoDatabase database, ILogger<CatalogDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var name in new[] { "categorias", "marcas", "productos" })
        {
            var collection = database.GetCollection<BsonDocument>(name);
            try
            {
                await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("idSqlOriginal"), new CreateIndexOptions { Unique = true, Name = $"ux_{name}_id" }), cancellationToken: ct);
            }
            catch (MongoCommandException ex) { logger.LogError("No se pudo crear el índice {Index}: {Code}", name, ex.Code); }
        }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
