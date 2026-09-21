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
        return View(await cartApi.GetAsync(token, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        var response = await cartApi.AddAsync(productId, token, cancellationToken);
        return response.IsSuccessStatusCode ? RedirectToAction("Index") : StatusCode((int)response.StatusCode);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int productId, bool increase, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        await cartApi.ChangeAsync(productId, increase, token, cancellationToken);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null) return Unauthorized();
        await cartApi.RemoveAsync(productId, token, cancellationToken);
        return RedirectToAction("Index");
    }

    private string? AccessToken() => HttpContext.Session.GetString("AccessToken");
}
