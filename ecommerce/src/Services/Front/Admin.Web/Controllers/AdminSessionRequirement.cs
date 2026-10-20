using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Admin.Web.Controllers;

public sealed class AdminSessionRequirementAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(context.HttpContext.Session.GetString("AdminToken")))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }
    }
}
