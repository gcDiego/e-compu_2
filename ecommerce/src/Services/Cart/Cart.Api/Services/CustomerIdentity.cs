using System.Security.Claims;

namespace Cart.Api.Services;

public static class CustomerIdentity
{
    public static int? GetCustomerId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }
}
