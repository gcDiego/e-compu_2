using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class CartController(CartApiClient cartApi) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });

        ViewData["Error"] = TempData["Error"] as string;
        var cart = await cartApi.GetAsync(token, cancellationToken) ?? new CartSnapshotDto([]);
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        var result = await cartApi.AddAsync(token, productId, cancellationToken);
        return result is null ? BadRequest() : RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int productId, bool increase, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        var result = await cartApi.ChangeAsync(token, productId, increase, cancellationToken);
        return result is null ? BadRequest() : RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        await cartApi.RemoveAsync(token, productId, cancellationToken);
        return RedirectToAction("Index");
    }

    private string? AccessToken() => HttpContext.Session.GetString("AccessToken");
}
