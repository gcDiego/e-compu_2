using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class CheckoutController(OrderApiClient orderApi, CartApiClient cartApi, PayPalClient payPal) : Controller
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayPalOrder(CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();

        var cart = await cartApi.GetAsync(token, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
            return BadRequest(new { error = "El carrito está vacío." });

        var (orderId, error) = await payPal.CreateOrderAsync(cart.Total, "MXN", cancellationToken);
        if (orderId is null)
            return BadRequest(new { error = error ?? "No se pudo generar la orden en PayPal Sandbox." });

        return Ok(new { orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapturePayPalOrder([FromBody] CapturePayPalRequest request, CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();

        if (request is null || string.IsNullOrWhiteSpace(request.PayPalOrderId) || request.Shipping is null)
            return BadRequest(new { error = "Datos incompletos para procesar el pago con PayPal." });

        if (!TryValidateModel(request.Shipping))
            return BadRequest(new { error = "Los datos de envío no son válidos." });

        var (txId, status, captureError) = await payPal.CaptureOrderAsync(request.PayPalOrderId, cancellationToken);
        if (captureError is not null)
            return BadRequest(new { error = captureError });

        request.Shipping.IdTransaccion = txId ?? request.PayPalOrderId;
        var (order, orderError) = await orderApi.CreateOrderAsync(token, request.Shipping, cancellationToken);
        if (order is null)
            return BadRequest(new { error = orderError ?? "El pago fue aprobado pero ocurrió un error al confirmar la orden." });

        return Ok(new { success = true, redirectUrl = Url.Action("Index", "Order") });
    }
}

public sealed class CapturePayPalRequest
{
    public string PayPalOrderId { get; set; } = "";
    public CheckoutInputModel Shipping { get; set; } = new();
}
