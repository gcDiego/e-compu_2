using System.ComponentModel.DataAnnotations;
using Front.Web.Models;

namespace Front.Web.UnitTests;

public sealed class SecurityInputValidationTests
{
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Login_rejects_invalid_email(string email)
    {
        var model = new LoginInputModel(email, "valid-password");

        Assert.False(IsValid(model));
    }

    [Fact]
    public void Registration_rejects_short_password()
    {
        var model = new RegisterViewModel("Name", "Last", "user@example.com", "short");

        Assert.False(IsValid(model));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("abc/../../admin")]
    [InlineData("abc?x=$ne")]
    public void Checkout_rejects_malformed_location_identifier(string locationId)
    {
        var model = new CheckoutInputModel("Valid Name", "+52 555 123 4567", "Valid shipping address", locationId);

        Assert.False(IsValid(model));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("555<script>")]
    public void Checkout_rejects_malformed_phone(string phone)
    {
        var model = new CheckoutInputModel("Valid Name", phone, "Valid shipping address", "location_1");

        Assert.False(IsValid(model));
    }

    [Fact]
    public void Checkout_accepts_expected_shipping_data()
    {
        var model = new CheckoutInputModel("Valid Name", "+52 555 123 4567", "Valid shipping address 123", "location_1");

        Assert.True(IsValid(model));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), [], true);
}
