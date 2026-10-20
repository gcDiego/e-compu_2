using Location.Api.Services;

namespace Location.Api.UnitTests;

public sealed class LocationIdValidatorTests
{
    [Theory]
    [InlineData("01")]
    [InlineData("loc_1-A")]
    [InlineData("a1-b2_C3")]
    public void Valid_ids_are_accepted(string id)
    {
        Assert.True(LocationIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("$ne")]
    [InlineData("id con espacios")]
    [InlineData("id.con.puntos")]
    public void Invalid_ids_are_rejected(string? id)
    {
        Assert.False(LocationIdValidator.IsValid(id));
    }

    [Fact]
    public void Ids_longer_than_40_chars_are_rejected()
    {
        Assert.False(LocationIdValidator.IsValid(new string('a', 41)));
    }
}
