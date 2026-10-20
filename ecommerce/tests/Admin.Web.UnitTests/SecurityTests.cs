using Admin.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Admin.Web.UnitTests;

public sealed class AdminSecurityTests
{
    [Fact]
    public void All_secure_controllers_require_admin_session()
    {
        var secured = typeof(SecureController).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SecureController)) && !t.IsAbstract)
            .ToArray();

        Assert.NotEmpty(secured);
        Assert.All(secured, t =>
            Assert.True(t.IsDefined(typeof(AdminSessionRequirementAttribute), true), t.Name));
    }

    [Fact]
    public void Public_controllers_do_not_require_admin_session()
    {
        Assert.False(typeof(AccountController).IsSubclassOf(typeof(SecureController)));
        Assert.False(typeof(HomeController).IsSubclassOf(typeof(SecureController)));
    }

    [Fact]
    public void Missing_admin_token_redirects_to_login()
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new TestSessionFeature(new TestSession()));
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            []);

        new AdminSessionRequirementAttribute().OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public void Admin_token_allows_request()
    {
        var session = new TestSession();
        session.SetString("AdminToken", "token");
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new TestSessionFeature(session));
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            []);

        new AdminSessionRequirementAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
