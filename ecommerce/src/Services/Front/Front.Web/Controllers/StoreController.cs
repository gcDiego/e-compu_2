using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class StoreController(MongoCatalogService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId, int? brandId, CancellationToken cancellationToken)
    {
        var categoriesTask = catalog.GetCategoriesAsync(cancellationToken);
        var brandsTask = catalog.GetBrandsAsync(categoryId, cancellationToken);
        var productsTask = catalog.GetProductsAsync(categoryId, brandId, cancellationToken);
        await Task.WhenAll(categoriesTask, brandsTask, productsTask);
        return View(new CatalogViewModel(categoriesTask.Result, brandsTask.Result, productsTask.Result, categoryId, brandId));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var product = await catalog.GetProductAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
