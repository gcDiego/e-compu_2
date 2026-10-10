using Admin.Web.Models;
using Admin.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;
public sealed class CatalogController(AdminApiClient api):SecureController
{
    public async Task<IActionResult> Products(CancellationToken ct){var p=api.Products(ct);var c=api.Categories(ct);var b=api.Brands(ct);await Task.WhenAll(p,c,b);return View(new CatalogViewModel(p.Result,c.Result,b.Result));}
    [HttpPost] public async Task<IActionResult> SaveProduct(ProductInput model,CancellationToken ct){if(!ModelState.IsValid){TempData["Error"]="Verifica los datos del producto.";return RedirectToAction("Products");}var r=await api.SaveProduct(model,ct);TempData[r.IsSuccessStatusCode?"Message":"Error"]=r.IsSuccessStatusCode?"Producto guardado.":"No se pudo guardar; puede existir un conflicto de versión.";return RedirectToAction("Products");}
    public async Task<IActionResult> Categories(CancellationToken ct)=>View(await api.Categories(ct));
    [HttpPost] public async Task<IActionResult> SaveCategory(LookupInput model,CancellationToken ct){if(ModelState.IsValid)await api.SaveLookup("categories",model,ct);return RedirectToAction("Categories");}
    public async Task<IActionResult> Brands(CancellationToken ct)=>View(await api.Brands(ct));
    [HttpPost] public async Task<IActionResult> SaveBrand(LookupInput model,CancellationToken ct){if(ModelState.IsValid)await api.SaveLookup("brands",model,ct);return RedirectToAction("Brands");}
}
