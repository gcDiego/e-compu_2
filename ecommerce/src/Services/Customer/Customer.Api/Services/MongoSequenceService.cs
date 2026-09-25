using MongoDB.Bson;
using MongoDB.Driver;

namespace Customer.Api.Services;

public sealed class MongoSequenceService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _counters = database.GetCollection<BsonDocument>("counters");

    public async Task<int> NextAsync(string name, int existingMaximum, IClientSessionHandle? session, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<BsonDocument>()
            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(new BsonDocument("$set", new BsonDocument("value",
                new BsonDocument("$add", new BsonArray
                {
                    new BsonDocument("$max", new BsonArray
                    {
                        new BsonDocument("$ifNull", new BsonArray { "$value", existingMaximum }),
                        existingMaximum
                    }),
                    1
                }))));

        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };
        var filter = Builders<BsonDocument>.Filter.Eq("_id", name);
        var result = session is null
            ? await _counters.FindOneAndUpdateAsync(filter, pipeline, options, ct)
            : await _counters.FindOneAndUpdateAsync(session, filter, pipeline, options, ct);

        return result["value"].ToInt32();
    }
}
