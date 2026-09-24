using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class CheckoutController(OrderApiClient orderApi) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CheckoutInputModel input, CancellationToken cancellationToken)
    {
        var customerId = HttpContext.Session.GetInt32("CustomerId");
        if (customerId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest("Los datos de envío no son válidos.");

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();

        try
        {
            var (order, error) = await orderApi.CreateOrderAsync(token, input, cancellationToken);
            if (order is not null)
                return RedirectToAction("Index", "Order");

            TempData["Error"] = error ?? "No se pudo crear la orden. Verifica tu carrito y productos.";
            return RedirectToAction("Index", "Cart");
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "El servicio de órdenes no está disponible. Intenta más tarde.";
            return RedirectToAction("Index", "Cart");
        }
    }
}
