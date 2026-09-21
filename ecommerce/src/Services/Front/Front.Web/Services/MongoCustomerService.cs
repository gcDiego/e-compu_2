using Front.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Front.Web.Services;

public sealed class MongoCustomerService(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");
    private readonly IMongoCollection<BsonDocument> _usuarios = database.GetCollection<BsonDocument>("usuarios");

    public async Task<CustomerDto?> GetCustomerByEmailAsync(string email, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", email);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<CustomerDto?> UpdateCustomerAsync(int id, string firstName, string lastName, string email, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id);
        var update = Builders<BsonDocument>.Update
            .Set("nombres", firstName)
            .Set("apellidos", lastName)
            .Set("correo", email);
        var doc = await _clientes.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, ct);
        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<CustomerDto?> CreateCustomerAsync(string firstName, string lastName, string email, string password, CancellationToken ct)
    {
        var existing = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("correo", email)).FirstOrDefaultAsync(ct);
        if (existing is not null) return null;

        var maxId = await _clientes.Find(_ => true)
            .SortByDescending(d => d["idSqlOriginal"])
            .Project(d => d["idSqlOriginal"])
            .FirstOrDefaultAsync(ct);

        int newId = (maxId.IsInt32 ? maxId.AsInt32 : 0) + 1;
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["idSqlOriginal"] = newId,
            ["nombres"] = firstName,
            ["apellidos"] = lastName,
            ["correo"] = email,
            ["clave"] = HashPassword(password),
            ["restablecer"] = false,
            ["fechaRegistro"] = DateTime.UtcNow,
            ["wallet"] = new BsonDocument
            {
                ["privateKey"] = BsonNull.Value,
                ["keyId"] = BsonNull.Value,
                ["clientWalletAddressUrl"] = BsonNull.Value,
                ["sendingWalletAddressUrl"] = BsonNull.Value
            }
        };
        await _clientes.InsertOneAsync(doc, cancellationToken: ct);
        return MapCustomer(doc);
    }

    public async Task<bool> VerifyCustomerPasswordAsync(string email, string password, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", email);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return false;
        return doc["clave"].AsString.Equals(HashPassword(password), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> VerifyAdminPasswordAsync(string email, string password, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", email);
        var doc = await _usuarios.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return false;
        return doc["clave"].AsString.Equals(HashPassword(password), StringComparison.OrdinalIgnoreCase);
    }

    private static CustomerDto MapCustomer(BsonDocument d) =>
        new(
            ToInt32(d["idSqlOriginal"]),
            d["nombres"].AsString,
            d["apellidos"].AsString,
            d["correo"].AsString,
            d.Contains("restablecer") && d["restablecer"].AsBoolean);

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;

    private static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
