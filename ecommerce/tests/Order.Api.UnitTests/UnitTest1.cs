using MongoDB.Driver;
using Order.Api.Models;
using Order.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Order.Api.UnitTests;

public sealed class CheckoutValidationTests
{
    private static bool IsValid(CheckoutRequest model)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    }

    [Fact]
    public void Valid_checkout_is_accepted()
    {
        Assert.True(IsValid(new CheckoutRequest("Juan Perez", "5512345678", "Calle 123, Colonia Centro", "loc1")));
    }

    [Theory]
    [InlineData("", "5512345678", "Calle 123, Colonia Centro", "loc1")]
    [InlineData("Juan Perez", "abc", "Calle 123, Colonia Centro", "loc1")]
    [InlineData("Juan Perez", "5512345678", "Ca", "loc1")]
    [InlineData("Juan Perez", "5512345678", "Calle 123, Colonia Centro", "")]
    public void Invalid_checkout_is_rejected(string contacto, string telefono, string direccion, string localidad)
    {
        Assert.False(IsValid(new CheckoutRequest(contacto, telefono, direccion, localidad)));
    }
}

public sealed class CustomerIdClaimTests
{
    private static OrderStore CreateStore()
    {
        var client = new MongoClient("mongodb://localhost:1");
        var db = client.GetDatabase("test");
        return new OrderStore(db, client, new MongoSequenceService(db));
    }

    [Fact]
    public void Nameidentifier_claim_returns_customer_id()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "42") }));
        Assert.Equal(42, CreateStore().GetCustomerId(user));
    }

    [Fact]
    public void Sub_claim_returns_customer_id()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "7") }));
        Assert.Equal(7, CreateStore().GetCustomerId(user));
    }

    [Fact]
    public void Missing_or_non_numeric_claim_returns_null()
    {
        var store = CreateStore();
        Assert.Null(store.GetCustomerId(new ClaimsPrincipal(new ClaimsIdentity())));
        Assert.Null(store.GetCustomerId(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "abc") }))));
    }
}

public sealed class AdminPaginationTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -10, 1, 1)]
    [InlineData(2, 25, 2, 25)]
    [InlineData(3, 500, 3, 100)]
    [InlineData(1, 101, 1, 100)]
    public void Page_and_page_size_are_clamped(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (actualPage, actualPageSize) = OrderPagination.Normalize(page, pageSize);
        Assert.Equal(expectedPage, actualPage);
        Assert.Equal(expectedPageSize, actualPageSize);
    }

    [Fact]
    public void Maximum_page_size_is_capped()
    {
        Assert.Equal(100, OrderPagination.MaxPageSize);
    }
}
