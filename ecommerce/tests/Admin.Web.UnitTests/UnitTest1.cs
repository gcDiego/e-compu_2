using Admin.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace Admin.Web.UnitTests;

public sealed class InputValidationTests
{
    private static bool IsValid(object model)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    }

    [Fact]
    public void Valid_product_input_is_accepted()
    {
        var model = new ProductInput { Name = "Producto", Description = "Descripcion", BrandId = 1, CategoryId = 1, Price = 10m, Stock = 5 };
        Assert.True(IsValid(model));
    }

    [Theory]
    [InlineData(0, 1, 10, 5)]
    [InlineData(1, 0, 10, 5)]
    [InlineData(1, 1, 0, 5)]
    [InlineData(1, 1, 10, -1)]
    public void Invalid_product_input_is_rejected(int brandId, int categoryId, decimal price, int stock)
    {
        var model = new ProductInput { Name = "Producto", Description = "Descripcion", BrandId = brandId, CategoryId = categoryId, Price = price, Stock = stock };
        Assert.False(IsValid(model));
    }

    [Fact]
    public void Short_reset_password_is_rejected()
    {
        var model = new ResetModel { Email = "a@b.com", Token = "t", NewPassword = "four" };
        Assert.False(IsValid(model));
    }

    [Fact]
    public void Invalid_login_email_is_rejected()
    {
        var model = new LoginModel { Email = "not-an-email", Password = "x" };
        Assert.False(IsValid(model));
    }
}
