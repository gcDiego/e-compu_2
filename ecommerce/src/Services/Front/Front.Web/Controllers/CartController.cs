using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class CartController(MongoCartService cartService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var customerId = CustomerId();
        if (customerId is null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });

        ViewData["Error"] = TempData["Error"] as string;
        return View(await cartService.GetAsync(customerId.Value, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, CancellationToken cancellationToken)
    {
        var customerId = CustomerId();
        if (customerId is null) return Unauthorized();
        var result = await cartService.AddAsync(customerId.Value, productId, cancellationToken);
        return result is null ? BadRequest() : RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int productId, bool increase, CancellationToken cancellationToken)
    {
        var customerId = CustomerId();
        if (customerId is null) return Unauthorized();
        var result = await cartService.ChangeAsync(customerId.Value, productId, increase, cancellationToken);
        return result is null ? BadRequest() : RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken cancellationToken)
    {
        var customerId = CustomerId();
        if (customerId is null) return Unauthorized();
        await cartService.RemoveAsync(customerId.Value, productId, cancellationToken);
        return RedirectToAction("Index");
    }

    private int? CustomerId() => HttpContext.Session.GetInt32("CustomerId");
}
