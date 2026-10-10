using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Admin.Web.Controllers;

public abstract class SecureController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AdminToken"))) context.Result = RedirectToAction("Login","Account");
        base.OnActionExecuting(context);
    }
}
