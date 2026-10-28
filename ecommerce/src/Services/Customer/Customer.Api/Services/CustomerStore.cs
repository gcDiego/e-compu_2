using Customer.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Customer.Api.Services;

public sealed class CustomerStore(IMongoDatabase database, CustomerPasswordHasher passwordHasher, MongoSequenceService sequenceService)
{
    private readonly IMongoCollection<BsonDocument> _clientes = database.GetCollection<BsonDocument>("clientes");
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedAccessAttempts = 5;

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
            ["failedLoginCount"] = 0,
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

    public async Task<CustomerAuthResult> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var filter = Builders<BsonDocument>.Filter.Eq("correo", normalizedEmail);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null)
            return CustomerAuthResult.Failed();

        if (IsLocked(doc))
            return CustomerAuthResult.Locked();

        if (!doc.TryGetValue("clave", out var hashValue) || !hashValue.IsString)
            return CustomerAuthResult.Failed();

        var storedHash = hashValue.AsString;
        var result = passwordHasher.Verify(storedHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            var attempts = GetInt(doc, "failedLoginCount") + 1;
            var update = Builders<BsonDocument>.Update.Set("failedLoginCount", attempts);
            if (attempts >= MaxFailedAccessAttempts)
                update = update.Set("lockoutEnd", DateTime.UtcNow.Add(LockoutDuration));

            await _clientes.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), update, cancellationToken: ct);
            return attempts >= MaxFailedAccessAttempts ? CustomerAuthResult.Locked() : CustomerAuthResult.Failed();
        }

        var successUpdate = Builders<BsonDocument>.Update
            .Set("failedLoginCount", 0)
            .Unset("lockoutEnd")
            .Set("ultimoAcceso", DateTime.UtcNow);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            successUpdate = successUpdate.Set("clave", passwordHasher.Hash(password));
        }

        await _clientes.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), successUpdate, cancellationToken: ct);
        return CustomerAuthResult.Success(MapCustomer(doc));
    }

    public async Task<CustomerProfile?> VerifyAndRehashAsync(string email, string password, CancellationToken ct)
    {
        var result = await AuthenticateAsync(email, password, ct);
        return result.Customer;
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
                .Set("failedLoginCount", 0)
                .Unset("lockoutEnd")
                .Set("recoveryTokenHash", BsonNull.Value)
                .Set("recoveryTokenExpiresAt", BsonNull.Value),
            cancellationToken: ct);

        return true;
    }

    public Task<long> CountAsync(CancellationToken ct) =>
        _clientes.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: ct);

    public async Task<CustomerProfile?> UpdateProfileAsync(int id, string firstName, string lastName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return null;

        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id);
        var update = Builders<BsonDocument>.Update
            .Set("nombres", firstName.Trim())
            .Set("apellidos", lastName.Trim());

        var doc = await _clientes.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            ct);

        return doc is null ? null : MapCustomer(doc);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(int id, string currentPassword, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return new ChangePasswordResult(ChangePasswordStatus.InvalidCurrentPassword);

        var filter = Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id);
        var doc = await _clientes.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null || !doc.TryGetValue("clave", out var hashValue) || !hashValue.IsString)
            return new ChangePasswordResult(ChangePasswordStatus.NotFound);

        var storedHash = hashValue.AsString;
        if (passwordHasher.Verify(storedHash, currentPassword) == PasswordVerificationResult.Failed)
            return new ChangePasswordResult(ChangePasswordStatus.InvalidCurrentPassword);

        var optimisticFilter = filter & Builders<BsonDocument>.Filter.Eq("clave", storedHash);
        var updatedDoc = await _clientes.FindOneAndUpdateAsync(
            optimisticFilter,
            Builders<BsonDocument>.Update
                .Set("clave", passwordHasher.Hash(newPassword))
                .Set("restablecer", false)
                .Set("failedLoginCount", 0)
                .Unset("lockoutEnd"),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            ct);

        if (updatedDoc is null)
            return new ChangePasswordResult(ChangePasswordStatus.ConcurrencyConflict);

        return new ChangePasswordResult(ChangePasswordStatus.Success, MapCustomer(updatedDoc));
    }

    private static bool IsLocked(BsonDocument d) =>
        d.TryGetValue("lockoutEnd", out var value) && value.IsValidDateTime && value.ToUniversalTime() > DateTime.UtcNow;

    private static int GetInt(BsonDocument d, string key) =>
        d.TryGetValue(key, out var v) && !v.IsBsonNull ? (v.IsInt32 ? v.AsInt32 : (int)v.AsInt64) : 0;

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
