using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Front.Web.Controllers;

public sealed class AccountController(CustomerApiClient customerApi) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["Success"] = TempData["Success"] as string;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginInputModel input, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid)
            return View(new LoginViewModel(input.Email, "Correo o contraseña incorrectos."));

        try
        {
            var apiLogin = await customerApi.LoginAsync(input.Email, input.Password, cancellationToken);
            if (apiLogin is not null)
            {
                var apiCustomer = new CustomerDto(apiLogin.Id, apiLogin.FirstName, apiLogin.LastName, apiLogin.Email, apiLogin.MustResetPassword);
                SetSession(apiCustomer, apiLogin.AccessToken);
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }

            return View(new LoginViewModel(input.Email, "Correo o contraseña incorrectos."));
        }
        catch (HttpRequestException)
        {
            return View(new LoginViewModel(input.Email, "No fue posible contactar al servicio de identidad. Intenta más tarde."));
        }
    }

    private void SetSession(CustomerDto customer, string token)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetString("AccessToken", token);
        HttpContext.Session.SetString("CustomerName", $"{customer.FirstName} {customer.LastName}".Trim());
        HttpContext.Session.SetString("CustomerEmail", customer.Email);
        HttpContext.Session.SetInt32("CustomerId", customer.Id);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel model, [FromForm] string confirmPassword, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid)
            return View(new RegisterViewModel(model.FirstName, model.LastName, model.Email, string.Empty, "Revisa los datos. La contraseña debe tener entre 12 y 128 caracteres."));

        if (!string.Equals(model.Password, confirmPassword, StringComparison.Ordinal))
            return View(new RegisterViewModel(model.FirstName, model.LastName, model.Email, string.Empty, "Las contraseñas no coinciden."));

        try
        {
            var created = await customerApi.RegisterAsync(model.FirstName, model.LastName, model.Email, model.Password, cancellationToken);
            if (created is not null)
                return RedirectToAction("Login", new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

            return View(new RegisterViewModel(model.FirstName, model.LastName, model.Email, string.Empty, "No fue posible crear la cuenta con esos datos."));
        }
        catch (HttpRequestException)
        {
            return View(new RegisterViewModel(model.FirstName, model.LastName, model.Email, string.Empty, "No fue posible contactar al servicio de identidad. Intenta más tarde."));
        }
    }

    [HttpGet]
    public IActionResult Recover(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Recover(string email, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        try
        {
            var token = await customerApi.RecoverPasswordAsync(email, cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Reset", new { email, token, returnUrl });

            return RedirectToAction("Reset", new { email, returnUrl });
        }
        catch (HttpRequestException)
        {
            return View("Login", new LoginViewModel(email, "No fue posible contactar al servicio de identidad. Intenta más tarde."));
        }
    }

    [HttpGet]
    public IActionResult Reset(string? email, string? token, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new ResetViewModel(email ?? string.Empty, token ?? string.Empty, string.Empty, string.Empty));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Reset(string email, string token, string newPassword, string confirmPassword, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return View(new ResetViewModel(email, token, newPassword, confirmPassword, "Las contraseñas no coinciden."));

        try
        {
            var (success, error) = await customerApi.ResetPasswordAsync(email, token, newPassword, cancellationToken);
            if (success)
            {
                TempData["Success"] = "Contraseña actualizada. Inicia sesión.";
                return RedirectToAction("Login", new { returnUrl });
            }

            return View(new ResetViewModel(email, token, newPassword, confirmPassword, error ?? "No fue posible restablecer la contraseña."));
        }
        catch (HttpRequestException)
        {
            return View(new ResetViewModel(email, token, newPassword, confirmPassword, "No fue posible contactar al servicio de identidad. Intenta más tarde."));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Store");
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Store")!;
}
