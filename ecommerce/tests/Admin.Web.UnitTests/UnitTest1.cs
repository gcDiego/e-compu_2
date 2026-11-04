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

public sealed class OrderFilterTests
{
    [Fact]
    public void Empty_filter_only_sends_pagination()
    {
        Assert.Equal("page=1&pageSize=50", new OrderFilterInput().Query(50));
    }

    [Fact]
    public void Filters_are_sent_to_the_api()
    {
        var filter = new OrderFilterInput { Page = 2, Status = "enviado", From = new DateTime(2026, 1, 5), To = new DateTime(2026, 2, 10), TransactionId = " TX 1 " };

        var query = filter.Query(50);

        Assert.Contains("page=2", query);
        Assert.Contains("status=enviado", query);
        Assert.Contains("from=2026-01-05", query);
        Assert.Contains("to=2026-02-10", query);
        Assert.Contains("transactionId=TX%201", query);
    }

    [Theory]
    [InlineData("borrado")]
    [InlineData("CONFIRMADO")]
    [InlineData(" ")]
    public void Unknown_status_is_not_forwarded(string status)
    {
        Assert.DoesNotContain("status", new OrderFilterInput { Status = status }.Query(50));
    }

    [Fact]
    public void Pagination_links_keep_the_active_filters()
    {
        var filter = new OrderFilterInput { Page = 3, Status = "entregado" };

        var fields = filter.Fields(2);

        Assert.Equal("2", fields["page"]);
        Assert.Equal("entregado", fields["status"]);
    }

    [Fact]
    public void Page_never_goes_below_one()
    {
        Assert.Equal("1", new OrderFilterInput().Fields(0)["page"]);
    }
}
