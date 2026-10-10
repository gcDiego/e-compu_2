using MongoDB.Bson;
using MongoDB.Driver;

namespace Catalog.Api.Services;

public sealed class MongoSequenceService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _counters = database.GetCollection<BsonDocument>("counters");
    public async Task<int> NextAsync(string name, int maximum, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<BsonDocument>().AppendStage<BsonDocument, BsonDocument, BsonDocument>(new BsonDocument("$set", new BsonDocument("value", new BsonDocument("$add", new BsonArray { new BsonDocument("$max", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$value", maximum }), maximum }), 1 }))));
        var result = await _counters.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("_id", name), pipeline, new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        return result["value"].ToInt32();
    }
}
