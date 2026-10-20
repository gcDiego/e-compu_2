using Catalog.Api.Models;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.UnitTests;

public sealed class InputValidationTests
{
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(0, 10)]
    public void Invalid_price_or_stock_is_rejected(decimal price,int stock)
    {
        var model=new SaveProductRequest("Product","Description",1,1,price,stock,true,0);
        var results=new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model,new ValidationContext(model),results,true));
    }

    [Fact] public void No_dynamic_filter_fields_exist_on_catalog_contract()
    {
        var names=typeof(SaveProductRequest).GetProperties().Select(x=>x.Name).ToArray();
        Assert.DoesNotContain(names,x=>x.StartsWith('$')||x.Equals("filter",StringComparison.OrdinalIgnoreCase)||x.Equals("query",StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void Equivalent_unique_index_is_detected_regardless_of_name()
    {
        var indexes=new[]{BsonDocument.Parse("{ key: { idSqlOriginal: 1 }, name: 'idSqlOriginal_unique', unique: true }")};
        Assert.True(CatalogDbInitializer.HasEquivalentUniqueIndex(indexes,"idSqlOriginal"));
    }

    [Fact] public void Index_with_different_direction_is_not_equivalent()
    {
        var indexes=new[]{BsonDocument.Parse("{ key: { idSqlOriginal: -1 }, name: 'idSqlOriginal_desc', unique: true }")};
        Assert.False(CatalogDbInitializer.HasEquivalentUniqueIndex(indexes,"idSqlOriginal"));
    }

    [Fact]
    public async Task Svg_is_rejected_before_cloudinary_is_called()
    {
        var store = new CloudinaryImageStore(new ConfigurationBuilder().Build());
        var bytes = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "image.svg");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.UploadAsync(file, CancellationToken.None));
        Assert.Equal("Formato de imagen no permitido.", error.Message);
    }

    [Fact]
    public async Task File_over_five_megabytes_is_rejected_before_cloudinary_is_called()
    {
        var store = new CloudinaryImageStore(new ConfigurationBuilder().Build());
        var file = new FormFile(Stream.Null, 0, 5_242_881, "image", "large.png");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.UploadAsync(file, CancellationToken.None));
        Assert.Equal("La imagen debe medir menos de 5 MB.", error.Message);
    }

    [Fact]
    public async Task Valid_png_requires_cloudinary_configuration()
    {
        var store = new CloudinaryImageStore(new ConfigurationBuilder().Build());
        byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "image.png");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.UploadAsync(file, CancellationToken.None));
        Assert.Equal("Cloudinary is not configured.", error.Message);
    }
}
