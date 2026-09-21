using Front.Web.Models;
using Front.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace Front.Web.Controllers;

public sealed class AccountController(IdentityApiClient identityApi, MongoCustomerService customerService, JwtTokenIssuer jwtTokenIssuer) : Controller
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
        var customer = await customerService.GetCustomerByEmailAsync(email, cancellationToken);
        if (customer is not null && await customerService.VerifyCustomerPasswordAsync(email, password, cancellationToken))
        {
            var token = jwtTokenIssuer.Issue(customer);
            SetSession(customer, token.Token);
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? Url.Action("Index", "Store")! : returnUrl);
        }

        LoginResponseDto? login = null;
        try
        {
            login = await identityApi.LoginAsync(email, password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel(email, "El servicio de identidad no está disponible. Usa una cuenta registrada en Mongo o inicia Identity."));
        }

        if (login is null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel(email, "Correo o contraseña incorrectos."));
        }

        HttpContext.Session.SetString("AccessToken", login.AccessToken);
        HttpContext.Session.SetString("CustomerName", $"{login.FirstName} {login.LastName}".Trim());
        HttpContext.Session.SetString("CustomerEmail", login.Email);
        HttpContext.Session.SetInt32("CustomerId", login.Id);
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? Url.Action("Index", "Store")! : returnUrl);
    }

    private void SetSession(CustomerDto customer, string token)
    {
        HttpContext.Session.SetString("AccessToken", token);
        HttpContext.Session.SetString("CustomerName", $"{customer.FirstName} {customer.LastName}".Trim());
        HttpContext.Session.SetString("CustomerEmail", customer.Email);
        HttpContext.Session.SetInt32("CustomerId", customer.Id);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var created = await customerService.CreateCustomerAsync(model.FirstName, model.LastName, model.Email, model.Password, cancellationToken);
        if (created is null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model with { Error = "El correo ya está registrado." });
        }

        return RedirectToAction("Login", new { returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Store");
    }
}
