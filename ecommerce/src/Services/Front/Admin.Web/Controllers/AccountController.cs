using Admin.Web.Models;
using Admin.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;

public sealed class AccountController(AdminApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var login = await api.Login(model.Email, model.Password, ct);
        if (login is null)
        {
            model.Error = "Correo o contraseña incorrectos.";
            return View(model);
        }

        HttpContext.Session.SetString("AdminToken", login.AccessToken);
        HttpContext.Session.SetString("AdminName", $"{login.FirstName} {login.LastName}");
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Recover() => View();

    [HttpPost]
    public async Task<IActionResult> Recover(string email, CancellationToken ct)
    {
        await api.Recover(email, ct);
        return RedirectToAction(nameof(Reset), new { email, emailed = true });
    }

    [HttpGet]
    public IActionResult Reset(string email = "", string token = "", bool emailed = false)
    {
        ViewBag.EmailSent = emailed;
        return View(new ResetModel { Email = email, Token = token });
    }

    [HttpPost]
    public async Task<IActionResult> Reset(ResetModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.Reset(model, ct);
        if (!result.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "El enlace no es válido o expiró.");
            return View(model);
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
