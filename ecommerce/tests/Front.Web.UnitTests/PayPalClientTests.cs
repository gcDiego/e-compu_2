using Front.Web.Models;
using Front.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Front.Web.UnitTests;

public sealed class PayPalClientTests
{
    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public void IsConfigured_returns_false_when_credentials_empty()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new PayPalClient(new DummyHttpClientFactory(), config, NullLogger<PayPalClient>.Instance);

        Assert.False(client.IsConfigured);
        Assert.Equal("", client.ClientId);
    }

    [Fact]
    public async Task CreateOrderAsync_returns_demo_order_id_when_not_configured()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new PayPalClient(new DummyHttpClientFactory(), config, NullLogger<PayPalClient>.Instance);

        var (orderId, error) = await client.CreateOrderAsync(150.00m, "MXN", CancellationToken.None);

        Assert.NotNull(orderId);
        Assert.StartsWith("DEMO-PAYPAL-", orderId);
        Assert.Null(error);
    }

    [Fact]
    public async Task CaptureOrderAsync_returns_completed_status_when_not_configured()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new PayPalClient(new DummyHttpClientFactory(), config, NullLogger<PayPalClient>.Instance);

        var (txId, status, error) = await client.CaptureOrderAsync("DEMO-123", CancellationToken.None);

        Assert.NotNull(txId);
        Assert.StartsWith("PAYPAL-SIM-", txId);
        Assert.Equal("COMPLETED", status);
        Assert.Null(error);
    }

    [Fact]
    public void CheckoutInputModel_with_transaction_id_is_valid()
    {
        var model = new CheckoutInputModel("Carlos Ruiz", "5512345678", "Av. Insurgentes 456", "loc-1", "PAYPAL-TX-12345");
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.True(isValid);
        Assert.Equal("PAYPAL-TX-12345", model.IdTransaccion);
    }
}
