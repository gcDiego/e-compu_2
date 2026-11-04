namespace Catalog.Api.Services;

public static class ProductImage
{
    // Si el producto no se pudo actualizar, la imagen ya subida se borra para no dejar huérfanos en Cloudinary.
    public static async Task<IResult> ApplyAsync(
        StoredImage uploaded,
        (string? OldPublicId, bool Success) saved,
        Func<string?, CancellationToken, Task> delete,
        CancellationToken ct)
    {
        if (!saved.Success)
        {
            await delete(uploaded.PublicId, ct);
            return Results.NotFound();
        }

        await delete(saved.OldPublicId, ct);
        return Results.NoContent();
    }
}
