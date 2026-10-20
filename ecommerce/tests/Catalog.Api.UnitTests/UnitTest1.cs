using Catalog.Api.Models;
using Catalog.Api.Services;
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
}
