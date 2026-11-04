using Catalog.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace Catalog.Api.UnitTests;

public sealed class ImageCompensationTests
{
    private static List<string?> Recorder(out Func<string?, CancellationToken, Task> delete)
    {
        var deleted = new List<string?>();
        delete = (publicId, _) => { deleted.Add(publicId); return Task.CompletedTask; };
        return deleted;
    }

    [Fact]
    public async Task Uploaded_image_is_deleted_when_the_product_does_not_exist()
    {
        var deleted = Recorder(out var delete);

        var result = await ProductImage.ApplyAsync(new StoredImage("https://img", "nueva"), (null, false), delete, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(["nueva"], deleted);
    }

    [Fact]
    public async Task Previous_image_is_deleted_when_the_product_is_updated()
    {
        var deleted = Recorder(out var delete);

        var result = await ProductImage.ApplyAsync(new StoredImage("https://img", "nueva"), ("anterior", true), delete, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NoContent, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(["anterior"], deleted);
    }

    [Fact]
    public async Task Nothing_is_deleted_when_the_product_had_no_previous_image()
    {
        var deleted = Recorder(out var delete);

        await ProductImage.ApplyAsync(new StoredImage("https://img", "nueva"), (null, true), delete, CancellationToken.None);

        Assert.Equal([null], deleted);
    }
}

public sealed class ImageUploadEndpointTests : IClassFixture<ImageUploadEndpointTests.Factory>
{
    private const string Issuer = "Ecommerce.Identity";
    private const string Audience = "Ecommerce.Services";
    private const string SigningKey = "catalog-tests-signing-key-32-chars-min";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        // Program lee la configuración antes de Build(); las variables de entorno son la única vía que ya consulta.
        public Factory()
        {
            Environment.SetEnvironmentVariable("MongoDb__ConnectionString", "mongodb://localhost:27017/?serverSelectionTimeoutMS=200");
            Environment.SetEnvironmentVariable("MongoDb__DatabaseName", "catalog-tests");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        }

        // Sin MongoDB disponible el inicializador de índices impediría arrancar el host de pruebas.
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(x => x.ImplementationType == typeof(CatalogDbInitializer)).ToList())
                    services.Remove(descriptor);
            });
    }

    private readonly Factory _factory;

    public ImageUploadEndpointTests(Factory factory) => _factory = factory;

    private static string AdminToken()
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "1"), new Claim("role", "admin")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "image", fileName);
        return form;
    }

    private HttpClient Client(bool authenticated)
    {
        var client = _factory.CreateClient();
        if (authenticated) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken());
        return client;
    }

    [Fact]
    public async Task Upload_without_token_is_rejected()
    {
        var response = await Client(false).PostAsync("/api/admin/catalog/products/1/image", Upload([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0], "image.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_of_a_disguised_svg_is_rejected()
    {
        var bytes = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();

        var response = await Client(true).PostAsync("/api/admin/catalog/products/1/image", Upload(bytes, "image.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Formato de imagen no permitido", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_of_a_valid_png_without_cloudinary_credentials_degrades_to_503()
    {
        var response = await Client(true).PostAsync("/api/admin/catalog/products/1/image", Upload([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0], "image.png"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
