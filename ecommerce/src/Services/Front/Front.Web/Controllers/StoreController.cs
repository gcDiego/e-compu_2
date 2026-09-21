using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class StoreController(CatalogApiClient catalogApi) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId, int? brandId, CancellationToken cancellationToken)
    {
        var categoriesTask = catalogApi.GetCategoriesAsync(cancellationToken);
        var brandsTask = catalogApi.GetBrandsAsync(categoryId, cancellationToken);
        var productsTask = catalogApi.GetProductsAsync(categoryId, brandId, cancellationToken);
        await Task.WhenAll(categoriesTask, brandsTask, productsTask);
        return View(new CatalogViewModel(categoriesTask.Result, brandsTask.Result, productsTask.Result, categoryId, brandId));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var product = await catalogApi.GetProductAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
