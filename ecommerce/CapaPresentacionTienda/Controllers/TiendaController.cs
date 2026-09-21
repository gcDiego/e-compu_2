using CapaEntidad;
using CapaEntidad.Paypal;
using CapaNegocio;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using CapaPresentacionTienda.Filter;

namespace CapaPresentacionTienda.Controllers
{
    public class TiendaController : Controller
    {
        // GET: Tienda
        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> DetalleProducto(int idproducto = 0)
        {
            Producto oProducto;

            if (UseCatalogApi())
            {
                oProducto = await new CatalogApiClient().ObtenerProductoAsync(idproducto);
                if (oProducto != null)
                {
                    var imagenLegada = new CN_Producto().Listar().FirstOrDefault(p => p.IdProducto == idproducto);
                    AgregarImagen(oProducto, imagenLegada);
                }
            }
            else
            {
                oProducto = new CN_Producto().Listar().FirstOrDefault(p => p.IdProducto == idproducto);
                AgregarImagen(oProducto, oProducto);
            }

            return View(oProducto);
        }

        [HttpGet]

        public async Task<JsonResult> ListarCategorias()
        {
            List<Categoria> lista = UseCatalogApi()
                ? await new CatalogApiClient().ListarCategoriasAsync(true)
                : new CN_Categoria().Listar();
            return Json(new {data = lista}, JsonRequestBehavior.AllowGet);
        }
        
        [HttpPost]

        public async Task<JsonResult> ListarMarcaporCategoria(int idcategoria)
        {
            List<Marca> lista = UseCatalogApi()
                ? await new CatalogApiClient().ListarMarcasPorCategoriaAsync(idcategoria)
                : new CN_Marca().ListarMarcaporCategoria(idcategoria);
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]

        public async Task<JsonResult> ListarProdcto(int idcategoria, int idmarca)
        {
            List<Producto> lista;

            if (UseCatalogApi())
            {
                lista = await new CatalogApiClient().ListarProductosAsync(idcategoria, idmarca);
                var imagenesLegadas = new CN_Producto().Listar().ToDictionary(p => p.IdProducto);
                foreach (var producto in lista)
                {
                    Producto imagenLegada;
                    imagenesLegadas.TryGetValue(producto.IdProducto, out imagenLegada);
                    AgregarImagen(producto, imagenLegada);
                }
            }
            else
            {
                bool coversion;
                lista = new CN_Producto().Listar().Select(p => new Producto()
                {
                    IdProducto = p.IdProducto,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    oMarca = p.oMarca,
                    oCategoria = p.oCategoria,
                    Precio = p.Precio,
                    Stock = p.Stock,
                    RutaImagen = p.RutaImagen,
                    Base64 = CN_Recursos.ConvertirBase64(Path.Combine(p.RutaImagen,p.NombreImagen), out coversion),
                    Extension = Path.GetExtension(p.NombreImagen),
                    Activo = p.Activo

                }).Where(p => p.oCategoria.IdCategoria == (idcategoria == 0 ? p.oCategoria.IdCategoria : idcategoria) && p.oMarca.IdMarca == (idmarca == 0 ? p.oMarca.IdMarca : idmarca) && p.Stock > 0 && p.Activo == true).ToList();
            }

            var jsonresult = Json(new { data = lista }, JsonRequestBehavior.AllowGet); 
            jsonresult.MaxJsonLength = int.MaxValue;

            return jsonresult;

        }

        [HttpPost] 

