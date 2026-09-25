using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class OrderController(OrderApiClient orderApi) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (HttpContext.Session.GetInt32("CustomerId") is null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Order") });

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Order") });

        try
        {
            var orders = await orderApi.GetOrdersAsync(token, cancellationToken);
            return View(orders ?? []);
        }
        catch (HttpRequestException)
        {
            ViewData["Error"] = "El servicio de órdenes no está disponible. Intenta más tarde.";
            return View(Array.Empty<OrderDto>());
        }
    }
}
