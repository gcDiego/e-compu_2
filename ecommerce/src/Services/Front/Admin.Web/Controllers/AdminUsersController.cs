using Admin.Web.Models;
using Admin.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;
public sealed class AdminUsersController(AdminApiClient api):SecureController
{
    public async Task<IActionResult> Index(CancellationToken ct)=>View(await api.Admins(ct));
    [HttpPost] public async Task<IActionResult> Save(SaveAdminModel model,CancellationToken ct){if(ModelState.IsValid){var r=await api.SaveAdmin(model,ct);if(r.IsSuccessStatusCode&&model.Id is null)await api.Recover(model.Email,ct);TempData[r.IsSuccessStatusCode?"Message":"Error"]=r.IsSuccessStatusCode?"Administrador guardado; se enviaron instrucciones para establecer su contraseña.":"No se pudo guardar el administrador.";}return RedirectToAction("Index");}
}
