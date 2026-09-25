using Customer.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Customer.Api.Services;

public sealed class CustomerStore(IMongoDatabase database, CustomerPasswordHasher passwordHasher, MongoSequenceService sequenceService)
{
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");

    public async Task<CustomerProfile?> GetCustomerByEmailAsync(string email, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", email.Trim().ToLowerInvariant());
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<CustomerProfile?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<CustomerProfile?> CreateCustomerAsync(string firstName, string lastName, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await _clientes.Find(Builders<BsonDocument>.Filter.Eq("correo", normalizedEmail)).FirstOrDefaultAsync(ct);
        if (existing is not null) return null;

        var maxIdValue = await _clientes.Find(_ => true)
            .SortByDescending(d => d["idSqlOriginal"])
            .Project(d => d["idSqlOriginal"])
            .FirstOrDefaultAsync(ct);
        var maxId = maxIdValue.IsBsonNull ? 0 : ToInt32(maxIdValue);
        var newId = await sequenceService.NextAsync("clientes.idSqlOriginal", maxId, null, ct);
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["idSqlOriginal"] = newId,
            ["nombres"] = firstName.Trim(),
            ["apellidos"] = lastName.Trim(),
            ["correo"] = normalizedEmail,
            ["clave"] = passwordHasher.Hash(password),
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

    public async Task<CustomerProfile?> VerifyAndRehashAsync(string email, string password, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", email.Trim().ToLowerInvariant());
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null || !doc.TryGetValue("clave", out var hashValue) || !hashValue.IsString) return null;

        var storedHash = hashValue.AsString;
        var result = passwordHasher.Verify(storedHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var optimisticFilter = filter & Builders<BsonDocument>.Filter.Eq("clave", storedHash);
            await _clientes.UpdateOneAsync(optimisticFilter, Builders<BsonDocument>.Update.Set("clave", passwordHasher.Hash(password)), cancellationToken: ct);
        }

        return MapCustomer(doc);
    }

    public async Task<string?> GenerateRecoveryTokenAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var filter = Builders<BsonDocument>.Filter.Eq("correo", normalizedEmail);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        await _clientes.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update
                .Set("recoveryTokenHash", tokenHash)
                .Set("recoveryTokenExpiresAt", DateTime.UtcNow.AddHours(1)),
            cancellationToken: ct);

        return token;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
        var filter = Builders<BsonDocument>.Filter.Eq("correo", normalizedEmail)
            & Builders<BsonDocument>.Filter.Eq("recoveryTokenHash", tokenHash)
            & Builders<BsonDocument>.Filter.Gt("recoveryTokenExpiresAt", DateTime.UtcNow);

        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null) return false;

        await _clientes.UpdateOneAsync(
            filter,
            Builders<BsonDocument>.Update
                .Set("clave", passwordHasher.Hash(newPassword))
                .Set("restablecer", false)
                .Set("recoveryTokenHash", BsonNull.Value)
                .Set("recoveryTokenExpiresAt", BsonNull.Value),
            cancellationToken: ct);

        return true;
    }

    private static CustomerProfile MapCustomer(BsonDocument d) =>
        new(
            ToInt32(d["idSqlOriginal"]),
            d["nombres"].AsString,
            d["apellidos"].AsString,
            d["correo"].AsString,
            "Customer",
            d.Contains("restablecer") && d["restablecer"].AsBoolean);

    private static int ToInt32(BsonValue value) =>
        value.BsonType == BsonType.Int32 ? value.AsInt32 : value.BsonType == BsonType.Int64 ? (int)value.AsInt64 : (int)value.AsDouble;
}
