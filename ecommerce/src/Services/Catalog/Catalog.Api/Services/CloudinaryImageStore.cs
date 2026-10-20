using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Catalog.Api.Services;

public sealed record StoredImage(string Url, string PublicId);
public sealed class CloudinaryImageStore
{
    private readonly Cloudinary? _cloudinary;
    public CloudinaryImageStore(IConfiguration config)
    {
        var cloud = config["Cloudinary:CloudName"]; var key = config["Cloudinary:ApiKey"]; var secret = config["Cloudinary:ApiSecret"];
        if (!string.IsNullOrWhiteSpace(cloud) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret)) _cloudinary = new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
    }
    public async Task<StoredImage> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 5_242_880) throw new InvalidDataException("La imagen debe medir menos de 5 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[12]; var read = await stream.ReadAsync(header, ct); stream.Position = 0;
        var jpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = read >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var webp = read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        if (!(jpeg || png || webp)) throw new InvalidDataException("Formato de imagen no permitido.");
        if (_cloudinary is null) throw new InvalidOperationException("Cloudinary is not configured.");
        var result = await _cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription("product", stream), PublicId = $"ecommerce/products/{Guid.NewGuid():N}", UniqueFilename = true, Overwrite = false }, ct);
        if (result.Error is not null) throw new InvalidOperationException("No se pudo almacenar la imagen.");
        return new StoredImage(result.SecureUrl.ToString(), result.PublicId);
    }
    public async Task DeleteAsync(string? publicId, CancellationToken ct) { if (_cloudinary is not null && !string.IsNullOrWhiteSpace(publicId)) await _cloudinary.DestroyAsync(new DeletionParams(publicId)); }
}
