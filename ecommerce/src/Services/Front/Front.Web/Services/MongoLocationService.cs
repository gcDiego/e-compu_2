using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Front.Web.Services;

public sealed class MongoLocationService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _estados = database.GetCollection<BsonDocument>("estados");

    public async Task<IReadOnlyList<StateDto>> GetStatesAsync(CancellationToken ct)
    {
        var docs = await _estados.Find(_ => true).SortBy(d => d["descripcion"]).ToListAsync(ct);
        return docs.Select(d => new StateDto(d["idEstado"].AsString, d["descripcion"].AsString)).ToList();
    }

    public async Task<IReadOnlyList<MunicipalityDto>> GetMunicipalitiesByStateAsync(string stateId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idEstado", stateId);
        var doc = await _estados.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return [];

        var municipios = doc["municipios"].AsBsonArray;
        return municipios
            .Cast<BsonDocument>()
            .Select(m => new MunicipalityDto(m["idMunicipio"].AsString, m["descripcion"].AsString))
            .ToList();
    }

    public async Task<IReadOnlyList<LocalityDto>> GetLocalitiesByMunicipalityAsync(string municipalityId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("municipios.idMunicipio", municipalityId);
        var doc = await _estados.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return [];

        var municipio = doc["municipios"].AsBsonArray
            .Cast<BsonDocument>()
            .FirstOrDefault(m => m["idMunicipio"].AsString == municipalityId);
        if (municipio is null) return [];

        var localidades = municipio["localidades"].AsBsonArray;
        return localidades
            .Cast<BsonDocument>()
            .Select(l => new LocalityDto(l["idLocalidad"].AsString, l["descripcion"].AsString))
            .ToList();
    }
}
