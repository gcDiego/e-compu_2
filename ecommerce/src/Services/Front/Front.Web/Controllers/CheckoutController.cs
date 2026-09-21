using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class CheckoutController(MongoOrderService orderService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string contacto, string telefono, string direccion, string idLocalidad, CancellationToken cancellationToken)
    {
        var customerId = HttpContext.Session.GetInt32("CustomerId");
        if (customerId is null) return Unauthorized();

        var order = await orderService.CreateOrderAsync(customerId.Value, contacto, telefono, direccion, idLocalidad, cancellationToken);
        if (order is null) return BadRequest("No se pudo crear la orden. Verifica tu carrito y productos.");

        return RedirectToAction("Index", "Store");
    }
}
