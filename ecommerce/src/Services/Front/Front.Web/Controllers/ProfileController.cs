using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Front.Web.Controllers;

public sealed class ProfileController(CustomerApiClient customerApi) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Profile") });

        try
        {
            var customer = await customerApi.GetProfileAsync(token, cancellationToken);
            if (customer is null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Profile") });

            ViewData["Success"] = TempData["Success"] as string;
            return View(new ProfileViewModel(customer.FirstName, customer.LastName, customer.Email, success: TempData["Success"] as string));
        }
        catch (HttpRequestException)
        {
            return View(new ProfileViewModel(error: "No fue posible contactar al servicio de clientes. Intenta más tarde."));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Profile") });

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            model.Email = HttpContext.Session.GetString("CustomerEmail") ?? model.Email;
            model.Error = "El nombre y apellido no pueden estar vacíos ni contener solo espacios.";
            return View(model);
        }

        try
        {
            var (updated, error) = await customerApi.UpdateProfileAsync(token, model.FirstName, model.LastName, cancellationToken);
            if (updated is not null)
            {
                HttpContext.Session.SetString("CustomerName", $"{updated.FirstName} {updated.LastName}".Trim());
                TempData["Success"] = "Perfil actualizado correctamente.";
                return RedirectToAction("Index");
            }

            model.Email = HttpContext.Session.GetString("CustomerEmail") ?? model.Email;
            model.Error = error ?? "No fue posible actualizar el perfil.";
            return View(model);
        }
        catch (HttpRequestException)
        {
            model.Email = HttpContext.Session.GetString("CustomerEmail") ?? model.Email;
            model.Error = "No fue posible contactar al servicio de clientes. Intenta más tarde.";
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var token = AccessToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("ChangePassword", "Profile") });

        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, [FromForm] string confirmPassword, CancellationToken cancellationToken)
    {
        var token = AccessToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("ChangePassword", "Profile") });

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.NewPassword))
        {
            model.Error = "Revisa los datos. La nueva contraseña debe tener entre 5 y 15 caracteres y no puede contener solo espacios.";
            return View(model);
        }

        if (!string.Equals(model.NewPassword, confirmPassword, StringComparison.Ordinal))
        {
            model.Error = "Las contraseñas no coinciden.";
            return View(model);
        }

        try
        {
            var (success, newToken, error) = await customerApi.ChangePasswordAsync(token, model.CurrentPassword, model.NewPassword, cancellationToken);
            if (success)
            {
                if (!string.IsNullOrEmpty(newToken))
                    HttpContext.Session.SetString("AccessToken", newToken);

                HttpContext.Session.Remove("MustResetPassword");
                TempData["Success"] = "Contraseña actualizada correctamente.";
                return RedirectToAction("Index");
            }

            model.Error = error ?? "No fue posible cambiar la contraseña.";
            return View(model);
        }
        catch (HttpRequestException)
        {
            model.Error = "No fue posible contactar al servicio de clientes. Intenta más tarde.";
            return View(model);
        }
    }

    private string? AccessToken() => HttpContext.Session.GetString("AccessToken");
}
