using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;

[AdminSessionRequirement]
public abstract class SecureController : Controller
{
}