        public async Task<JsonResult> AgregarCarrito(int idproducto)
        {
            if (UseCartApi())
            {
                var result = await CreateCartApiClient().AgregarAsync(idproducto);
                return Json(new { respuesta = result.Success, mensaje = result.Message }, JsonRequestBehavior.AllowGet);
            }

            int idcliente = ((Cliente)Session["Cliente"]).IdCliente;

            bool existe = new CN_Carrito().ExisteCarrito(idcliente, idproducto);

            bool respuesta = false; 

            string mensaje = string.Empty;

            if(existe)
            {
                mensaje = "El producto ya existe en el carrito"; 

            }
            else
            {
                respuesta = new CN_Carrito().OperacionCarrito(idcliente, idproducto, true, out mensaje);
            }

            return Json(new { respuesta = respuesta, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]

        public async Task<JsonResult> CantidadEnCarrito()
        {
            if (UseCartApi())
            {
                var snapshot = await CreateCartApiClient().ObtenerAsync();
                return Json(new { cantidad = snapshot.CantidadProductos }, JsonRequestBehavior.AllowGet);
            }

            int idcliente = ((Cliente)Session["Cliente"]).IdCliente;
            int cantidad = new CN_Carrito().CantidadEnCarrito(idcliente);
            return Json(new { cantidad = cantidad }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]

        public async Task<JsonResult> ListarProductosCarrito()
        {
            if (UseCartApi())
            {
                var snapshot = await CreateCartApiClient().ObtenerAsync();
                var imagenesLegadas = new CN_Producto().Listar().ToDictionary(p => p.IdProducto);
                foreach (var item in snapshot.Items)
                {
                    Producto imagenLegada;
                    imagenesLegadas.TryGetValue(item.oProducto.IdProducto, out imagenLegada);
                    AgregarImagen(item.oProducto, imagenLegada);
                }

                return Json(new { data = snapshot.Items }, JsonRequestBehavior.AllowGet);
            }

            int idcliente = ((Cliente)Session["cliente"]).IdCliente;

            List<Carrito> olista = new List<Carrito>();

            bool convesion;

            olista = new CN_Carrito().ListarProducto(idcliente).Select(oc => new Carrito()
            {
               oProducto = new Producto()
               {
                   IdProducto = oc.oProducto.IdProducto,
                   Nombre = oc.oProducto.Nombre,
                   oMarca = oc.oProducto.oMarca,
                   Precio = oc.oProducto.Precio,
                   RutaImagen = oc.oProducto.RutaImagen,
                   Base64 = CN_Recursos.ConvertirBase64(Path.Combine( oc.oProducto.RutaImagen, oc.oProducto.NombreImagen),out convesion),
                   Extension = Path.GetExtension(oc.oProducto.NombreImagen)
               }, 
               Cantidad = oc.Cantidad
            }).ToList();

            return Json(new { data = olista }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<JsonResult> OperacionCarrito(int idproducto, bool sumar)
        {
            if (UseCartApi())
            {
                var result = await CreateCartApiClient().CambiarCantidadAsync(idproducto, sumar);
                return Json(new { respuesta = result.Success, mensaje = result.Message }, JsonRequestBehavior.AllowGet);
            }

            int idcliente = ((Cliente)Session["Cliente"]).IdCliente;

            string mensaje = string.Empty;

            
            bool respuesta = new CN_Carrito().OperacionCarrito(idcliente, idproducto, sumar, out mensaje);

            return Json(new { respuesta = respuesta, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]

        public async Task<JsonResult> EliminarCarrito(int idproducto)
        {
            if (UseCartApi())
            {
                var result = await CreateCartApiClient().EliminarAsync(idproducto);
                return Json(new { respuesta = result.Success, mensaje = result.Message }, JsonRequestBehavior.AllowGet);
            }

            int idcliente = ((Cliente)Session["Cliente"]).IdCliente;

            bool respuesta = false;

            string mensaje = string.Empty;

            respuesta = new CN_Carrito().EliminarCarrito(idcliente, idproducto);

            return Json(new { respuesta = respuesta, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]

        public JsonResult ObtenerEstado()
        {
            List<Estado> oLista = new List<Estado>();

            oLista = new CN_Ubicacion().ObtenerEstado();
            
            return Json(new { lista = oLista }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]

        public JsonResult ObtenerMunicipio(string IdEstado)
        {
            List<Municipio> oLista = new List<Municipio>();

            oLista = new CN_Ubicacion().ObtenerMunicipio(IdEstado);

            return Json(new { lista = oLista }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult ObtenerLocalidad(string IdEstado, string IdMunicipio)
        {
            List<Localidad> oLista = new List<Localidad>();

            oLista = new CN_Ubicacion().ObtenerLocalidad(IdEstado, IdMunicipio);

            return Json(new { lista = oLista }, JsonRequestBehavior.AllowGet);
        }

        [ValidarSession]
        [Authorize]
        public ActionResult Carrito()
        {
            return View();
        }

        [HttpPost]

        public async Task<JsonResult> ProcesarPago(List<Carrito> oListarCarrito, Venta oVenta)
        {
            try
            {
                decimal total = 0;

                DataTable detalle_venta = new DataTable();
                detalle_venta.Locale = new CultureInfo("es-MX");
                detalle_venta.Columns.Add("IdProducto", typeof(string));
                detalle_venta.Columns.Add("Cantidad", typeof(string));
                detalle_venta.Columns.Add("Total", typeof(decimal));

                List<Item> oListaItem = new List<Item>();

                foreach (Carrito oCarrito in oListarCarrito)
                {
                    decimal subtotal = oCarrito.Cantidad * oCarrito.oProducto.Precio;
                    total += subtotal;

                    oListaItem.Add(new Item()
                    {
                        name = oCarrito.oProducto.Nombre,
                        quantity = oCarrito.Cantidad.ToString(),
                        unit_amount = new UnitAmount()
                        {
                            currency_code = "MXN",
                            value = oCarrito.oProducto.Precio.ToString("G", new CultureInfo("es-MX"))
                        }
                    });

                    detalle_venta.Rows.Add(oCarrito.oProducto.IdProducto, oCarrito.Cantidad, subtotal);
                }

                var purchaseUnit = new PurchaseUnit()
                {
                    amount = new Amount()
                    {
                        currency_code = "MXN",
                        value = total.ToString("G", new CultureInfo("es-MX")),
                        breakdown = new Breakdown()
                        {
                            item_total = new ItemTotal()
                            {
                                currency_code = "MXN",
                                value = total.ToString("G", new CultureInfo("es-MX"))
                            }
                        }
                    },
                    description = "Compra de articulos de ECOMPU",
                    items = oListaItem
                };

                var oCheckOutOrder = new Checkout_Order()
                {
                    intent = "CAPTURE",
                    purchase_units = new List<PurchaseUnit> { purchaseUnit },
                    application_context = new ApplicationContext()
                    {
                        brand_name = "ECOMPU.com",
                        landing_page = "NO_PREFERENCE",
                        user_action = "PAY_NOW",
                        return_url = "https://localhost:44387/Tienda/PagoEfectuado",
                        cancel_url = "https://localhost:44387/Tienda/Carrito"
                    }
                };

                oVenta.MontoTotal = total;
                oVenta.IdCliente = ((Cliente)Session["Cliente"]).IdCliente;

                TempData["Venta"] = oVenta;
                TempData["DetalleVenta"] = detalle_venta;

                var oPaypal = new CN_Paypal();
                var response_paypal = await oPaypal.CrearSolicitud(oCheckOutOrder);

                if (response_paypal == null || response_paypal.Response == null)
                {
                    return Json(new { Status = false, Response = "Error al crear la orden en PayPal." }, JsonRequestBehavior.AllowGet);
                }

                return Json(response_paypal, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Response = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [ValidarSession]
        [Authorize]
        public async Task<ActionResult> PagoEfectuado()
        {
            string token = Request.QueryString["token"];

            CN_Paypal oPayapal = new CN_Paypal();
            Response_Paypal<Response_Capture> response_paypal = new Response_Paypal<Response_Capture>();
            response_paypal = await oPayapal.AprobarPago(token);
            

            ViewData["Status"] = response_paypal.Status;

            if (response_paypal.Status)
            {
                Venta oVenta = (Venta)TempData["Venta"];

                DataTable detalle_venta = (DataTable)TempData["DetalleVenta"];

                oVenta.IdTransaccion = response_paypal.Response.purchase_units[0].payments.captures[0].id;

                string mensaje = string.Empty;

                bool respuesta = new CN_Venta().Registrar(oVenta, detalle_venta, out mensaje);

                ViewData["IdTransaccion"] = oVenta.IdTransaccion;
            }

            return View();
        }

        [ValidarSession]
        [Authorize]
        public ActionResult MisCompras()
        {
            int idcliente = ((Cliente)Session["cliente"]).IdCliente;

            List<DetalleVenta> olista = new List<DetalleVenta>();

            bool convesion;

            olista = new CN_Venta().ListarCompras(idcliente).Select(oc => new DetalleVenta()
            {
                oProducto = new Producto()
                {
                    Nombre = oc.oProducto.Nombre,
                    Precio = oc.oProducto.Precio,
                    Base64 = CN_Recursos.ConvertirBase64(Path.Combine(oc.oProducto.RutaImagen, oc.oProducto.NombreImagen), out convesion),
                    Extension = Path.GetExtension(oc.oProducto.NombreImagen)
                },
                Cantidad = oc.Cantidad,
                Total = oc.Total,
                IdTransaccion = oc.IdTransaccion,

            }).ToList();

            return View(olista);
        }

        private static bool UseCatalogApi()
        {
            bool enabled;
            return bool.TryParse(ConfigurationManager.AppSettings["Features:UseCatalogApi"], out enabled) && enabled;
        }

        private static bool UseCartApi()
        {
            bool enabled;
            return bool.TryParse(ConfigurationManager.AppSettings["Features:UseCartApi"], out enabled) && enabled;
        }

        private CartApiClient CreateCartApiClient()
        {
            return new CartApiClient(Session["IdentityAccessToken"] as string);
        }

        private static void AgregarImagen(Producto producto, Producto imagenLegada)
        {
            if (producto == null || imagenLegada == null)
                return;

            bool conversion;
            producto.Base64 = CN_Recursos.ConvertirBase64(
                Path.Combine(imagenLegada.RutaImagen, imagenLegada.NombreImagen), out conversion);
            producto.Extension = Path.GetExtension(imagenLegada.NombreImagen);
        }
    }
}