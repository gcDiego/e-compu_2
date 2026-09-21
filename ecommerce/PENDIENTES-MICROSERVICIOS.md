# Pendientes de migración a microservicios

Estado actual: `Front.Web` ya consume MongoDB directamente para catálogo, clientes, ubicaciones, carrito y órdenes. Esto funciona como paso intermedio, pero no es la arquitectura final de microservicios.

## Funcionalidad pendiente

### Customer / Accounts
- [ ] Recuperación de contraseña por correo.
- [ ] Cambio obligatorio de contraseña (`must_reset_password`).
- [ ] Perfil y actualización de datos del cliente.
- [ ] Migrar la lógica actual en `MongoCustomerService` a un `Customer.Api` independiente.

### Location
- [ ] Definir si se mantiene catálogo propio de ubicaciones o se usa proveedor externo.
- [ ] Extraer `MongoLocationService` a un `Location.Api`.

### Orders / Sales
- [ ] Historial de compras del cliente (vista / consulta).
- [ ] Consulta de transacciones.
- [ ] Descuento transaccional de inventario al confirmar la orden.
- [ ] Extraer `MongoOrderService` a un `Order.Api`.

### Payments
- [ ] Creación de órdenes de pago en PayPal.
- [ ] Captura y confirmación del pago.
- [ ] URLs de retorno y cancelación configurables.
- [ ] Idempotencia y conciliación de pagos.
- [ ] Manejo de webhooks de PayPal.
- [ ] Crear `Payment.Api`.

### Catalog Administration
- [ ] Alta, edición y desactivación de productos.
- [ ] Administración de categorías y marcas.
- [ ] Control de stock desde una API protegida para administradores.
- [ ] Crear `Admin.Api` o extender `Catalog.Api` con políticas de administrador.

### Media / Product Images
- [ ] Definir almacenamiento de imágenes (local, S3, Azure Blob, CDN).
- [ ] Agregar `imageUrl` al contrato de producto.
- [ ] Carga de imágenes desde administración.
- [ ] Reemplazar las imágenes ilustrativas actuales por URLs reales.

## Refactor hacia microservicios reales

Actualmente `Front.Web` accede directamente a MongoDB. Para completar la arquitectura de microservicios se recomienda:

1. Extraer `MongoCatalogService` → `Catalog.Api` (lecturas públicas).
2. Extraer `MongoCustomerService` → `Customer.Api` (registro, login, perfil).
3. Extraer `MongoCartService` → `Cart.Api` (carrito por cliente).
4. Extraer `MongoLocationService` → `Location.Api` (estados, municipios, localidades).
5. Extraer `MongoOrderService` → `Order.Api` (creación y consulta de órdenes).
6. Crear `Payment.Api` (PayPal).
7. Crear `Media.Api` o integrar almacenamiento de imágenes.
8. `Front.Web` debe consumir los servicios anteriores vía HTTP + JWT, igual que hacía con `CatalogApiClient`, `IdentityApiClient` y `CartApiClient`.

## Seguridad y operación

- [ ] No versionar secretos en `appsettings.Local.json` (actualmente en `.gitignore`, revisar antes de commit).
- [ ] Pruebas de integración para flujos de compra y pago.
- [ ] Registrar cambios en `REGISTRO_CAMBIOS_MICROSERVICIOS.md`.
- [ ] Retirar `CapaPresentacionTienda` cuando los flujos anteriores estén validados.
