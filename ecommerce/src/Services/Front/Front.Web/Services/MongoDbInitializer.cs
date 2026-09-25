using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoDbInitializer(IMongoDatabase database, ILogger<MongoDbInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = InitializeAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await EnsureUniqueIndexAsync("clientes", "correo", cancellationToken);
        await EnsureUniqueIndexAsync("clientes", "idSqlOriginal", cancellationToken);
        await EnsureUniqueIndexAsync("usuarios", "correo", cancellationToken);
        await EnsureUniqueIndexAsync("productos", "idSqlOriginal", cancellationToken);
        await EnsureUniqueIndexAsync("categorias", "idSqlOriginal", cancellationToken);
        await EnsureUniqueIndexAsync("marcas", "idSqlOriginal", cancellationToken);
        await EnsureUniqueIndexAsync("ventas", "idSqlOriginal", cancellationToken);
        await EnsureUniqueIndexAsync("ventas", "checkoutKey", cancellationToken);
        await EnsureIndexAsync("carritos", "idClienteSqlOriginal", cancellationToken);
    }

    private async Task EnsureUniqueIndexAsync(string collectionName, string fieldName, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(fieldName);
        var indexModel = new CreateIndexModel<BsonDocument>(
            indexKeys,
            new CreateIndexOptions { Unique = true, Name = $"{fieldName}_unique" });

        try
        {
            await collection.Indexes.CreateOneAsync(indexModel, null, cancellationToken);
            logger.LogInformation("Unique index on {Collection}.{Field} created or already exists", collectionName, fieldName);
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            logger.LogWarning(
                "Cannot create unique index on {Collection}.{Field} because duplicate values already exist",
                collectionName,
                fieldName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to create unique index on {Collection}.{Field}",
                collectionName,
                fieldName);
        }
    }

    private async Task EnsureIndexAsync(string collectionName, string fieldName, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(fieldName);
        var indexModel = new CreateIndexModel<BsonDocument>(
            indexKeys,
            new CreateIndexOptions { Name = $"{fieldName}_index" });

        try
        {
            await collection.Indexes.CreateOneAsync(indexModel, null, cancellationToken);
            logger.LogInformation("Index on {Collection}.{Field} created or already exists", collectionName, fieldName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create index on {Collection}.{Field}", collectionName, fieldName);
        }
    }
}
