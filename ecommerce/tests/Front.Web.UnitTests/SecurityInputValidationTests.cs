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

    [Theory]
    [InlineData("four")]
    [InlineData("sixteen-chars-long")]
    public void Registration_rejects_password_outside_length_limits(string password)
    {
        var model = new RegisterViewModel("Name", "Last", "user@example.com", password);

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

    [Fact]
    public void Profile_rejects_empty_first_name()
    {
        var model = new ProfileViewModel("", "Last", "user@example.com");

        Assert.False(IsValid(model));
    }

    [Theory]
    [InlineData("   ", "Last")]
    [InlineData("First", "   ")]
    public void Profile_rejects_whitespace_names(string first, string last)
    {
        var model = new ProfileViewModel(first, last, "user@example.com");

        Assert.False(IsValid(model));
    }

    [Fact]
    public void Profile_accepts_valid_names()
    {
        var model = new ProfileViewModel("Name", "Last", "user@example.com");

        Assert.True(IsValid(model));
    }

    [Theory]
    [InlineData("four")]
    [InlineData("sixteen-chars-long")]
    public void ChangePassword_rejects_password_outside_length_limits(string password)
    {
        var model = new ChangePasswordViewModel { CurrentPassword = "current", NewPassword = password };

        Assert.False(IsValid(model));
    }

    [Theory]
    [InlineData("     ")]
    [InlineData("        ")]
    public void ChangePassword_rejects_whitespace_new_password(string password)
    {
        var model = new ChangePasswordViewModel { CurrentPassword = "current", NewPassword = password };

        Assert.False(IsValid(model));
    }

    [Fact]
    public void ChangePassword_accepts_valid_password()
    {
        var model = new ChangePasswordViewModel { CurrentPassword = "current", NewPassword = "valid1" };

        Assert.True(IsValid(model));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), [], true);
}
