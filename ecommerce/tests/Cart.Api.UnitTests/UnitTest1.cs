using Cart.Api.Models;
using Cart.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Cart.Api.UnitTests;

public sealed class CartValidationTests
{
    private static bool IsValid(object model)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Invalid_product_id_is_rejected(int productId)
    {
        Assert.False(IsValid(new AddCartItemRequest(productId)));
    }

    [Fact]
    public void Positive_product_id_is_accepted()
    {
        Assert.True(IsValid(new AddCartItemRequest(1)));
    }
}

public sealed class CustomerIdentityTests
{
    [Fact]
    public void Nameidentifier_claim_returns_customer_id()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "42") }));
        Assert.Equal(42, CustomerIdentity.GetCustomerId(user));
    }

    [Fact]
    public void Sub_claim_returns_customer_id()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "7") }));
        Assert.Equal(7, CustomerIdentity.GetCustomerId(user));
    }

    [Fact]
    public void Missing_or_non_numeric_claim_returns_null()
    {
        Assert.Null(CustomerIdentity.GetCustomerId(new ClaimsPrincipal(new ClaimsIdentity())));
        Assert.Null(CustomerIdentity.GetCustomerId(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "abc") }))));
    }
}
