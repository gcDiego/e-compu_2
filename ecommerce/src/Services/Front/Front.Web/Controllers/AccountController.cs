using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Front.Web.Controllers;

public sealed class AccountController(IdentityApiClient identityApi) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl, CancellationToken cancellationToken)
    {
        var login = await identityApi.LoginAsync(email, password, cancellationToken);
        if (login is null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel(email, "Correo o contraseña incorrectos."));
        }

        HttpContext.Session.SetString("AccessToken", login.AccessToken);
        HttpContext.Session.SetString("CustomerName", $"{login.FirstName} {login.LastName}".Trim());
        HttpContext.Session.SetString("CustomerEmail", login.Email);
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? Url.Action("Index", "Store")! : returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Store");
    }
}
