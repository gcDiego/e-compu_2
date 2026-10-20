using Administration.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Administration.Api.Services;

public sealed class AdminStore(IMongoDatabase database, AdminPasswordHasher hasher, MongoSequenceService sequence)
{
    private readonly IMongoCollection<BsonDocument> _users = database.GetCollection<BsonDocument>("usuarios");
    private readonly IMongoCollection<BsonDocument> _audit = database.GetCollection<BsonDocument>("auditoria");
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AdminProfile?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var filter = Builders<BsonDocument>.Filter.Eq("correo", normalized);
        var doc = await _users.Find(filter).FirstOrDefaultAsync(ct);
        if (doc is null || !GetBool(doc, "activo", true) || IsLocked(doc)) return null;
        var result = hasher.Verify(GetString(doc, "clave"), password);
        if (result == PasswordVerificationResult.Failed)
        {
            var attempts = GetInt(doc, "failedLoginCount") + 1;
            var update = Builders<BsonDocument>.Update.Set("failedLoginCount", attempts);
            if (attempts >= 5) update = update.Set("lockoutEnd", DateTime.UtcNow.Add(LockoutDuration));
            await _users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), update, cancellationToken: ct);
            return null;
        }
        var success = Builders<BsonDocument>.Update.Set("failedLoginCount", 0).Unset("lockoutEnd").Set("ultimoAcceso", DateTime.UtcNow);
        if (result == PasswordVerificationResult.SuccessRehashNeeded) success = success.Set("clave", hasher.Hash(password));
        await _users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), success, cancellationToken: ct);
        return Map(doc);
    }

    public async Task<IReadOnlyList<AdminProfile>> ListAsync(CancellationToken ct) =>
        (await _users.Find(FilterDefinition<BsonDocument>.Empty).SortBy(x => x["nombres"]).ToListAsync(ct)).Select(Map).ToList();

    public async Task<AdminCreation?> CreateAsync(SaveAdminRequest request, int actorId, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await _users.Find(Builders<BsonDocument>.Filter.Eq("correo", email)).AnyAsync(ct)) return null;
        var maximum = await _users.Find(FilterDefinition<BsonDocument>.Empty).SortByDescending(x => x["idSqlOriginal"]).Project(x => x["idSqlOriginal"]).FirstOrDefaultAsync(ct);
        var id = await sequence.NextAsync("usuarios.idSqlOriginal", maximum?.ToInt32() ?? 0, ct);
        var token = CreateToken();
        var doc = new BsonDocument { ["_id"] = ObjectId.GenerateNewId(), ["idSqlOriginal"] = id, ["nombres"] = request.FirstName.Trim(), ["apellidos"] = request.LastName.Trim(), ["correo"] = email, ["clave"] = hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))), ["activo"] = request.IsActive, ["restablecer"] = true, ["recoveryTokenHash"] = HashToken(token), ["recoveryTokenExpiresAt"] = DateTime.UtcNow.AddHours(24), ["fechaRegistro"] = DateTime.UtcNow, ["failedLoginCount"] = 0 };
        try { await _users.InsertOneAsync(doc, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000) { return null; }
        await AuditAsync(actorId, "create", "usuario", id, ct);
        return new AdminCreation(Map(doc), token);
    }

    public async Task<AdminProfile?> UpdateAsync(int id, SaveAdminRequest request, int actorId, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var duplicate = Builders<BsonDocument>.Filter.Eq("correo", email) & Builders<BsonDocument>.Filter.Ne("idSqlOriginal", id);
        if (await _users.Find(duplicate).AnyAsync(ct)) return null;
        var update = Builders<BsonDocument>.Update.Set("nombres", request.FirstName.Trim()).Set("apellidos", request.LastName.Trim()).Set("correo", email).Set("activo", request.IsActive).Set("fechaModificacion", DateTime.UtcNow);
        var doc = await _users.FindOneAndUpdateAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id), update, new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, ct);
        if (doc is not null) await AuditAsync(actorId, "update", "usuario", id, ct);
        return doc is null ? null : Map(doc);
    }

    public async Task<bool> SetActiveAsync(int id, bool active, int actorId, CancellationToken ct)
    {
        if (id == actorId && !active) return false;
        var result = await _users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("idSqlOriginal", id), Builders<BsonDocument>.Update.Set("activo", active).Set("fechaModificacion", DateTime.UtcNow), cancellationToken: ct);
        if (result.ModifiedCount > 0) await AuditAsync(actorId, active ? "activate" : "deactivate", "usuario", id, ct);
        return result.ModifiedCount > 0;
    }

    public async Task<string?> GenerateRecoveryTokenAsync(string email, CancellationToken ct)
    {
        var token = CreateToken();
        var result = await _users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("correo", NormalizeEmail(email)) & Builders<BsonDocument>.Filter.Eq("activo", true), Builders<BsonDocument>.Update.Set("recoveryTokenHash", HashToken(token)).Set("recoveryTokenExpiresAt", DateTime.UtcNow.AddHours(1)), cancellationToken: ct);
        return result.ModifiedCount == 0 ? null : token;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string password, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("correo", NormalizeEmail(email)) & Builders<BsonDocument>.Filter.Eq("recoveryTokenHash", HashToken(token.Trim())) & Builders<BsonDocument>.Filter.Gt("recoveryTokenExpiresAt", DateTime.UtcNow);
        var update = Builders<BsonDocument>.Update.Set("clave", hasher.Hash(password)).Set("restablecer", false).Set("failedLoginCount", 0).Unset("lockoutEnd").Unset("recoveryTokenHash").Unset("recoveryTokenExpiresAt");
        return (await _users.UpdateOneAsync(filter, update, cancellationToken: ct)).ModifiedCount == 1;
    }

    private async Task AuditAsync(int actorId, string action, string entity, int entityId, CancellationToken ct) => await _audit.InsertOneAsync(new BsonDocument { ["actorId"] = actorId, ["action"] = action, ["entity"] = entity, ["entityId"] = entityId, ["timestamp"] = DateTime.UtcNow }, cancellationToken: ct);
    private static AdminProfile Map(BsonDocument d) => new(GetInt(d, "idSqlOriginal"), GetString(d, "nombres"), GetString(d, "apellidos"), GetString(d, "correo"), GetBool(d, "activo", true));
    private static bool IsLocked(BsonDocument d) => d.TryGetValue("lockoutEnd", out var value) && value.IsValidDateTime && value.ToUniversalTime() > DateTime.UtcNow;
    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static string HashToken(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string GetString(BsonDocument d, string name) => d.TryGetValue(name, out var value) && value.IsString ? value.AsString : "";
    private static int GetInt(BsonDocument d, string name) => d.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : 0;
    private static bool GetBool(BsonDocument d, string name, bool fallback) => d.TryGetValue(name, out var value) && value.IsBoolean ? value.AsBoolean : fallback;
}
