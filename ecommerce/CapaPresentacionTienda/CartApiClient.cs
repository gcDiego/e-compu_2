using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CapaEntidad;
using Newtonsoft.Json;

namespace CapaPresentacionTienda
{
    public class CartApiClient
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly string accessToken;

        public CartApiClient(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("La sesión no contiene un token de Identity Service válido.");
            this.accessToken = accessToken;
        }

        public async Task<CartSnapshot> ObtenerAsync()
        {
            var response = await SendAsync(HttpMethod.Get, "api/v1/cart", null);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<CartSnapshotDto>(json) ?? new CartSnapshotDto();
            return new CartSnapshot
            {
                CantidadProductos = dto.DistinctItemCount,
                Items = dto.Items.ConvertAll(item => new Carrito
                {
                    oProducto = new Producto
                    {
                        IdProducto = item.ProductId,
                        Nombre = item.ProductName,
                        Precio = item.UnitPrice,
                        oMarca = new Marca { Descripcion = item.BrandName }
                    },
                    Cantidad = item.Quantity
                })
            };
        }

        public async Task<CartApiOperationResult> AgregarAsync(int productId)
        {
            return await ExecuteOperationAsync(HttpMethod.Post, "api/v1/cart/items/" + productId, null);
        }

        public async Task<CartApiOperationResult> CambiarCantidadAsync(int productId, bool increase)
        {
            var body = JsonConvert.SerializeObject(new { increase = increase });
            return await ExecuteOperationAsync(
                new HttpMethod("PATCH"),
                "api/v1/cart/items/" + productId,
                new StringContent(body, Encoding.UTF8, "application/json"));
        }

        public async Task<CartApiOperationResult> EliminarAsync(int productId)
        {
            return await ExecuteOperationAsync(HttpMethod.Delete, "api/v1/cart/items/" + productId, null);
        }

        private async Task<CartApiOperationResult> ExecuteOperationAsync(HttpMethod method, string path, HttpContent content)
        {
            var response = await SendAsync(method, path, content);
            if (response.IsSuccessStatusCode)
                return new CartApiOperationResult { Success = true, Message = string.Empty };

            var message = await ReadProblemTitleAsync(response);
            return new CartApiOperationResult
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(message) ? "No fue posible actualizar el carrito." : message
            };
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent content)
        {
            EnsureConfigured();
            var request = new HttpRequestMessage(method, path) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await Client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("La sesión de Identity Service expiró. Inicia sesión nuevamente.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("La cuenta actual no tiene acceso al carrito.");
            return response;
        }

        private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return null;
            var problem = JsonConvert.DeserializeObject<ProblemDto>(json);
            return problem == null ? null : problem.Title;
        }

        private static void EnsureConfigured()
        {
            if (Client.BaseAddress != null)
                return;

            var baseUrl = ConfigurationManager.AppSettings["CartApi:BaseUrl"];
            Uri uri;
            if (string.IsNullOrWhiteSpace(baseUrl) ||
                !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ConfigurationErrorsException(
                    "CartApi:BaseUrl debe contener una URL HTTP o HTTPS absoluta cuando Features:UseCartApi está habilitado.");
            }

            Client.BaseAddress = uri;
        }

        private class CartSnapshotDto
        {
            [JsonProperty("items")] public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
            [JsonProperty("distinctItemCount")] public int DistinctItemCount { get; set; }
        }

        private class CartItemDto
        {
            [JsonProperty("productId")] public int ProductId { get; set; }
            [JsonProperty("productName")] public string ProductName { get; set; }
            [JsonProperty("brandName")] public string BrandName { get; set; }
            [JsonProperty("unitPrice")] public decimal UnitPrice { get; set; }
            [JsonProperty("quantity")] public int Quantity { get; set; }
        }

        private class ProblemDto
        {
            [JsonProperty("title")] public string Title { get; set; }
        }
    }

    public class CartSnapshot
    {
        public List<Carrito> Items { get; set; }
        public int CantidadProductos { get; set; }
    }

    public class CartApiOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
