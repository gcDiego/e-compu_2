using Admin.Web.Models;
using Admin.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Web.Controllers;
public sealed class DashboardController(AdminApiClient api):SecureController
{
    public async Task<IActionResult> Index(CancellationToken ct){var customers=api.CustomerCount(ct);var orders=api.OrderDashboard(ct);var products=api.Products(ct);var recent=api.Orders(1,ct);await Task.WhenAll(customers,orders,products,recent);return View(new DashboardViewModel(customers.Result.Count,orders.Result.Sales,orders.Result.Revenue,products.Result.Count,products.Result.Count(x=>x.Stock<=3),recent.Result.Items.Take(8).ToList()));}
}
