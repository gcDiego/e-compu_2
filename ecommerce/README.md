# Migración de Ecommerce Paduki

## Propósito

Este `README.md` es el **único** documento del proyecto y la fuente autoritativa del estado, decisiones, riesgos, pendientes e historial de la migración de Ecommerce Paduki. El 2026-11-03 se fusionaron aquí el antiguo `README.md` (presentación, componentes, puertos y comandos) y `CONTINUACION_MIGRACION_ADMIN.md` (detalle administrativo, hoy en el anexo final), `MIGRACION.md` se renombró a este archivo y ambos originales se eliminaron. Cualquier sesión nueva debe leer este archivo y sólo este.

Debe actualizarse después de cada incremento funcional. No se debe marcar una etapa como terminada hasta que compile, tenga pruebas ejecutadas y exista una estrategia de rollback o recuperación.

## Estado ejecutivo

El sistema evolucionó desde dos aplicaciones ASP.NET MVC 5 sobre .NET Framework 4.7.2 y SQL Server hacia una solución ASP.NET Core 8 con interfaces separadas y APIs por dominio sobre MongoDB.

Actualmente `Front.Web` consume `Catalog.Api`, `Customer.Api`, `Order.Api`, `Cart.Api` y `Location.Api` mediante HTTP/JWT y ya no accede directamente a MongoDB ni referencia `MongoDB.Driver`. Los flujos de registro, login, recuperación, catálogo, carrito, checkout e historial están implementados y fueron probados de extremo a extremo contra el servidor privado configurado localmente.

`Admin.Web` consume `Administration.Api`, `Catalog.Api`, `Customer.Api` y `Order.Api` para autenticación administrativa, catálogo, métricas, pedidos y reportes. La propiedad de datos es por dominio; la única excepción transitoria es la colección `carritos`, propiedad de `Cart.Api` y leída/eliminada por `Order.Api` dentro de la transacción de checkout.

## Punto de partida para continuar en otra sesión

1. Leer este archivo completo; el anexo final tiene el detalle administrativo.
2. Levantar y verificar el entorno: `dotnet build Front.Web.sln --configuration Release`, `dotnet test Front.Web.sln --configuration Release --no-build` (114 pruebas aprobadas el 2026-11-03), `dotnet list Front.Web.sln package --vulnerable --include-transitive`, `./start.sh`.
3. Trabajo abierto, en orden sugerido: probar PayPal con credenciales reales de Sandbox; configurar SMTP en `Order.Api` para los correos de estado; automatizar la carga de archivos de extremo a extremo; reportes administrativos adicionales; Etapa 7 de madurez operativa (fuera de alcance de la demo).
4. Al terminar un incremento, actualizar "Validaciones realizadas", "Pendientes funcionales" y agregar una entrada de historial con la plantilla del final.

## Componentes activos

- `src/Services/Front/Front.Web`: tienda ASP.NET Core 8.
- `src/Services/Front/Admin.Web`: panel administrativo ASP.NET Core 8.
- `src/Services/Administration/Administration.Api`: autenticación y gestión administrativa.
- `src/Services/Catalog/Catalog.Api`: catálogo público y administrativo, stock e imágenes Cloudinary.
- `src/Services/Customer/Customer.Api`: registro, autenticación, perfil y recuperación de clientes.
- `src/Services/Cart/Cart.Api`: carrito persistente por cliente autenticado con JWT.
- `src/Services/Location/Location.Api`: estados, municipios y localidades de lectura pública.
- `src/Services/Order/Order.Api`: checkout, historial, pedidos, dashboard y reportes.
- `tests/Integration.Seed`: end-to-end contra servicios reales (registro, carrito, checkout, concurrencia y rollback).
- `tests/MongoCleanup`: limpieza de datos heredados (duplicados de `ventas.checkoutKey`, campos `rutaImagen`/`nombreImagen`).
- `mongo/*.json`: dumps de `carritos`, `categorias`, `clientes`, `estados`, `marcas`, `productos`, `usuarios` y `ventas` para restaurar un entorno local. `auditoria` y `counters` no tienen dump; se crean solas.

## Regla para interpretar el repositorio

Existen tres generaciones de componentes:

1. El sistema original en `CapaPresentacionAdmin`, `CapaPresentacionTienda`, `CapaNegocio`, `CapaDatos` y `CapaEntidad`.
2. Los primeros servicios .NET 8 sobre SQL Server: Catalog, Identity y Cart.
3. El frontend ASP.NET Core 8 `Front.Web`, actualmente conectado directamente a MongoDB.

Las implementaciones SQL y MVC se conservan como referencia funcional e historial. Para trabajo nuevo sobre la tienda, el estado de `Front.Web` y este documento tienen prioridad.

## Arquitectura actual

```text
Navegador
  ├── Front.Web (.NET 8)
  │     ├── Catalog.Api (HTTP)
  │     ├── Customer.Api (HTTP/JWT)
  │     ├── Cart.Api (HTTP/JWT)
  │     ├── Location.Api (HTTP)
  │     └── Order.Api (HTTP/JWT)
  └── Admin.Web (.NET 8)
        ├── Administration.Api (HTTP/JWT admin)
        ├── Catalog.Api (HTTP/JWT admin)
        ├── Customer.Api (métricas administrativas)
        └── Order.Api (pedidos, dashboard y reportes)

APIs
  └── MongoDB privado
        ├── categorias      (Catalog.Api)
        ├── marcas          (Catalog.Api)
        ├── productos       (Catalog.Api; stock también escrito por Order.Api en checkout)
        ├── clientes        (Customer.Api)
        ├── usuarios        (Administration.Api)
        ├── estados         (Location.Api)
        ├── carritos        (Cart.Api; leída/eliminada por Order.Api en la transacción de checkout)
        ├── ventas          (Order.Api)
        ├── auditoria       (Administration.Api/Catalog.Api)
        └── counters        (contadores atómicos de IDs heredados; escrita por cada servicio que crea documentos)
```

`Front.Web` y `Admin.Web` conservan el JWT en sesión del servidor. Las APIs validan JWT y políticas por rol; los navegadores no almacenan tokens en JavaScript ni `localStorage`.

## Arquitectura objetivo

```text
Navegador
  └── Front.Web
        └── Gateway o APIs HTTP
              ├── Customer.Api
              ├── Catalog.Api
              ├── Cart.Api
              ├── Location.Api
              ├── Order.Api
              ├── Payment.Api
              └── Media/Catalog Administration
                    └── MongoDB con propiedad de datos por dominio
```

Reglas:

- `Front.Web` no debe acceder directamente a MongoDB al terminar la migración.
- Cada servicio es propietario de sus reglas y colecciones.
- La identidad del cliente se deriva de autenticación validada, nunca de identificadores enviados por el navegador.
- Precios, cantidades finales y totales se calculan en servidor.
- Órdenes y pagos son idempotentes.
- Los secretos se configuran fuera del repositorio.
- No se comparten modelos de persistencia entre servicios.
- Una extracción debe aportar aislamiento, seguridad, escalabilidad o despliegue independiente; no se crean servicios sólo para aumentar su número.

## Funcionalidad completada

### Front.Web

- ASP.NET Core 8, configuración local fuera de Git y sesión de servidor para el flujo autenticado.
- Registro, login, recuperación y restablecimiento mediante `Customer.Api`.
- Perfil de cliente (consulta y actualización de nombre/apellido) y cambio autenticado de contraseña mediante `Customer.Api`.
- Catálogo, filtros y detalle mediante `Catalog.Api`.
- Ubicaciones dependientes mediante `Location.Api` y carrito persistente mediante `Cart.Api`.
- Checkout autenticado e historial mediante `Order.Api`.
- Creación transaccional e idempotente de órdenes, reconstrucción de precios, descuento condicional de stock y vaciado del carrito.
- Sin `MongoDB.Driver` ni acceso directo a la base de datos.

### Admin.Web

- Login, recuperación, reset y cierre de sesión administrativos.
- Dashboard de clientes, ventas, ingresos, productos y stock bajo.
- Gestión de administradores, categorías, marcas, productos, stock e imágenes.
- Pedidos con detalle, cambio de estado (confirmado, en preparación, enviado, entregado, cancelado), paquetería y número de guía, reportes y exportación XLSX.
- JWT en sesión del servidor, antiforgery global, cookies seguras y cabeceras defensivas.

### APIs

- `Administration.Api`: identidad administrativa, bloqueo, rate limiting, recuperación, gestión de usuarios y auditoría.
- `Catalog.Api`: lectura pública y administración protegida de catálogo, stock e imágenes Cloudinary.
- `Customer.Api`: identidad y recuperación de clientes, además de métrica administrativa protegida.
- `Order.Api`: checkout, historial, pedidos administrativos, dashboard y reportes.
- `Cart.Api`: carrito por cliente autenticado con JWT; valida producto activo, stock y máximo 99 unidades.
- `Location.Api`: lectura pública de estados, municipios y localidades.

## Validaciones realizadas

- `dotnet test Front.Web.sln --configuration Release` completó con 75 pruebas aprobadas el 2026-10-20; el comando también restauró y compiló la solución sin errores.
- La validación integrada más reciente comprobó health checks, registro, login, carrito mediante `Cart.Api`, ubicaciones mediante `Location.Api`, checkout, historial, autorización administrativa, recuperación SMTP, dashboard, catálogo, pedidos y reportes.
- Las configuraciones sensibles de MongoDB, JWT y SMTP permanecen en configuración local no versionada; Cloudinary todavía no tiene credenciales locales.
- Los inicializadores de Administration y Catalog detectan índices únicos equivalentes por clave y opciones, sin depender de su nombre; se validaron en vivo contra MongoDB sin código 85.
- La auditoría de paquetes detectó y corrigió transitivos vulnerables en Customer.Api, Order.Api.UnitTests y Admin.Web.UnitTests; la repetición quedó limpia.
- El checkout concurrente se validó en vivo el 2026-10-20: dos requests paralelos producen una sola orden, un solo descuento de stock y ningún error 5xx.
- El rollback de fallo parcial y el correo de establecimiento al crear administrador se validaron en vivo el 2026-10-20.
- Cloudinary validado en vivo el 2026-10-28: credenciales reales configuradas, subida JPG desde `Admin.Web` y renderizado en `Front.Web` tras ampliar `img-src` de la CSP a `res.cloudinary.com`.
- Antiforgery de extremo a extremo automatizado el 2026-11-03 en `tests/Admin.Web.UnitTests/AntiforgeryTests.cs` con `WebApplicationFactory<Program>`: un POST sin token a `/Account/Logout` responde `400` y el mismo POST con el token obtenido de `/Account/Login` responde `302`.
- Los campos de imagen heredados de Windows (`rutaImagen`/`nombreImagen`) se eliminaron del dump `mongo/productos.json` y `tests/MongoCleanup` los borra de los documentos existentes de `productos`.

## Riesgos prioritarios

### Seguridad de contraseñas

Los registros nuevos ya usan PBKDF2-SHA256 y los logins SHA-256 válidos se actualizan de forma optimista. El riesgo restante es retirar completamente los hashes heredados después de medir la migración y centralizar identidad en Customer/Identity API.

### Precio del carrito

El checkout ya reconstruye nombre y precio desde el producto vigente. Falta validar el comportamiento integrado ante cambios concurrentes de precio y definir formalmente si el negocio utiliza precio vigente o precio reservado.

### Consistencia de checkout

La creación de orden, el descuento de stock y la eliminación del carrito usan una transacción con `checkoutKey` como clave de idempotencia única. Si dos operaciones concurrentes intentan crear una orden para el mismo carrito, el índice único en `ventas.checkoutKey` hace fallar la segunda y `OrderStore` devuelve la orden ya existente. Desde el 2026-10-20 la operación reintenta hasta 3 veces ante `TransientTransactionError` (conflicto de escritura entre transacciones), por lo que un request perdedor devuelve `400` controlado en lugar de `500`; validado en vivo con dos requests paralelos contra MongoDB con replica set. También se validó el rollback con un carrito multiartículo cuyo segundo producto no tenía stock: no se creó orden, el stock reservado por el primer artículo se restauró y el carrito permaneció disponible.

### Concurrencia de identificadores

Los nuevos IDs heredados se asignan mediante contadores atómicos. Falta revisar duplicados preexistentes y crear índices únicos antes de considerar cerrada la garantía de unicidad.

## Inventario de dependencias SQL pendientes

### Estado del repositorio

`Front.Web` ya no contiene conexiones, adaptadores ni consultas a SQL Server. Sin embargo, el repositorio aún alberga artefactos de la arquitectura anterior que impiden declarar SQL como eliminado.

### Artefactos legados compilados

- [x] Carpetas `CapaDatos`, `CapaNegocio`, `CapaEntidad`, `CapaPresentacionAdmin` y `CapaPresentacionTienda` eliminadas.
- Contenían únicamente ensamblados, configuraciones de `Web.config` y metadatos de compilación; el código fuente no estaba presente.

### Servicios .NET 8 sobre SQL Server

- [x] Proyectos `Cart`, `Catalog` e `Identity` (`src/Services/Cart`, `src/Services/Catalog`, `src/Services/Identity`) eliminados; no tenían código fuente, solo carpetas `obj` con artefactos de compilación.
- [x] Las pruebas en `tests/Identity.*`, `tests/Catalog.*` y `tests/Cart.*` fueron eliminadas porque apuntaban a servicios SQL que no existen en el repositorio.
- La arquitectura intermedia SQL ya no existe en el repositorio; `Front.Web` opera sobre MongoDB.

### Consumidores HTTP de SQL en Front.Web

- [x] `CatalogApiClient`, `IdentityApiClient` y `CartApiClient` fueron desregistrados del contenedor de DI en `Program.cs`.
- [x] `AccountController.Login` ya no usa `IdentityApiClient`; el login es ahora exclusivo sobre `MongoCustomerService`.
- [x] Los archivos `.cs` de los clientes HTTP (`CatalogApiClient.cs`, `IdentityApiClient.cs`, `CartApiClient.cs`) fueron eliminados del repositorio.

### Índices y nombres heredados

- Los documentos de MongoDB usan `idSqlOriginal` para mantener compatibilidad con IDs del legado. No constituye dependencia de SQL Server, pero debe documentarse para futuras migraciones a `ObjectId` o UUID propios.

## Roadmap vigente

### Etapa 1 — Estabilización de seguridad e integridad

Estado: implementada en código y pruebas unitarias; validación integrada de MongoDB pendiente.

- [x] Sustituir SHA-256 para contraseñas nuevas.
- [x] Implementar verificación compatible y rehash progresivo.
- [x] Recalcular precios desde productos durante checkout.
- [x] Validar cliente, producto activo, cantidad y stock en carrito.
- [x] Eliminar IDs calculados únicamente con máximo más uno mediante contadores atómicos.
- [x] Revisar duplicados y agregar índices únicos mediante `MongoDbInitializer`.
- [x] Ejecutar checkout dentro de una transacción MongoDB.
- [x] Probar concurrencia de checkout contra MongoDB real (2026-10-20: una sola orden y un solo descuento de stock; reintento `TransientTransactionError` agregado).
- [x] Probar rollback ante fallo parcial (2026-10-20: carrito multiartículo con segundo producto sin stock, sin orden ni descuento residual).

Criterio de terminado:

- Ninguna cuenta nueva usa SHA-256.
- Alterar el precio almacenado en carrito no altera el total final.
- Reintentar checkout no duplica una orden ni descuenta stock dos veces.
- Un fallo después de reservar stock deja un estado recuperable y trazable.
- Build y pruebas pasan sin secretos versionados.

### Etapa 2 — Customer.Api (completada)

- [x] Convertir Customer/Identity en fuente autoritativa.
- [x] Registro, login, perfil y actualización de datos.
- [x] Cambio autenticado de contraseña.
- [x] Cambio obligatorio de contraseña cuando `mustResetPassword=true`.
- [x] Recuperación mediante token opaco de un solo uso.
- [x] JWT, roles, bloqueo temporal (5 intentos / 15 min), rate limiting y políticas completas.
- [x] Migración progresiva de hashes heredados.
- [x] Retirar acceso de `Front.Web` a la colección `clientes`.
- **Hecho en este paso:** creación del proyecto `Customer.Api`; `Front.Web` consume `Customer.Api` y `AccountController` ya no tiene fallback a `MongoCustomerService`.
- **Hecho el 2026-10-27:** `Customer.Api` expone `GET/PUT /api/customers/me` y `POST /api/customers/change-password` protegidos con JWT; `Front.Web` agregó `ProfileController` con las vistas `Profile/Index` y `Profile/ChangePassword`, enlazadas desde la barra de navegación autenticada. Bloqueo temporal por fuerza bruta (5 intentos fallidos / 15 minutos), rate limiting en endpoints sensibles de `Customer.Api`, validación estricta de espacios en blanco y cambio obligatorio forzado cuando `mustResetPassword=true`.

### Etapa 3 — Order.Api (en progreso)

- Crear órdenes desde identidad autenticada y carrito server-side.
- Guardar snapshots de nombre y precio.
- Usar clave de idempotencia.
- Transacción de orden, stock y carrito cuando sea viable.
- Modelar estados y transiciones.
- Exponer historial y detalle.
- Retirar `MongoOrderService` de `Front.Web`.
- **Hecho en este paso:** creación del proyecto `Order.Api`; `CheckoutController` y `OrderController` ya no tienen fallback a `MongoOrderService`; `Front.Web` depende de HTTP/JWT para login, registro, checkout e historial.
- **Hecho el 2026-10-28:** `PUT /api/admin/orders/{id}/status` permite cambiar estado (`confirmado`, `en_proceso`, `enviado`, `entregado`, `cancelado`) con paquetería, número de guía y notas; se persiste historial en `ventas.seguimiento.historial` y `OrderEmailSender` (MailKit) notifica al cliente por correo o degrada a log si SMTP no está configurado. `Admin.Web/Orders/Details` expone el formulario de actualización.

### Etapa 4 — Pagos (integración parcial en Front.Web el 2026-10-28)

- [x] Integrar PayPal Sandbox REST v2 en `Front.Web` (`PayPalClient`: oauth2/token, create order, capture) con simulador `PAYPAL-SIM-*` cuando no hay credenciales.
- [x] Flujo de checkout con botón PayPal Sandbox en `Cart/Index`; el `captureId` se guarda como `idTransaccion` en la orden.
- [x] Decidido el 2026-11-03: el pago permanece integrado en `Front.Web` (`PayPalClient`) y no se crea `Payment.Api`. La demo no necesita despliegue, escalado ni aislamiento independientes del pago, y la captura ya se confirma contra PayPal antes de crear la orden. Si más adelante se agregan webhooks, conciliación o varios proveedores, la extracción se reevalúa.
- [ ] Configurar `PayPal:ClientId`/`ClientSecret` de Sandbox en `appsettings.Local.json` para probar contra la API real.
- [ ] Webhooks firmados como confirmación autoritativa (descartado para demo; la captura directa del botón es la confirmación).
- [ ] Idempotencia de pagos a nivel de gateway, conciliación y pruebas de timeout/webhook fuera de orden (fuera de alcance de la demo).

### Etapa 5 — APIs restantes (completada el 2026-10-14)

- [x] Extraer Catalog API para MongoDB.
- [x] Extraer Cart API.
- [x] Extraer Location API o decidir proveedor externo.
- [x] Hacer que `Front.Web` consuma exclusivamente HTTP y JWT.
- [x] Retirar `MongoDB.Driver` de `Front.Web`.

### Etapa 6 — Administración y medios (implementada; validación externa parcial)

- [x] Alta, edición y desactivación de productos, categorías y marcas.
- [x] Control de stock protegido por rol administrativo.
- [x] Auditoría básica de mutaciones.
- [x] Integración de object storage mediante Cloudinary.
- [x] `imageUrl` en contratos públicos.
- [x] Validación de MIME, tamaño y firma en cargas.
- [x] Validar Cloudinary con credenciales reales y sus casos de error (subida real desde `Admin.Web` el 2026-10-28).
- [x] Retirar rutas físicas heredadas de los datos (2026-11-03: `mongo/productos.json` ya no las contiene y `tests/MongoCleanup` borra `rutaImagen`/`nombreImagen` de los documentos existentes; los productos sin `imageUrl` siguen mostrando placeholder local).

### Etapa 7 — Madurez operativa

- Gateway/reverse proxy.
- OpenAPI y contratos versionados.
- Problem Details uniforme.
- Health checks por dependencia.
- Logs estructurados, correlation ID, métricas y trazas.
- Pruebas unitarias, integración, contrato y E2E críticas.
- CI/CD, imágenes de contenedor, escaneo de secretos y dependencias.
- Backups y simulacros de restauración.

### Etapa 8 — Retiro del legado

- Verificar que Admin y Tienda antiguas no reciben tráfico.
- Confirmar que el frontend no referencia `CapaNegocio`, `CapaDatos`, `CapaEntidad` ni SQL Server.
- Confirmar que ventas históricas están disponibles.
- Deshabilitar primero, observar y eliminar después.
- Revocar secretos y accesos del sistema retirado.

## Pendientes funcionales

- ~~Estados y transiciones de pedidos, paquetería y guía~~ **Completado el 2026-10-28** (`PUT /api/admin/orders/{id}/status` + formulario en `Admin.Web/Orders/Details`).
- PayPal: flujo Sandbox REST v2 implementado en `Front.Web` el 2026-10-28; el 2026-11-03 se decidió mantenerlo integrado en `Front.Web` sin `Payment.Api`. Pendiente probar con credenciales reales de PayPal Developer. Webhooks, idempotencia de gateway y conciliación quedan fuera del alcance de la demo.
- ~~Validación real de cargas y reemplazos de imágenes en Cloudinary~~ **Completado el 2026-10-28** (subida JPG real verificada).
- Notificaciones transaccionales de cambio de estado de pedido implementadas el 2026-10-28 en `Order.Api` (MailKit); pendiente SMTP en `Order.Api` para envío real. Recuperación de contraseña usa SMTP en `Customer.Api`/`Administration.Api`.
- Reportes administrativos adicionales a los ya implementados.
- Automatizar de extremo a extremo la carga de archivos (antiforgery ya quedó automatizado el 2026-11-03; autorización de sesión administrativa, paginación y validación local de imágenes ya tienen cobertura).
- Madurez operativa (Etapa 7): gateway, OpenAPI, observabilidad, sesiones y rate limiting distribuidos, CI/CD y backups. Fuera del alcance de la demo.
- ~~Limpieza de `rutaImagen`/`nombreImagen` legados de Windows en documentos antiguos de `productos`~~ **Completado el 2026-11-03** (dump `mongo/productos.json` y `tests/MongoCleanup`; ejecutar `dotnet run --project tests/MongoCleanup` contra la base en uso para aplicarlo).

## Configuración local y ejecución

Requisitos actuales:

- .NET SDK 8 compatible con el proyecto.
- Acceso autorizado a MongoDB.
- Valores locales para `MongoDb:ConnectionString`, `MongoDb:DatabaseName` y JWT compartido.
- `Jwt:SigningKey` de al menos 32 caracteres; issuer, audience y clave deben coincidir en todos los servicios.
- SMTP para recuperación funcional; `Email:*` en `Order.Api` para correos de estado de pedido.
- `Cloudinary:CloudName`/`ApiKey`/`ApiSecret` en `Catalog.Api` (configuradas el 2026-10-28; la carga responde `503` controlado si faltan).
- Opcional: `PayPal:ClientId`, `PayPal:ClientSecret` y `PayPal:BaseUrl` en `Front.Web` para PayPal Sandbox real; sin credenciales opera el simulador `PAYPAL-SIM-*`.

Los valores sensibles deben estar en archivos `appsettings.Local.json` de cada servicio, user-secrets o variables de entorno. `appsettings.Local.json` no se versiona.

Puertos locales:

- Front.Web: `5160`
- Customer.Api: `5161`
- Order.Api: `5162`
- Admin.Web: `5163`
- Administration.Api: `5164`
- Catalog.Api: `5165`
- Cart.Api: `5166`
- Location.Api: `5167`

Ejecución:

```bash
dotnet restore Front.Web.sln
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
./start.sh
```

`start.sh` comprueba los puertos `5160`–`5167`, levanta los ocho servicios y guarda PID y logs en `.logs/`; `stop.sh` sólo detiene los procesos que registró. La tienda queda en `http://localhost:5160` y el panel en `http://localhost:5163`.

Validación mínima antes de confirmar cambios:

```bash
dotnet list Front.Web.sln package --vulnerable --include-transitive
git diff --check
```

Con los servicios y la infraestructura local disponibles, validar también health checks de los seis APIs, registro o login, catálogo, carrito, checkout e historial, autorización administrativa (`401`, `403`, `200` según token y rol), alta y edición de categoría, marca y producto, y carga o reemplazo de imagen en Cloudinary. Conservar el E2E de concurrencia y rollback de `tests/Integration.Seed` como regresión.

## Política de seguridad

- No versionar conexiones, contraseñas, JWT, secretos de PayPal o SMTP ni datos personales.
- No copiar secretos a documentación, pruebas, comandos compartidos o logs.
- Rotar cualquier secreto que haya sido expuesto.
- No publicar respaldos o exportaciones MongoDB con datos reales.
- Usar cuentas y datos sanitizados para pruebas.
- No exponer rutas físicas de imágenes.
- No confiar en IDs de cliente, precios o totales enviados por el navegador.

## Definición de terminado por incremento

Un incremento está terminado cuando:

- El alcance y las reglas de negocio están definidos.
- Compila sin advertencias nuevas relevantes.
- Incluye pruebas proporcionales al riesgo.
- No introduce secretos ni datos personales.
- Los contratos están documentados.
- Los errores son trazables.
- Existe rollback o recuperación.
- Se validó el flujo integrado afectado.
- Se actualizó este documento.

## Historial por etapas

### Antiforgery automatizado, limpieza de imágenes heredadas y decisión de pagos — 2026-11-03

- `tests/Admin.Web.UnitTests/AntiforgeryTests.cs` cubre el antiforgery de extremo a extremo con `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing` 8.0.26): POST sin token responde `400`, POST con token de la vista responde `302`.
- `tests/MongoCleanup` borra `rutaImagen` y `nombreImagen` de `productos` y el dump `mongo/productos.json` ya no los incluye.
- El pago queda integrado en `Front.Web`; no se crea `Payment.Api` para la demo.
- `dotnet test Front.Web.sln --configuration Release` quedó en 114 pruebas aprobadas (112 previas más las 2 de antiforgery).
- Documentación unificada en un solo archivo: el antiguo `README.md` y `CONTINUACION_MIGRACION_ADMIN.md` se fusionaron en `MIGRACION.md` (componentes, puertos, comandos y validación mínima en las secciones principales; el detalle administrativo en el anexo final), se eliminaron, y `MIGRACION.md` se renombró a `README.md`. Ningún archivo de código o script referenciaba esos `.md`.
- Corroboración contra el código del 2026-11-03: puertos `5160`–`5167` de `start.sh`, rutas de los ocho proyectos, 16 proyectos en `Front.Web.sln`, ausencia de `MongoDB.Driver` en `Front.Web`, endpoints `GET/PUT /api/customers/me`, `POST /api/customers/change-password`, `GET /api/admin/customers/count`, `GET /api/admin/orders{,/{id},/dashboard,/report}`, `PUT /api/admin/orders/{id:int}/status`, `POST /api/admin/catalog/products/{id:int}/image`, estados `confirmado|en_proceso|enviado|entregado|cancelado`, máximo 99 unidades por producto en carrito, bloqueo de 5 intentos / 15 minutos en Customer y Administration, reporte limitado a 366 días y 5,000 ventas, paginación administrativa topada en 100, límite de 5 MB e `img-src https://res.cloudinary.com` en ambas CSP, y `ventas.seguimiento.historial`. Se corrigieron tres desajustes: faltaba la colección `counters` en el diagrama, y en el anexo quedaban como vigentes el acceso MongoDB de `Front.Web` y los pedidos de sólo lectura (ambos marcados como históricos).

### Paridad visual de carrito y registro — 2026-09-22

- Las vistas Razor de carrito y registro adoptaron la estructura y los estilos de `carrito.html` y `register.html`.
- El carrito conserva productos dinámicos, cantidades, eliminación, total, ubicaciones dependientes y checkout con antiforgery.
- El registro conserva el envío real, valores ingresados, errores del servidor y validación de confirmación de contraseña.
- Los estilos de ambos prototipos se aislaron en hojas independientes para no alterar las demás pantallas.
- `dotnet test Front.Web.sln --configuration Release --no-restore` completó con 7 pruebas aprobadas.
- `GET /Account/Register` respondió `200` y cargó la nueva estructura Razor y sus estilos.

### Endurecimiento de seguridad e integridad — 2026-09-22

- Los registros nuevos dejaron de usar SHA-256 y ahora usan PBKDF2-SHA256 con salt aleatorio.
- Los hashes SHA-256 existentes siguen funcionando y se migran de forma optimista al iniciar sesión.
- Se agregaron contadores MongoDB atómicos para clientes y ventas.
- El carrito exige cliente existente, producto activo, stock disponible y un máximo de 99 unidades.
- El checkout ignora el precio guardado en carrito y usa el precio vigente de `productos`.
- Orden, stock y eliminación del carrito se ejecutan en una transacción MongoDB.
- Se agregaron 7 pruebas unitarias de hashing y todas fueron aprobadas en Release.
- Quedó pendiente validar transacciones, rollback y concurrencia contra el servidor MongoDB autorizado.

### Etapa original — ASP.NET MVC 5 y SQL Server

- Solución .NET Framework 4.7.2 con capas Entidad, Datos, Negocio, Admin y Tienda.
- ADO.NET y procedimientos almacenados sobre SQL Server.
- Forms Authentication y sesión.
- PayPal, SMTP e imágenes físicas dentro del legado.
- Se validó compilación con Mono/MSBuild y ejecución con XSP4 en macOS.
- Admin respondió en el puerto `8080` y Tienda en `8081` durante esa validación.

### Primera migración — Servicios .NET 8 sobre SQL Server

- Se creó `Ecommerce.Services.sln`.
- Catalog Service implementó categorías, marcas, productos, detalle, health check y OpenAPI.
- Identity Service implementó login, JWT, compatibilidad SHA-256, rehash PBKDF2 y cambio autenticado de contraseña.
- Cart Service implementó consulta, alta, cambio de cantidad y eliminación con identidad derivada de JWT.
- Tienda MVC integró servicios mediante feature flags y conservó caminos heredados de rollback.
- La suite documentada llegó a 30 pruebas aprobadas.
- La base SQL Server y su esquema permanecieron sin cambios.

### Integración MVC validada

- Identity emitió el claim interoperable `role`.
- El JWT se almacenó en sesión del servidor.
- Se validaron login MVC, catálogo y lecturas del carrito mediante Mono/XSP.
- Quedaron pendientes las mutaciones reales controladas y el endurecimiento de checkout.

### Migración de datos y nuevo Front.Web

- Se incorporó `Front.Web` sobre ASP.NET Core 8.
- Se configuró acceso al servidor privado MongoDB.
- Catálogo, clientes, ubicaciones, carrito y órdenes se implementaron inicialmente dentro del frontend como etapa de transición.
- Se implementó registro y login de clientes.
- Se implementaron endpoints de ubicación y selects dependientes.
- Se implementó carrito persistente.
- Se implementó checkout, descuento condicional de stock, creación de venta, historial y vaciado del carrito.
- El flujo completo fue validado manualmente contra MongoDB.

### Revisión documental y técnica — 2026-09-22

- Se consolidaron los documentos de migración en este archivo.
- Se confirmó que varias listas de pendientes no reflejaban la implementación actual.
- Se definió `Front.Web` más MongoDB como estado de transición vigente.
- Se identificaron como riesgos prioritarios SHA-256 para cuentas nuevas, autenticación híbrida, precios del carrito, checkout no transaccional, IDs `max + 1` y validaciones insuficientes de carrito.
- Se priorizó estabilizar seguridad e integridad antes de PayPal o de nuevas extracciones.

## Plantilla para registrar cambios

Agregar entradas nuevas al final de esta sección:

```markdown
### AAAA-MM-DD — Título

- Estado: planificado | en progreso | completado | revertido.
- Alcance: servicio o flujo afectado.
- Cambios: lista concreta.
- Contratos: endpoints o eventos modificados.
- Datos: colecciones, índices o migraciones afectadas.
- Seguridad: efectos y controles.
- Pruebas: comandos y resultados.
- Rollback o recuperación: procedimiento.
- Pendientes: siguiente incremento.
```

### 2026-09-22 — Cierre de la plantilla de cambios

- Estado: completado.
- Alcance: documentación.
- Cambios: se cerró la plantilla de cambios.
- Contratos: no aplica.
- Datos: no aplica.
- Seguridad: no aplica.
- Pruebas: no aplica.
- Rollback o recuperación: no aplica.
- Pendientes: no aplica.

### 2026-09-22 — Inventario de dependencias SQL pendientes

- Estado: completado.
- Alcance: auditoría técnica y documentación.
- Cambios: se inspeccionó el repositorio en busca de referencias restantes a SQL Server; se documentaron artefactos legados, servicios SQL faltantes, clientes HTTP obsoletos en `Front.Web` y la convención `idSqlOriginal`.
- Contratos: no aplica.
- Datos: no aplica.
- Seguridad: el login híbrido con `IdentityApiClient` sigue siendo un riesgo pendiente.
- Pruebas: búsqueda de `ConnectionStrings`, `SqlClient`, `UseSqlServer` y referencias legadas; `Front.Web` no presenta conexiones SQL.
- Rollback o recuperación: no aplica.
- Pendientes: decidir si se archivan/eliminan los artefactos legados y si se reescriben las APIs `Cart`, `Catalog` e `Identity` a MongoDB o se descartan.

### 2026-09-24 — Eliminación del fallback a IdentityApiClient

- Estado: completado.
- Alcance: `Front.Web`.
- Cambios: se quitó el fallback a `IdentityApiClient` en `AccountController.Login`; se desregistraron `CatalogApiClient`, `IdentityApiClient` y `CartApiClient` del contenedor de DI; el login depende únicamente de `MongoCustomerService`.
- Contratos: `POST /Account/Login` sigue con el mismo contrato de entrada y salida.
- Datos: no aplica.
- Seguridad: elimina la doble vía de autenticación y reduce el riesgo de hashes/datos divergentes; requiere asegurar `MongoCustomerService` como fuente autoritativa.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: revertir los cambios en `AccountController.cs` y `Program.cs`.
- Pendientes: archivar/eliminar artefactos legados y decidir el destino de las APIs `Cart`, `Catalog` e `Identity`.

### 2026-09-24 — Limpieza de clientes HTTP y tests obsoletos

- Estado: completado.
- Alcance: `Front.Web` y `tests/`.
- Cambios: se eliminaron los archivos `CatalogApiClient.cs`, `IdentityApiClient.cs` y `CartApiClient.cs`; se eliminaron los proyectos de pruebas obsoletos `Identity.*`, `Catalog.*` y `Cart.*` que configuraban `ConnectionStrings` de SQL Server.
- Contratos: no aplica.
- Datos: no aplica.
- Seguridad: reduce el riesgo de referencias confusas a credenciales y conexiones heredadas.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: restaurar archivos y directorios desde respaldo o control de versiones.
- Pendientes: archivar/eliminar artefactos legados `Capa*` y limpiar las carpetas `obj` restantes de los servicios SQL.

### 2026-09-24 — Eliminación de artefactos legados y servicios SQL intermedios

- Estado: completado.
- Alcance: repositorio.
- Cambios: se eliminaron las carpetas `CapaDatos`, `CapaNegocio`, `CapaEntidad`, `CapaPresentacionAdmin`, `CapaPresentacionTienda`, `src/Services/Cart`, `src/Services/Catalog` y `src/Services/Identity`; no contenían código fuente activo, solo binarios, `Web.config` y carpetas `obj`.
- Contratos: no aplica.
- Datos: no aplica.
- Seguridad: retira ensamblados legados, configuraciones heredadas y posibles secretos en `.config`.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: recuperar carpetas desde respaldo; `Front.Web.sln` no se vio afectado.
- Pendientes: crear índices únicos en MongoDB, extraer `Customer.Api`, validar transacciones bajo concurrencia y reforzar la postura de seguridad.

### 2026-09-24 — Creación de índices MongoDB al inicio

- Estado: completado.
- Alcance: `Front.Web`.
- Cambios: se creó `MongoDbInitializer` como `IHostedService`; crea índices únicos para `correo` e `idSqlOriginal` de clientes, usuarios, productos, categorías, marcas y ventas, además de un índice no único en `carritos.idClienteSqlOriginal`; falla de forma controlada si existen duplicados y continúa el arranque.
- Contratos: no aplica.
- Datos: índices creados automáticamente en MongoDB al iniciar la aplicación.
- Seguridad: evita condiciones de carrera en registro de cuentas y duplicados de IDs heredados.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: eliminar los índices manualmente desde MongoDB y revertir `MongoDbInitializer`.
- Pendientes: validar creación real contra el servidor MongoDB y probar concurrencia del checkout.

### 2026-09-24 — Idempotencia y concurrencia en checkout

- Estado: completado.
- Alcance: `MongoOrderService` y `MongoDbInitializer`.
- Cambios: se promovió `checkoutKey` a ámbito externo del `try`, se agregó un índice único en `ventas.checkoutKey` y se captura `MongoWriteException` con código `11000` para devolver la orden existente en caso de carrera concurrente; la transacción se aborta antes de retornar.
- Contratos: `CreateOrderAsync` mantiene su firma y contrato; `CheckoutController` no requiere cambios.
- Datos: índice único `ventas.checkoutKey`; el campo ya estaba presente en cada orden.
- Seguridad: elimina la posibilidad de duplicar una orden o descontar stock doble si el usuario presiona el botón de pago dos veces o si el request se repite por red.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: eliminar el índice único `ventas.checkoutKey` y revertir `MongoOrderService`.
- Pendientes: validar el comportamiento contra un MongoDB con replica set y simular concurrencia con dos requests paralelos.

### 2026-09-24 — Integration.Seed: prueba end-to-end de Customer.Api y Order.Api

- Estado: completado.
- Alcance: `tests/Integration.Seed` y los servicios levantados.
- Cambios: se creó el proyecto `tests/Integration.Seed` (.NET 8 console) que lee `src/Services/Customer/Customer.Api/appsettings.Local.json`, siembra un producto, un cliente y un carrito, luego prueba registro, login, checkout e historial entre `Customer.Api` y `Order.Api`, y limpia los datos al final.
- Contratos: consume `POST /api/customers/register`, `POST /api/customers/login`, `POST /api/orders` y `GET /api/orders/me`.
- Datos: inserta y luego elimina documentos de prueba en `productos`, `clientes`, `carritos` y `ventas`.
- Seguridad: usa el token JWT de `Customer.Api` para autenticar las llamadas a `Order.Api`.
- Pruebas: ejecución exitosa: producto creado (`idSqlOriginal=10`), cliente (`idSqlOriginal=1018`), carrito, orden (`id=12, total=100, productCount=2, status=confirmado`) e historial con 1 orden.
- Rollback o recuperación: eliminar el proyecto `tests/Integration.Seed` y los datos de prueba si quedaron.
- Pendientes: reutilizar la semilla en un pipeline de CI/CD para validación continua.

### 2026-09-24 — Customer.Api: extracción inicial

- Estado: en progreso.
- Alcance: `src/Services/Customer/Customer.Api` y `Front.Web`.
- Cambios: se creó el microservicio `Customer.Api` (.NET 8 minimal API) con login, registro, hash PBKDF2, JWT y acceso a MongoDB; `Front.Web` añadió `CustomerApiClient` y `AccountController` lo usa con fallback a `MongoCustomerService`; `Program.cs` de `Customer.Api` ahora exige `MongoDb:ConnectionString` y asigna `ecommerce` como base por defecto.
- Contratos: `POST /api/customers/register` y `POST /api/customers/login`; `CustomerApiClient` mapea `LoginResponseDto` y `CustomerDto`.
- Datos: reutiliza la colección `clientes`; no hay cambio de esquema.
- Seguridad: el fallback a `MongoCustomerService` es temporal; el objetivo es que `Front.Web` dependa exclusivamente de HTTP y JWT.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias; `dotnet build src/Services/Customer/Customer.Api/Customer.Api.csproj`: 0 advertencias, 0 errores; smoke test con `GET /health` respondió `200` y `POST /api/customers/login` con credenciales inválidas respondió `401`; validación end-to-end: registro → login → `GET /api/orders/me` con token → `POST /api/orders` sin carrito (400).
- Rollback o recuperación: revertir `AccountController` y `Program.cs` para usar solo `MongoCustomerService`; eliminar usuario de prueba creado en `clientes`.
- Pendientes: agregar rate limiting y políticas en `Customer.Api`; retirar `MongoCustomerService` del DI de `Front.Web` una vez que `Customer.Api` esté validado.

### 2026-09-24 — Customer.Api: recuperación de contraseña

- Estado: completado.
- Alcance: `Customer.Api` y `Front.Web`.
- Cambios: `CustomerStore` añadió `GenerateRecoveryTokenAsync` (token de 32 bytes en base64, hash SHA-256, expira en 1 hora) y `ResetPasswordAsync` con trim de token; `Customer.Api` expone `POST /api/customers/recover` y `POST /api/customers/reset`; el endpoint `recover` envía el token por correo a través de `MailKit` e incluye un enlace directo a `/Account/Reset` usando `Email:ResetBaseUrl`; `Front.Web` añadió vistas `Recover` y `Reset`, `CustomerApiClient.RecoverPasswordAsync` y `ResetPasswordAsync`, y `AccountController` redirige automáticamente al reset; en fallos de reset se muestra el error real y en éxito se redirige al login con mensaje de confirmación.
- Contratos: `POST /api/customers/recover` (RecoverRequest) envía el token y enlace por correo; `POST /api/customers/reset` (ResetRequest) requiere email, token y nueva contraseña.
- Datos: `clientes` ahora puede contener `recoveryTokenHash` y `recoveryTokenExpiresAt`; se limpia el token tras restablecer.
- Seguridad: token opaco de un solo uso con expiración; hash SHA-256 almacenado; SMTP con `MailKit`, credenciales en `appsettings.Local.json`, `CheckCertificateRevocation = false` para evitar fallo de CRL en macOS; en `Development`, si falla el envío, se devuelve el token en la respuesta.
- Pruebas: `dotnet build src/Services/Customer/Customer.Api/Customer.Api.csproj` y `dotnet build Front.Web.sln`: 0 advertencias, 0 errores; `dotnet test Front.Web.sln --no-build`: 16 pasadas; smoke test end-to-end con Gmail: registro → recover (correo enviado y recibido) → reset por enlace del correo → login con nueva contraseña exitoso.
- Rollback o recuperación: eliminar los endpoints `recover` y `reset`, revertir `CustomerStore` y quitar `MailKit`.
- Pendientes: para producción, actualizar `MailKit` a una versión sin `NU1902` o gestionar certificados correctamente; añadir rate limiting en `Customer.Api`.

### 2026-09-24 — Order.Api: extracción inicial

- Estado: en progreso.
- Alcance: `src/Services/Order/Order.Api` y `Front.Web`.
- Cambios: se creó el microservicio `Order.Api` (.NET 8 minimal API) con autenticación JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`), `POST /api/orders` para checkout, `GET /api/orders/me` para historial y acceso a MongoDB; `Front.Web` añadió `OrderApiClient` y `CheckoutController` y `OrderController` lo usan con fallback a `MongoOrderService`.
- Contratos: `POST /api/orders` (CheckoutRequest) y `GET /api/orders/me`; `OrderApiClient` envía token Bearer y consume dichos endpoints.
- Datos: reutiliza las colecciones `clientes`, `carritos`, `productos` y `ventas`; no hay cambio de esquema.
- Seguridad: autenticación JWT en `Order.Api`; el fallback a `MongoOrderService` es temporal; el objetivo es que `Front.Web` dependa exclusivamente de HTTP y JWT.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias; `dotnet build src/Services/Order/Order.Api/Order.Api.csproj`: 0 advertencias, 0 errores; smoke test con `GET /health` respondió `200` y `GET /api/orders/me` sin token respondió `401`; validación end-to-end con `tests/Integration.Seed`: crea producto, cliente y carrito, registra/login en `Customer.Api`, checkout en `Order.Api` (`id=12, total=100, productCount=2, status=confirmado`) e historial `GET /api/orders/me` devuelve 1 orden; limpia datos al final.
- Rollback o recuperación: revertir `CheckoutController` y `OrderController` para usar solo `MongoOrderService`.
- Pendientes: modelar estados y transiciones; retirar `MongoOrderService` del DI de `Front.Web` cuando `Order.Api` esté validado.

### 2026-09-24 — Retiro de fallbacks y limpieza de Front.Web

- Estado: completado.
- Alcance: `Front.Web`.
- Cambios: `AccountController` ahora usa únicamente `CustomerApiClient`; `CheckoutController` y `OrderController` usan únicamente `OrderApiClient`; se eliminaron `MongoCustomerService.cs`, `MongoOrderService.cs`, `MongoSequenceService.cs` y `JwtTokenIssuer.cs`; `Program.cs` de `Front.Web` ya no inyecta `CustomerPasswordHasher` ni `JwtTokenIssuer`.
- Contratos: `Front.Web` depende de `Customer.Api` y `Order.Api` por HTTP/JWT para identidad y órdenes; el catálogo, ubicación y carrito siguen accediendo directamente a MongoDB.
- Datos: no aplica.
- Seguridad: elimina accesos directos a `clientes` y `ventas` desde `Front.Web`; el frontend ya no firma ni hashea tokens.
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: restaurar archivos eliminados o revertir controladores.
- Pendientes: extraer `Catalog.Api`, `Location.Api` y `Cart.Api` para retirar `MongoDB.Driver` de `Front.Web`.

### 2026-09-24 — Corrección de validación en modelos de Front.Web

- Estado: completado.
- Alcance: `Front.Web` y `tests/Front.Web.UnitTests`.
- Cambios: `LoginInputModel`, `RegisterViewModel` y `CheckoutInputModel` se convirtieron de `record` a `class`; se agregaron constructores sin parámetros para ASP.NET MVC model binding; `AccountController` se ajustó para no usar `with` en `RegisterViewModel`.
- Contratos: mismos endpoints y contratos; cambio interno del tipo de modelo.
- Datos: no aplica.
- Seguridad: evita las excepciones `InvalidOperationException` de model binding en login y registro y mantiene las reglas de validación (email, contraseña, campos de checkout).
- Pruebas: `dotnet build Front.Web.sln` y `dotnet test Front.Web.sln --no-build`: 16 pasadas, 0 fallidas, 0 advertencias.
- Rollback o recuperación: revertir los modelos a `record` y restaurar los `with` en `AccountController`.
- Pendientes: limpiar duplicados en `ventas.checkoutKey` para que el índice único se cree; verificar `Customer.Api` y `Order.Api` models.

### 2026-09-24 — Limpieza de duplicados en `ventas.checkoutKey`

- Estado: completado.
- Alcance: `tests/MongoCleanup` y base de datos MongoDB.
- Cambios: se creó el proyecto `tests/MongoCleanup` (.NET 8 console) que lee `src/Services/Customer/Customer.Api/appsettings.Local.json`, detecta `checkoutKey` duplicados en `ventas` y elimina todos menos el más antiguo.
- Contratos: no aplica.
- Datos: se encontró 1 `checkoutKey` con 7 documentos duplicados (la clave era `''`); se eliminaron 6, conservando el más antiguo.
- Seguridad: permite crear el índice único `ventas.checkoutKey` y restaurar la idempotencia del checkout.
- Pruebas: ejecución exitosa del limpiador; el índice debe crearse al reiniciar `Front.Web`.
- Rollback o recuperación: no hay rollback directo; los documentos eliminados no se recuperan.
- Pendientes: añadir `MongoDbInitializer` a `Order.Api` para que también garantice el índice; verificar que el warning desaparezca en el próximo arranque.

### 2026-10-06 — Migración del panel administrativo sobre MongoDB

- Estado: implementado en código; validación integrada con servicios externos pendiente.
- Alcance: `Admin.Web`, `Administration.Api`, `Catalog.Api`, `Customer.Api`, `Order.Api` y catálogo de `Front.Web`.
- Cambios: se creó el panel administrativo separado con login, recuperación, dashboard, administradores, productos, categorías, marcas, pedidos de sólo lectura, reportes y exportación XLSX; la tienda ahora consulta catálogo mediante HTTP.
- Contratos: nuevos endpoints `/api/admin/auth/*`, `/api/admin/users`, `/api/catalog/*`, `/api/admin/catalog/*`, `/api/admin/orders/*` y `/api/admin/customers/count`.
- Datos: se reutilizan exclusivamente las colecciones MongoDB existentes; se agregan campos opcionales de bloqueo, recuperación, versión, `imageUrl` y `imagePublicId`, además de auditoría.
- Seguridad: JWT con rol `admin`, rate limiting, bloqueo temporal, PBKDF2 y migración SHA-256, DTOs y filtros Mongo tipados, antiforgery, cookies HttpOnly/SameSite, CSP, límites de carga y validación de firma de imágenes.
- Pruebas: se agregaron pruebas unitarias de hashing, validación de credenciales y contratos de catálogo; la validación final se registra al ejecutar la solución completa.
- Rollback o recuperación: detener `Admin.Web`, `Administration.Api` y `Catalog.Api`; restaurar temporalmente el lector Mongo anterior de la tienda sin introducir SQL; los campos nuevos son aditivos.
- Pendientes: configurar secretos locales de Cloudinary/SMTP/JWT/MongoDB y ejecutar E2E contra infraestructura autorizada.

### 2026-10-08 — Validación integrada del panel administrativo y corrección de rol JWT

- Estado: completado.
- Alcance: `Admin.Web`, `Administration.Api`, `Catalog.Api`, `Customer.Api`, `Order.Api`, scripts de arranque y dependencias.
- Cambios:
  - Se cerró la actualización de paquetes interrumpida: `Snappier 1.3.0` → `1.3.1`, `MailKit` → `4.16.0`, referencias directas a `System.Net.Http 4.3.4` y `System.Text.RegularExpressions 4.3.1` en los proyectos de prueba que arrastraban transitivos vulnerables.
  - Se crearon `appsettings.Local.json` (no versionados) para `Administration.Api` y `Catalog.Api` con MongoDB y el JWT compartido; `Administration.Api` además recibió SMTP y `Email:ResetBaseUrl` apuntando a `Admin.Web`.
  - `start.sh`/`stop.sh` ahora levantan y detienen los 6 servicios (5160–5165).
  - Se corrigió un defecto de autorización: `Administration.Api`, `Catalog.Api` y `Customer.Api` configuraban `RoleClaimType = "role"`, pero `JsonWebTokenHandler` (manejador por defecto en .NET 8) mapea el claim entrante `role` a `ClaimTypes.Role`; la política `admin` nunca encontraba el rol y devolvía `403` a todos. Se retiró el override para usar el `RoleClaimType` por defecto.
  - Se agregó la pantalla `Orders/Details` en `Admin.Web` (`OrderDetailModel`, `AdminApiClient.OrderDetail`, acción `Details`, vista y enlace desde el listado), cubriendo el pendiente 8 del documento de continuación.
  - La carga de imagen sin Cloudinary configurado ahora responde `503` controlado en lugar de `500`.
- Contratos: sin cambios de contrato; el claim JWT sigue siendo `role`.
- Datos: el inicializador de `Catalog.Api`/`Administration.Api` registra código 85 (`IndexOptionsConflict`) porque los índices únicos ya existen con otro nombre; la protección de unicidad sigue vigente.
- Seguridad: validado en vivo: endpoints administrativos devuelven `401` sin token, `403` con token `role=customer` y `200` con `role=admin`; bloqueo temporal tras 5 intentos fallidos de login; concurrencia optimista devuelve `409` con `version` obsoleta.
- Pruebas: `dotnet restore`, `dotnet build --configuration Release` (0 advertencias, 0 errores), `dotnet test --no-build` (27 aprobadas) y `dotnet list package --vulnerable --include-transitive` sin vulnerabilidades. E2E real contra MongoDB Atlas: login admin, dashboard (sales/revenue), pedidos paginados, detalle de pedido `GET /api/admin/orders/13`, reporte por rango y exportación XLSX (archivo Excel válido). Regresión de tienda: `Front.Web` sirve el catálogo vía `Catalog.Api`.
- Rollback o recuperación: detener los servicios con `stop.sh`; los cambios de código son revertibles por commit.
- Pendientes: credenciales de Cloudinary para habilitar imágenes; prueba de envío SMTP real al crear administrador; pantalla de detalle de pedido ya entregada; retirar `MongoDB.Driver` de `Front.Web` (pendiente de `Cart.Api`/`Location.Api`); E2E de checkout con concurrencia.

### 2026-10-13 — Revalidación E2E y proyectos de pruebas faltantes

- Estado: completado.
- Alcance: `tests/Order.Api.UnitTests`, `tests/Admin.Web.UnitTests`, solución y validación integrada.
- Cambios: se crearon los dos proyectos de pruebas pendientes del plan y se añadieron a `Front.Web.sln`; cobertura de validación de `CheckoutRequest` (rechazo de teléfono no numérico, dirección corta, campos vacíos), resolución de `customerId` desde claims `nameidentifier`/`sub` y validación de modelos de `Admin.Web` (`ProductInput`, `ResetModel`, `LoginModel`).
- Contratos: no aplica.
- Datos: `Integration.Seed` volvió a ejecutarse completo contra MongoDB real (registro → login → checkout → historial → limpieza); se eliminó además un cliente temporal usado para probar autorización.
- Seguridad: verificado en vivo que endpoints administrativos de `Order.Api`, `Catalog.Api` y `Customer.Api` devuelven `401` sin token, `401` con token falso y `403` con JWT de cliente (`role=Customer`).
- Pruebas: `dotnet build --configuration Release` (0 advertencias, 0 errores), `dotnet test --no-build` (42 aprobadas: 17 Front.Web + 6 Administration + 4 Catalog + 8 Order + 7 Admin.Web), auditoría de paquetes vulnerables limpia; health checks OK en los cuatro APIs; `Front.Web` sirve tienda y autenticación (`/Store` 200).
- Rollback o recuperación: revertir los dos proyectos de pruebas y su registro en la solución.
- Pendientes: credenciales de Cloudinary y prueba SMTP real; pruebas automatizadas de antiforgery, carga de archivos, paginación y compensación Cloudinary; E2E de checkout con concurrencia; `Cart.Api`/`Location.Api` para retirar `MongoDB.Driver` de `Front.Web`.

### 2026-10-13 — SMTP administrativo, recuperación y scripts de ejecución

- Estado: completado salvo Cloudinary y pendientes indicados abajo.
- Alcance: `Administration.Api`, `Admin.Web`, `start.sh`, `stop.sh` y documentación.
- Cambios: se validó Gmail SMTP con STARTTLS en puerto 587; la recuperación redirige a la pantalla de nueva contraseña con correo precargado y token manual; el correo contiene enlace y token; el mínimo administrativo se ajustó a 5 caracteres; se formatearon las vistas de Account y `AccountController`. `start.sh` ahora usa puertos explícitos 5160–5165, detecta puertos ocupados, elimina PID obsoletos, espera readiness y muestra el log de cualquier fallo. `stop.sh` detiene y espera únicamente procesos registrados por el script.
- Contratos: se mantienen `/api/admin/auth/recover` y `/api/admin/auth/reset`; el token sigue siendo opaco, expira en una hora y es de un solo uso.
- Datos: al completar el reset se actualiza el hash PBKDF2 y se eliminan el hash y expiración del token de recuperación.
- Seguridad: la respuesta de recuperación permanece uniforme; no se entrega el token por la respuesta HTTP. Para ngrok, `Email:ResetBaseUrl` debe apuntar a la URL HTTPS pública de Admin.Web, no a Administration.Api.
- Pruebas: envío real recibido; reset devolvió `204`; contraseña anterior devolvió `401`; contraseña nueva devolvió `200`; dashboard y APIs administrativas devolvieron `200`; build Release completado con 0 errores y 0 advertencias.
- Rollback o recuperación: restaurar el flujo anterior de `AccountController`, las vistas y la longitud mínima; los scripts previos pueden recuperarse desde control de versiones.
- Pendientes: probar correo al crear un administrador; corregir warning MongoDB 85; pruebas automatizadas de antiforgery/paginación/autorización; Cloudinary cuando haya credenciales; checkout concurrente y rollback; extraer `Cart.Api`/`Location.Api`; formatear vistas administrativas restantes.

### 2026-10-14 — Índices equivalentes y consolidación documental

- Estado: completado y validado.
- Alcance: `Administration.Api`, `Catalog.Api`, `Customer.Api`, proyectos de pruebas y documentación principal.
- Cambios: los inicializadores enumeran índices existentes y no intentan recrearlos cuando ya existe una clave única equivalente con otro nombre; se actualizaron transitivos vulnerables; `README.md`, el estado ejecutivo, la arquitectura y el roadmap se actualizaron para reflejar el uso real de APIs y el acceso MongoDB transitorio limitado a carrito y ubicaciones.
- Contratos: sin cambios.
- Datos: no se crean, renombran ni eliminan índices existentes; se conserva cualquier índice único equivalente.
- Seguridad: se mantiene la garantía de unicidad de correos e identificadores heredados.
- Pruebas: `dotnet test Front.Web.sln --configuration Release` completó con 46 pruebas aprobadas; se agregaron cuatro casos sobre equivalencia, unicidad, índices compuestos y dirección; la auditoría de paquetes quedó sin vulnerabilidades.
- Validación integrada: los seis servicios arrancaron correctamente; Administration y Catalog reconocieron los índices existentes contra MongoDB sin registrar código 85.
- Rollback o recuperación: revertir los inicializadores y referencias directas de paquetes; no se aplicaron cambios destructivos a datos.
- Pendientes: correo de establecimiento de administrador; Cloudinary; antiforgery/paginación/autorización automatizados; checkout concurrente y rollback; `Cart.Api`/`Location.Api`.

### 2026-10-14 — Extracción de Cart.Api y Location.Api; Front.Web sin MongoDB

- Estado: completado y validado.
- Alcance: `Cart.Api` (nuevo, puerto 5166), `Location.Api` (nuevo, puerto 5167), `Front.Web`, scripts de arranque y solución.
- Cambios:
  - `Location.Api` expone lectura pública `/api/location/states`, `/api/location/municipalities/{id}` y `/api/location/localities/{id}` con validación `LocationIdValidator` (`^[A-Za-z0-9_-]{1,40}$`), propietaria de `estados`.
  - `Cart.Api` exige JWT de cliente y deriva `customerId` del claim `nameidentifier`/`sub`; expone `GET/POST/DELETE /api/cart*` con la misma lógica de `MongoCartService` (producto activo, stock, máximo 99, reconstrucción de precio desde `productos`), propietaria de `carritos`.
  - `Front.Web` usa `CartApiClient` y `LocationApiClient` con `IHttpClientFactory`; el carrito viaja con el JWT de sesión (`AccessToken`) y ya no con el `CustomerId` numérico.
  - Se eliminaron `MongoCartService`, `MongoLocationService`, `MongoCatalogService` (código muerto) y `MongoDbInitializer`, junto con `MongoDB.Driver`, `SharpCompress`, `Snappier` y `System.IdentityModel.Tokens.Jwt` de `Front.Web.csproj`.
  - `start.sh`/`stop.sh` levantan/detienen los 8 servicios (5160–5167); `Integration.Seed` ahora cubre registro → login → consulta/alta/cambio de carrito vía Cart.Api → checkout → historial → carrito vacío → limpieza.
- Contratos: `CartController` mantiene sus rutas MVC; `api/location/*` de Front.Web conserva sus rutas públicas y validación; Cart.Api no acepta `customerId` del cliente en ningún contrato.
- Datos: `carritos` y `estados` sin cambio de esquema; `Order.Api` sigue leyendo y eliminando `carritos` dentro de la transacción de checkout (propiedad compartida transitoria documentada).
- Seguridad: mutaciones de carrito requieren JWT de cliente emitido por `Customer.Api`; verificado en vivo `401` sin token y con token falso; `400` ante IDs de ubicación inválidos.
- Pruebas: suite con 72 aprobadas (16 nuevas: 7 Cart.Api + 9 Location.Api); `Integration.Seed` E2E real completó con `=== OK ===`; los 8 servicios levantaron correctamente.
- Rollback o recuperación: restaurar los servicios Mongo en `Front.Web`, revertir controladores y `Program.cs`; las colecciones no fueron modificadas estructuralmente.
- Pendientes: correo de establecimiento de administrador; catálogo manual y Cloudinary; antiforgery E2E con `WebApplicationFactory` y carga de archivos; checkout concurrente y rollback; decidir si `Order.Api` consulta `carritos` vía `Cart.Api` o conserva el acceso transitorio documentado; pagos y madurez operativa.

### 2026-10-14 — Pruebas de seguridad administrativa y paginación

- Estado: completado y validado.
- Alcance: `Admin.Web`, `Order.Api`, `Admin.Web.UnitTests` y `Order.Api.UnitTests`.
- Cambios: la protección por sesión administrativa se extrajo al atributo `AdminSessionRequirementAttribute` (filtro de autorización declarativo aplicado a `SecureController`), reemplazando la validación imperativa en `OnActionExecuting`; la normalización de paginación administrativa se extrajo a `OrderPagination.Normalize` con tope documentado de 100 elementos por página.
- Contratos: sin cambios; `page` y `pageSize` siguen normalizándose con los mismos límites.
- Datos: no aplica.
- Seguridad: la exigencia de `AdminToken` ahora es declarativa, verificable por reflexión y cubierta por pruebas; se confirmó que los controladores públicos (Account y Home) quedan fuera del requisito.
- Pruebas: suite completa con 56 aprobadas; se agregaron 4 pruebas de seguridad en Admin.Web (cobertura de todos los controladores seguros, exclusión de públicos, redirección sin token y acceso con token) y 6 de paginación en Order.Api (límites inferior y superior).
- Rollback o recuperación: revertir `SecureController` a la validación imperativa anterior y eliminar `AdminSessionRequirementAttribute`, `OrderPagination` y sus pruebas.
- Pendientes: correo de establecimiento de administrador; catálogo manual y Cloudinary; pruebas de antiforgery de extremo a extremo con `WebApplicationFactory`; checkout concurrente y rollback; `Cart.Api`/`Location.Api`.

### 2026-10-20 — Reintento de checkout y E2E de concurrencia

- Estado: completado y validado en vivo.
- Alcance: `Order.Api` y `Integration.Seed`.
- Cambios: `OrderStore.CreateOrderAsync` extrajo el intento transaccional a `CreateOrderAttemptAsync` y reintenta hasta 3 veces cuando MongoDB devuelve una excepción con etiqueta `TransientTransactionError` (conflicto de escritura entre transacciones concurrentes); antes el conflicto propagaba un error `500`. `Integration.Seed` agregó la sección 9, que crea un segundo carrito, dispara dos `POST /api/orders` paralelos con el mismo JWT y verifica en MongoDB el resultado.
- Contratos: sin cambios; el request perdedor de la carrera devuelve `400` (carrito ya procesado) en lugar de `500`.
- Datos: no aplica.
- Seguridad: sin cambios; el `customerId` sigue derivándose del JWT.
- Pruebas: E2E ejecutado con `=== OK ===`: dos checkouts paralelos produjeron exactamente una orden (`checkoutKey` único), el stock pasó de 98 a 96 (un solo descuento) y el carrito quedó eliminado; las respuestas fueron `200` y `400`, sin errores de servidor. La sección 10 comprobó rollback de un carrito multiartículo con segundo producto sin stock: respuesta `400`, cero órdenes, stock sin cambios y carrito conservado. Suite completa: 75 pruebas aprobadas.
- Rollback o recuperación: revertir el bucle de reintento y las secciones 9-10 de `Integration.Seed`.
- Pendientes: catálogo manual y Cloudinary; antiforgery E2E con `WebApplicationFactory`; pagos y madurez operativa.

### 2026-10-20 — Correo de establecimiento administrativo y validaciones de imagen

- Estado: SMTP completado y validado en vivo; Cloudinary real pendiente por ausencia de cuenta y credenciales.
- Alcance: `Administration.Api`, `Catalog.Api`, `Integration.Seed` y `Catalog.Api.UnitTests`.
- Cambios: la creación de administradores conserva el token inicial generado por `AdminStore`, lo envía mediante `AdminEmailSender` y sólo expone `AdminProfile` en la respuesta. El cliente SMTP migró de `System.Net.Mail` a MailKit 4.16.0 para negociar STARTTLS/TLS implícito automáticamente; la consulta de revocación permanece activa por defecto y puede deshabilitarse explícitamente en configuración local cuando macOS no puede completarla. `Integration.Seed` admite el opt-in `RUN_EMAIL_E2E=true`, crea administradores temporales y limpia sus documentos. La validación de tamaño y magic bytes de imagen ahora ocurre antes de comprobar la configuración de Cloudinary.
- Contratos: `POST /api/admin/users` conserva su respuesta pública `AdminProfile`; el token de establecimiento no se expone.
- Seguridad: las credenciales y destinatario no se imprimen; el destinatario E2E usa un alias temporal; los usuarios de prueba se eliminan; no se desactiva la validación de cadena ni hostname TLS.
- Pruebas: `POST /api/admin/users` devolvió `201` y el servidor SMTP aceptó el mensaje. Se agregaron 3 pruebas de imagen: SVG rechazado, archivo mayor de 5 MB rechazado y PNG válido que exige configuración Cloudinary. Suite completa: 75 aprobadas.
- Rollback o recuperación: volver a `System.Net.Mail`, retirar `AdminCreation` y las secciones opcionales del seed; no hay migración de datos.
- Pendientes: confirmar visualmente la recepción del mensaje; crear una cuenta Cloudinary si el proyecto decide mantener ese proveedor y validar carga/reemplazo/compensación reales.

### 2026-10-27 — Perfil de cliente y cambio autenticado de contraseña

- Estado: completado y validado en vivo.
- Alcance: `Customer.Api`, `Front.Web` y `tests/Customer.Api.UnitTests` (nuevo proyecto).
- Cambios: `Customer.Api` agregó `UpdateProfileRequest`, `ChangePasswordRequest` y `ChangePasswordResult`; `CustomerStore.UpdateProfileAsync` actualiza nombre/apellido con `FindOneAndUpdateAsync`; `CustomerStore.ChangePasswordAsync` verifica la contraseña actual con `CustomerPasswordHasher.Verify` y rehashea con PBKDF2-SHA256 usando un filtro optimista sobre el hash leído. Se expusieron `GET/PUT /api/customers/me` y `POST /api/customers/change-password`, los tres protegidos con `RequireAuthorization()` y resolviendo `customerId` desde los claims `nameidentifier`/`sub` del JWT, igual que en `Cart.Api`/`Order.Api`. `Front.Web` agregó los métodos correspondientes a `CustomerApiClient`, los modelos `ProfileViewModel`/`ChangePasswordViewModel`, el nuevo `ProfileController` (acciones `Index` y `ChangePassword`, con antiforgery y rate limiting en el cambio de contraseña) y las vistas `Profile/Index.cshtml` y `Profile/ChangePassword.cshtml`; se agregó el enlace "Mi perfil" a la barra de navegación autenticada. `StoreController.Index` se envolvió en `try/catch (HttpRequestException)` para degradar con catálogo vacío si `Catalog.Api` no responde, consistente con el resto de controladores.
- Contratos: nuevos endpoints `GET /api/customers/me`, `PUT /api/customers/me` (`UpdateProfileRequest`: `firstName`, `lastName`) y `POST /api/customers/change-password` (`ChangePasswordRequest`: `currentPassword`, `newPassword`); todos requieren `Authorization: Bearer <token>` de `Customer.Api`. No se expone ni acepta `customerId` desde el cliente.
- Datos: no hay cambio de esquema; se reutilizan los campos existentes `nombres`, `apellidos` y `clave` de `clientes`.
- Seguridad: el cambio de contraseña exige conocer la contraseña actual (comparación en tiempo constante vía `CustomerPasswordHasher.Verify`); el perfil y la contraseña sólo pueden modificarse para el `customerId` derivado del JWT, nunca para un ID enviado por el cliente; se validó en vivo `401` sin token y con token falso en los tres endpoints.
- Pruebas: se creó `tests/Customer.Api.UnitTests` (8 pruebas: round-trip PBKDF2, rehash de SHA-256 heredado, validación de `UpdateProfileRequest` y `ChangePasswordRequest`) y se agregaron 4 pruebas a `Front.Web.UnitTests` para `ProfileViewModel`/`ChangePasswordViewModel`. Suite completa: 88 aprobadas (antes 75). `dotnet build Front.Web.sln --configuration Release`: 0 advertencias, 0 errores. `dotnet list package --vulnerable --include-transitive`: sin vulnerabilidades. Validación en vivo contra MongoDB real: registro → login → `GET /api/customers/me` (`200`) → `PUT /api/customers/me` (`200`, y `400` con nombre vacío) → `POST /api/customers/change-password` con contraseña actual incorrecta (`400`) y correcta (`204`) → login con contraseña anterior (`401`) y con la nueva (`200`). Los dos clientes de prueba creados para la validación se eliminaron de `clientes` al finalizar.
- Rollback o recuperación: eliminar `ProfileController`, las vistas `Profile/*`, los métodos nuevos de `CustomerApiClient` y el enlace del layout; revertir los tres endpoints y los métodos nuevos de `CustomerStore` en `Customer.Api`; eliminar `tests/Customer.Api.UnitTests` y quitarlo de `Front.Web.sln`. Ningún cambio es destructivo sobre datos existentes.
- Pendientes: forzar el cambio de contraseña cuando `mustResetPassword` es verdadero; agregar bloqueo temporal y rate limiting de login a `Customer.Api` (hoy sólo cubre `Administration.Api`); continuar con Payment.Api o estados de pedidos según prioridad de negocio.

### 2026-10-27 — Bloqueo temporal, rate limiting en Customer.Api y cambio obligatorio de contraseña

- Estado: completado y validado con pruebas unitarias y compilación Release.
- Alcance: `Customer.Api`, `Front.Web`, `tests/Customer.Api.UnitTests` y `tests/Front.Web.UnitTests`.
- Cambios:
  - **Defectos de code review corregidos**: `CustomerStore.ChangePasswordAsync` ahora utiliza `FindOneAndUpdateAsync` atómico con filtro optimista sobre el hash previo y devuelve `ConcurrencyConflict` si el hash cambió entre la lectura y la actualización, evitando falsos éxitos. Se agregó `IValidatableObject` en los modelos de request (`RegisterRequest`, `UpdateProfileRequest`, `ChangePasswordRequest`, `ProfileViewModel`, `ChangePasswordViewModel`) para rechazar cadenas de solo espacios en blanco antes de invocar los hashers y evitar excepciones 500 no capturadas. En `ProfileController`, `model.Success` y `ViewData["Success"]` se unificaron limpiamente.
  - **Bloqueo temporal por fuerza bruta**: `CustomerStore.AuthenticateAsync` implementó contadores de fallos y bloqueo idénticos a `Administration.Api` (bloqueo por 15 minutos tras 5 intentos fallidos consecutivos). Al autenticarse correctamente, restablecer o cambiar contraseña, se reinician `failedLoginCount` y `lockoutEnd`. `Customer.Api/Program.cs` devuelve HTTP 423 (Locked) ante cuentas bloqueadas. `Front.Web` detecta el estado 423 y muestra advertencia informativa al cliente.
  - **Rate Limiting en Customer.Api**: se registró middleware y política de rate limiting fija (`PermitLimit = 10`, ventana de 1 minuto por IP/UserAgent) en `POST /api/customers/login`, `POST /api/customers/recover`, `POST /api/customers/reset`, `POST /api/customers/register` y `POST /api/customers/change-password`.
  - **Cambio obligatorio de contraseña (`mustResetPassword=true`)**: al iniciar sesión con `MustResetPassword=true`, `Front.Web` guarda la bandera en sesión de servidor y redirige forzosamente a `/Profile/ChangePassword`. Se agregó un middleware en el pipeline de `Front.Web` que intercepta cualquier intento de navegar a la tienda, carrito o checkout mientras la bandera permanezca activa, forzando al usuario a cambiar su contraseña antes de continuar (permitiendo solo el cambio de contraseña o salir de la sesión). `PUT /api/customers/me` en `Customer.Api` valida además el claim `must_reset_password` y rechaza mutaciones de perfil con 403 Forbidden hasta que se actualice la clave.
  - **Refresco de JWT tras cambio de contraseña**: `POST /api/customers/change-password` devuelve un nuevo token JWT emitido sin `must_reset_password`, el cual `ProfileController` actualiza en sesión permitiendo continuar la navegación fluida sin tener que reiniciar sesión.
- Contratos: `POST /api/customers/login` puede devolver 423 Locked; `POST /api/customers/change-password` responde 200 OK con `{ accessToken, expiresAt }` al cambiar exitosamente.
- Seguridad: protección contra ataques de fuerza bruta en cuentas de clientes, limitación de tasa por IP contra scraping/fuerza bruta de endpoints de autenticación, rechazo de contraseñas de solo espacios y forzado del cambio de credenciales provisionales/heredadas antes de interactuar con el comercio.
- Pruebas: `UnitTest1.cs` renombrado y reformateado a `SecurityTests.cs` en `Customer.Api.UnitTests`. Cobertura total de pruebas unitarias aumentó a **100 pruebas aprobadas** (0 fallidas, 0 omitidas). Auditoría de paquetes limpia (0 vulnerabilidades).
- Rollback o recuperación: revertir commits correspondientes a `CustomerStore.cs`, `Program.cs`, `CustomerApiClient.cs`, `AccountController.cs` y `ProfileController.cs`. No requiere migraciones destructivas sobre MongoDB.
- Pendientes: estados y transiciones de pedidos; Payment.Api (PayPal); Cloudinary real.

### 2026-10-28 — Estados de pedidos, paquetería, notificaciones por correo y PayPal Sandbox REST v2

- Estado: completado y validado con pruebas unitarias, compilación Release y auditoría de paquetes.
- Alcance: `Order.Api`, `Admin.Web`, `Front.Web`, `tests/Order.Api.UnitTests` y `tests/Front.Web.UnitTests`.
- Cambios:
  - **Estados de pedidos y paquetería**: se agregó `UpdateOrderStatusRequest` a `Order.Api` (`confirmado`, `en_proceso`, `enviado`, `entregado`, `cancelado`) y el endpoint `PUT /api/admin/orders/{id}/status` con registro histórico en `seguimiento.historial`. En `Admin.Web`, `Orders/Details` ahora incluye el formulario para actualizar estado, paquetería, número de guía y notas.
  - **Notificaciones por correo de status**: se implementó `OrderEmailSender` en `Order.Api` usando `MailKit 4.16.0` y `System.Security.Cryptography.Pkcs 8.0.2` para enviar un correo resumen al cliente al cambiar el estado de su pedido, con degradación controlada a log en modo demo cuando SMTP no está configurado.
  - **PayPal Sandbox REST v2**: se creó `PayPalClient` en `Front.Web` que soporta la API REST v2 de PayPal Sandbox (`/v1/oauth2/token`, `/v2/checkout/orders` y `/v2/checkout/orders/{id}/capture`), con modo simulador sandbox para demos locales sin credenciales de desarrollador. En `Front.Web`, el carrito y checkout (`Views/Cart/Index.cshtml`) agregaron el botón de pago con PayPal Sandbox y los endpoints `CreatePayPalOrder` y `CapturePayPalOrder` que asocian el `IdTransaccion` de PayPal a la orden creada.
  - **Manejo de errores de Cloudinary**: `CatalogController` en `Admin.Web` ahora distingue explícitamente cuando el guardado fue exitoso pero la subida de imagen devolvió `503 Service Unavailable` por falta de configuración de Cloudinary en `Catalog.Api`, mostrando una advertencia informativa (`.notice.warning`) en lugar de un error de versión.
- Contratos: nuevos endpoints `PUT /api/admin/orders/{id}/status` en `Order.Api`, `POST /Checkout/CreatePayPalOrder` y `POST /Checkout/CapturePayPalOrder` en `Front.Web`. `CheckoutRequest` en `Order.Api` ahora admite `IdTransaccion` opcional.
- Datos: `ventas.seguimiento` ahora persiste transiciones de estado, paquetería y número de guía en la base MongoDB.
- Seguridad: todos los endpoints administrativos requieren rol `admin`; la creación y captura de PayPal exigen sesión autenticada con JWT de cliente y validan antiforgery; la auditoría de vulnerabilidades quedó con 0 paquetes vulnerables.
- Pruebas: se agregaron pruebas para `UpdateOrderStatusRequest` en `Order.Api.UnitTests` y para `PayPalClient` y `CheckoutInputModel` en `Front.Web.UnitTests`. Suite completa: **112 pruebas aprobadas** (0 fallidas, 0 omitidas). Build en Release sin errores ni advertencias.
- Rollback o recuperación: revertir cambios en `Order.Api`, `Admin.Web` y `Front.Web`; los campos agregados en MongoDB son compatibles y no destructivos.
- Pendientes: configurar credenciales locales de Cloudinary y PayPal Sandbox en archivos `appsettings.Local.json` cuando se deseen probar contra los servicios en vivo de terceros.

### 2026-10-28 — Cloudinary en vivo, propagación de imageUrl al frontend y corrección de versión heredada

- Estado: completado y validado en vivo.
- Alcance: `Catalog.Api`, `Cart.Api`, `Admin.Web`, `Front.Web` y configuración local.
- Cambios:
  - **Credenciales Cloudinary configuradas**: `Catalog.Api/appsettings.Local.json` (no versionado) recibió `Cloudinary:CloudName`, `ApiKey` y `ApiSecret`. Se validó en vivo: subida real de JPG desde `Admin.Web` → `POST /api/admin/catalog/products/{id}/image` → `res.cloudinary.com` responde `SecureUrl` y `publicId`; el documento `productos` quedó con `imageUrl` y `imagePublicId`.
  - **Defecto de concurrencia optimista con productos heredados corregido**: los documentos migrados desde SQL Server no tienen campo `version` en MongoDB (ej. `idSqlOriginal=8`). El filtro `Eq("version", r.Version)` no coincidía cuando `version` está ausente y `PUT /api/admin/catalog/products/{id}` devolvía `409` antes de siquiera llegar a la subida de imagen. Se agregó `VersionFilter` en `CatalogStore`: cuando el cliente reporta versión `0`, acepta documentos con `version` en `0`, `null` o inexistente; el `$inc` inicializa el campo en la primera escritura. Aplicado también en `SetStockAsync`.
  - **Propagación de imageUrl a la tienda**: `ProductDto` y `CartItemDto` en `Front.Web` ahora deserializan `imageUrl`; `Cart.Api` agregó `imageUrl` a `CartItemResponse` (leída de `productos`); las vistas `Store/Index`, `Store/Detail` y `Cart/Index` muestran la imagen de Cloudinary con fallback a `~/images/computadora.png` cuando el producto no tiene imagen.
  - **CSP de Front.Web actualizada**: `img-src` pasó de `'self' data:` a `'self' data: https://res.cloudinary.com`; sin esto el navegador bloqueaba las imágenes externas aunque el HTML las referenciara correctamente.
  - **Auditoría de paquetes**: `System.Security.Cryptography.Pkcs` se fijó en `8.0.2` como referencia directa en `Order.Api` y `Order.Api.UnitTests` para eliminar el transitivo vulnerable `8.0.1` arrastrado por MailKit.
- Contratos: `CartItemResponse` ahora incluye `imageUrl` opcional; `ProductResponse`/`ProductDto` ya lo exponían.
- Datos: los productos heredados sin `version` se normalizan a `version=1` en su primera edición desde el panel.
- Seguridad: la subida de imágenes sigue validando MIME/tamaño/firma antes de invocar Cloudinary; la CSP sigue restringida a un único dominio externo de imágenes.
- Pruebas: suite completa en Release con **112 aprobadas**, 0 fallidas; `dotnet list package --vulnerable --include-transitive` limpio; validación visual confirmada por el usuario: imagen JPG subida desde `Admin.Web` y renderizada en `Front.Web`.
- Rollback o recuperación: revertir `VersionFilter`, el campo `imageUrl` en contratos y el cambio de CSP; eliminar las credenciales de `appsettings.Local.json`.
- Pendientes: migrar/limpiar `rutaImagen`/`nombreImagen` legados de Windows en documentos antiguos (opcional; los productos sin `imageUrl` muestran placeholder local); configurar credenciales reales de PayPal Sandbox cuando se quiera el flujo contra la API v2 en vivo (sin credenciales opera el simulador `PAYPAL-SIM-*`); notificaciones por correo de status de pedido ya implementadas, requieren SMTP en `Order.Api` para envío real.

## Anexo — Detalle del incremento administrativo

Esta sección era el archivo `CONTINUACION_MIGRACION_ADMIN.md`. Conserva el detalle fino de la migración del panel administrativo: archivos por servicio, verificaciones, configuración por servicio, validaciones integradas y limitaciones conocidas.

### Estado general

La estructura principal y un primer vertical funcional quedaron implementados:

- `Administration.Api`: autenticación y gestión de administradores.
- `Catalog.Api`: lectura pública y administración de categorías, marcas, productos, stock e imágenes.
- `Admin.Web`: panel MVC separado con las secciones principales.
- `Customer.Api`: métrica administrativa de clientes protegida por rol.
- `Order.Api`: consulta administrativa, dashboard y reportes de ventas.
- `Cart.Api` (2026-10-14): carrito persistente por cliente autenticado con JWT.
- `Location.Api` (2026-10-14): lectura pública de estados, municipios y localidades.
- `Front.Web`: consume exclusivamente HTTP/JWT; `MongoDB.Driver` retirado el 2026-10-14.
- Solución y pruebas unitarias actualizadas.

El código activo no incorpora SQL Server. Se buscó `SqlClient`, `UseSqlServer`, `EntityFrameworkCore.SqlServer` y `ConnectionStrings` dentro de `src/` sin encontrar coincidencias.

### Trabajo implementado

#### 1. Administration.Api

Ruta: `src/Services/Administration/Administration.Api`

Incluye:

- MongoDB como única persistencia, usando la colección `usuarios`.
- Login de administradores activos.
- JWT con claim `role=admin`, issuer, audience, expiración y firma HMAC.
- Compatibilidad con SHA-256 heredado y migración progresiva a PBKDF2-SHA256.
- Comparaciones criptográficas en tiempo constante.
- Conteo de intentos fallidos y bloqueo temporal después de cinco fallos.
- Rate limiting para login y recuperación.
- Recuperación mediante token opaco aleatorio, hash almacenado, expiración y uso único.
- Listado, creación, edición y activación/desactivación de administradores.
- Índices únicos para correo e `idSqlOriginal`.
- Contadores MongoDB atómicos.
- Auditoría básica de mutaciones en la colección `auditoria`.
- Envío SMTP para enlaces de recuperación/establecimiento de contraseña.
- Problem Details y autorización por política `admin`.

Archivos principales:

- `Program.cs`
- `Models/AdminModels.cs`
- `Services/AdminStore.cs`
- `Services/AdminPasswordHasher.cs`
- `Services/AdminJwtIssuer.cs`
- `Services/AdminDbInitializer.cs`
- `Services/AdminEmailSender.cs`

#### 2. Catalog.Api

Ruta: `src/Services/Catalog/Catalog.Api`

Incluye:

- Lecturas públicas de categorías, marcas, productos y detalle.
- Filtros tipados por IDs de categoría y marca.
- Endpoints administrativos protegidos por rol.
- Alta y edición/activación de categorías y marcas.
- Alta y edición de productos.
- Actualización de stock con campo `version` para concurrencia optimista.
- Referencias a documentos MongoDB existentes.
- Compatibilidad con `idSqlOriginal`.
- Contadores atómicos e índices únicos.
- Auditoría de mutaciones.
- Integración de Cloudinary encapsulada en `CloudinaryImageStore`.
- Validación de límite de 5 MB y magic bytes para JPEG, PNG y WebP.
- Compensación básica si la imagen se carga pero el producto no existe.
- Almacenamiento de `imageUrl` y `imagePublicId`.
- DTOs fuertemente tipados; no se aceptan filtros BSON/JSON arbitrarios.

Archivos principales:

- `Program.cs`
- `Models/CatalogModels.cs`
- `Services/CatalogStore.cs`
- `Services/CloudinaryImageStore.cs`
- `Services/CatalogDbInitializer.cs`
- `Services/MongoSequenceService.cs`

#### 3. Admin.Web

Ruta: `src/Services/Front/Admin.Web`

Incluye:

- Aplicación MVC separada de la tienda.
- Diseño inspirado en `src/Services/Front/admin.html`.
- Login, recuperación, reset y logout POST.
- Dashboard con clientes, ventas, ingresos, productos y stock bajo.
- Pantallas para productos, categorías, marcas y administradores.
- Formularios de alta y edición.
- Carga de imágenes de productos.
- Pedidos de sólo lectura. **Histórico:** desde el 2026-10-28 incluyen cambio de estado, paquetería y guía.
- Reportes por rango/transacción.
- Exportación XLSX con ClosedXML.
- Clientes HTTP tipados mediante `IHttpClientFactory`.
- JWT conservado en sesión del servidor, no en JavaScript ni localStorage.
- Cookie de sesión HttpOnly, SameSite Strict y Secure en producción.
- Antiforgery global en mutaciones MVC.
- CSP, `X-Frame-Options`, `nosniff`, no-cache y Permissions Policy.
- Límites de formularios y cargas.

Archivos principales:

- `Program.cs`
- `Models/AdminModels.cs`
- `Services/AdminApiClient.cs`
- `Controllers/AccountController.cs`
- `Controllers/DashboardController.cs`
- `Controllers/CatalogController.cs`
- `Controllers/AdminUsersController.cs`
- `Controllers/OrdersController.cs`
- `Controllers/SecureController.cs`
- `Views/**`
- `wwwroot/css/admin.css`

#### 4. Customer.Api

Cambios:

- Se agregó validación JWT Bearer.
- Se agregó política `admin`.
- Se agregó `GET /api/admin/customers/count`.
- Se agregó `CustomerStore.CountAsync`.
- Se corrigieron atributos de validación de records usando `property:` para que `Validator.TryValidateObject` realmente valide sus propiedades.

#### 5. Order.Api

Cambios:

- Se agregó política `admin`.
- Endpoints administrativos:
  - `GET /api/admin/orders`
  - `GET /api/admin/orders/{id}`
  - `GET /api/admin/orders/dashboard`
  - `GET /api/admin/orders/report`
- Paginación limitada a 100 elementos.
- Filtros tipados de fechas, transacción y estado.
- Detalle de orden de sólo lectura.
- Dashboard de ventas e ingresos.
- Reporte limitado a un año y 5,000 filas.
- Se corrigieron atributos de validación de records usando `property:`.

#### 6. Front.Web

Cambios:

- Se creó `Services/CatalogApiClient.cs`.
- `StoreController` ahora consume `Catalog.Api`.
- Se retiró el registro de `MongoCatalogService` del contenedor.
- `Front.Web` todavía conserva MongoDB para carrito y ubicaciones, tal como estaba previsto. **Histórico:** este acceso se retiró el 2026-10-14 al extraer `Cart.Api` y `Location.Api`; hoy `Front.Web` no referencia `MongoDB.Driver`.
- `appsettings.json` ahora contiene URLs vigentes de Catalog, Customer y Order.

#### 7. Solución y pruebas

Se añadieron a `Front.Web.sln`:

- Administration.Api
- Catalog.Api
- Customer.Api
- Order.Api
- Admin.Web
- Administration.Api.UnitTests
- Catalog.Api.UnitTests

Pruebas agregadas:

- PBKDF2 correcto e incorrecto.
- Migración de SHA-256 heredado.
- Validación de correo y contraseña de reset.
- Validación de precio y stock.
- Comprobación de que el contrato de catálogo no admite campos dinámicos de filtro.

También se corrigió una vulnerabilidad funcional descubierta por las pruebas: los atributos colocados en parámetros de records no eran leídos por `Validator.TryValidateObject`. Se cambiaron a atributos con target `property:` en Administration, Catalog, Customer y Order.

### Verificaciones completadas

Antes del último ajuste de paquetes se ejecutó exitosamente:

```bash
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
```

Resultado:

- Build: 0 errores y 0 advertencias.
- Front.Web.UnitTests: 17 aprobadas.
- Administration.Api.UnitTests: 6 aprobadas.
- Catalog.Api.UnitTests: 4 aprobadas.
- Total: 27 pruebas aprobadas.

También se ejecutó:

```bash
git diff --check
```

Sin errores reportados.

Se verificó que no hubiera secretos no vacíos en JSON versionados mediante búsqueda de `ApiSecret`, `SigningKey` y `ConnectionString`.

### Estado de dependencias y trabajo interrumpido

La auditoría:

```bash
dotnet list Front.Web.sln package --vulnerable --include-transitive
```

reportó inicialmente:

- `SharpCompress 0.30.1`: vulnerabilidad moderada.
- `Snappier 1.0.0`: vulnerabilidad alta.
- `MailKit 4.11.0` y `MimeKit 4.11.0`: vulnerabilidades moderadas.
- En algunos proyectos de prueba, `System.Net.Http 4.3.0` y `System.Text.RegularExpressions 4.3.0` transitivos.

Se actualizó:

- `MongoDB.Driver` de `2.28.0` a `3.4.3` en todos los proyectos MongoDB.
- MailKit se intentó actualizar; la versión `4.14.2` no existe y NuGet resolvió `4.15.0`.
- Se agregaron referencias directas a SharpCompress y Snappier para sobreescribir transitivos.

Los primeros intentos (`SharpCompress 0.39.0`, `Snappier 1.1.1`) seguían marcados como vulnerables. Justo antes de detener el trabajo, los `.csproj` se editaron a:

- `SharpCompress 0.49.0`
- `Snappier 1.3.0`
- `MailKit 4.15.0`

**Resuelto el 2026-10-08.** `Snappier 1.3.0` seguía vulnerable y se subió a `1.3.1`; `MailKit` se subió a `4.16.0` y se agregaron referencias directas a `System.Net.Http 4.3.4` y `System.Text.RegularExpressions 4.3.1` en los proyectos de prueba. Restore, build, pruebas (27) y auditoría completados sin vulnerabilidades.

### Comandos prioritarios para continuar

Ejecutar primero:

```bash
dotnet restore Front.Web.sln
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
dotnet list Front.Web.sln package --vulnerable --include-transitive
```

Después:

```bash
git diff --check
git status --short
```

Buscar nuevamente dependencias SQL y secretos:

```text
Patrones SQL: SqlClient | UseSqlServer | EntityFrameworkCore.SqlServer | ConnectionStrings
Patrones de secretos JSON: ApiSecret, SigningKey y ConnectionString con valor no vacío
```

No usar `grep` desde shell en esta sesión de agente; usar la herramienta de búsqueda disponible.

### Configuración local requerida

Todos los servicios que validan JWT deben compartir exactamente:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey` de al menos 32 caracteres

Crear archivos `appsettings.Local.json` ignorados por Git.

#### Administration.Api

Requiere:

- `MongoDb:ConnectionString`
- `MongoDb:DatabaseName`
- JWT compartido
- SMTP opcional pero necesario para recuperación funcional
- `Email:ResetBaseUrl` apuntando a `Admin.Web`, por ejemplo `http://localhost:5163`

#### Catalog.Api

Requiere:

- MongoDB
- JWT compartido
- `Cloudinary:CloudName`
- `Cloudinary:ApiKey`
- `Cloudinary:ApiSecret`

#### Customer.Api y Order.Api

Requieren MongoDB y el mismo JWT. Customer.Api también requiere SMTP para recuperación de clientes.

#### Puertos documentados

- Front.Web: `5160`
- Customer.Api: `5161`
- Order.Api: `5162`
- Admin.Web: `5163`
- Administration.Api: `5164`
- Catalog.Api: `5165`
- Cart.Api: `5166`
- Location.Api: `5167`

### Validación integrada

**Resultados del 2026-10-14** (los ocho servicios corriendo con `appsettings.Local.json`):

- Health checks OK en los seis APIs, incluidos Cart (5166) y Location (5167).
- `Integration.Seed` completó: registro → login JWT → consulta/alta/cambio de carrito vía Cart.Api → checkout con descuento de stock → historial → carrito vacío → limpieza. `=== OK ===`.
- `GET /api/cart` sin token: `401`; con token falso: `401`.
- `/api/location/municipalities/$ne`: `400`.
- `Front.Web` sin `MongoDB.Driver`: `/Store` `200`, `/api/location/states` proxied OK, `/Cart` `302` a login.
- Suite: 75 pruebas aprobadas; auditoría de paquetes sin vulnerabilidades.

**Resultados del 2026-10-13** (los seis servicios corriendo con `appsettings.Local.json`):

- Health checks OK en Customer (5161), Order (5162), Administration (5164) y Catalog (5165).
- `Integration.Seed` completó el flujo completo: registro → login JWT → checkout con descuento de stock → historial → limpieza. `=== OK ===`.
- Sin token: `401` en `/api/admin/orders`, `/api/admin/catalog/products` y `/api/admin/customers/count`.
- Con JWT de cliente (rol `Customer`): `403` en los cuatro endpoints administrativos probados.
- Token falso: `401`.
- Front.Web: `/Store`, `/Store/Index`, `/Account/Login`, `/Account/Register` responden `200`.
- Suite integrada del 2026-10-13: 42 pruebas unitarias aprobadas (17 Front.Web + 6 Administration + 4 Catalog + 8 Order + 7 Admin.Web).
- Suite local del 2026-10-20: 75 pruebas aprobadas (17 Front.Web + 8 Administration + 9 Catalog + 14 Order + 11 Admin.Web + 7 Cart + 9 Location).
- Auditoría `dotnet list package --vulnerable --include-transitive`: sin paquetes vulnerables en la última ejecución documentada.

**Validaciones manuales adicionales completadas el 2026-10-13:**

- SMTP real de Gmail funciona con STARTTLS en `smtp.gmail.com:587`; la contraseña de aplicación se configura sin espacios.
- Recuperación administrativa envía correo, redirige a `Account/Reset` y permite introducir manualmente el token recibido.
- El correo contiene enlace y token; el token expira en una hora, es de un solo uso y se elimina tras cambiar la contraseña.
- El mínimo de la nueva contraseña administrativa se ajustó a 5 caracteres tanto en API como en MVC por decisión del proyecto.
- Cambio de contraseña devolvió `204`; login con la contraseña anterior devolvió `401` y con la nueva devolvió `200`.
- Tras el login, dashboard, conteo de clientes, catálogo y pedidos administrativos devolvieron `200`.

**Pendiente de validación manual:**

1. ~~Crear un administrador y comprobar que SMTP acepta el correo de establecimiento~~ **Completado el 2026-10-20** mediante `Integration.Seed`; resta confirmar visualmente la recepción en la bandeja.
2. Crear categoría y marca.
3. Crear y editar producto sin imagen; comprobar su aparición en la tienda a través de Catalog.Api.
4. ~~Cuando exista una cuenta Cloudinary, probar alta/reemplazo real y compensación~~ **Completado el 2026-10-28**: credenciales reales en `Catalog.Api/appsettings.Local.json`, subida JPG validada en vivo y renderizado en `Front.Web` (requirió `img-src ... res.cloudinary.com` en la CSP). SVG, formato inválido y archivo mayor de 5 MB ya tienen validación local automatizada.
5. ~~Ejecutar dos checkouts simultáneos y confirmar una sola orden y un solo descuento de stock~~ **Completado el 2026-10-20** mediante la sección 9 de `Integration.Seed`: dos `POST /api/orders` paralelos devolvieron `200` y `400`, con una sola orden en `ventas` y un solo descuento de stock.
6. ~~Validar rollback ante un fallo parcial de checkout~~ **Completado el 2026-10-20**: un carrito multiartículo cuyo segundo producto no tenía stock devolvió `400`, no creó orden, no descontó stock y conservó el carrito.
7. ~~Ejecutar regresión completa de carrito, checkout e historial después de futuras extracciones~~ **Completado el 2026-10-14** mediante `Integration.Seed` extendido con Cart.Api.

### Problemas y limitaciones conocidas

1. ~~**Dependencias pendientes de revalidación**~~ **Resuelto**
   - Restore, build y auditoría de vulnerabilidades completados correctamente; la suite local más reciente ejecutó 75 pruebas.

2. **E2E parcialmente pendiente**
   - MongoDB, SMTP, recuperación, cambio de contraseña, login, dashboard, catálogo, pedidos, reportes y regresión básica están validados.
   - Checkout concurrente, rollback parcial y aceptación SMTP del correo de establecimiento se validaron el 2026-10-20. Cloudinary real se validó el 2026-10-28 con credenciales configuradas y subida JPG en vivo.

3. ~~**Conflicto de nombres de índices MongoDB**~~ **Resuelto y validado el 2026-10-14**
   - Los inicializadores de Administration y Catalog enumeran los índices y reutilizan cualquier índice único equivalente por clave y opciones, independientemente del nombre.
   - Ambos servicios arrancaron contra MongoDB real, reconocieron todos los índices existentes y no registraron el código 85.
   - Se agregaron cuatro pruebas unitarias para equivalencia, unicidad, índices compuestos y dirección.

4. **Cuenta administrativa inicial**
   - Se asume que existe al menos un documento activo en `usuarios` con credenciales conocidas. No se creó un bootstrap automático para evitar introducir contraseñas predeterminadas.

5. **Recuperación SMTP**
   - Validada con Gmail mediante STARTTLS en el puerto 587. Si SMTP no está configurado, `Administration.Api` no envía correo; la respuesta sigue siendo uniforme para no enumerar cuentas.
   - `Email:ResetBaseUrl` debe ser `http://localhost:5163` para uso local o la URL HTTPS pública de ngrok que apunte a Admin.Web. La comunicación interna de Admin.Web con Administration.Api puede continuar en `http://localhost:5164`.

6. **Imágenes heredadas**
   - Las rutas físicas Windows antiguas no se migran: desde el 2026-11-03 se eliminan. `tests/MongoCleanup` borra `rutaImagen`/`nombreImagen` de `productos` y el dump `mongo/productos.json` ya no los incluye. Los productos sin `imageUrl` se muestran con placeholder hasta cargar una imagen en Cloudinary.

7. **Pedidos**
   - ~~Son deliberadamente de sólo lectura~~ **Actualizado el 2026-10-28**: `Order.Api` expone `PUT /api/admin/orders/{id}/status` con estados `confirmado`, `en_proceso`, `enviado`, `entregado`, `cancelado`, paquetería, número de guía y notas (persistidos en `ventas.seguimiento.historial`). `Admin.Web/Orders/Details` incluye el formulario. `OrderEmailSender` (MailKit en `Order.Api`) notifica al cliente por correo o degrada a log sin SMTP.

8. **Filtros de Admin.Web**
   - La API admite filtros tipados, pero la primera interfaz de pedidos muestra principalmente paginación básica. El reporte sí admite fechas y transacción.

9. ~~Detalle de pedido en UI~~ **Resuelto el 2026-10-08**: se agregó `Orders/Details` en `Admin.Web` (acción, vista, `AdminApiClient.OrderDetail`, `OrderDetailModel`) con enlace desde el listado.

10. **Pruebas faltantes respecto al plan completo**
   - ~~No se crearon proyectos separados para Order.Api.UnitTests y Admin.Web.UnitTests.~~ **Resuelto el 2026-10-13**: se crearon ambos proyectos y se añadieron a la solución (8 y 7 pruebas respectivamente: validación de checkout, claims de cliente, modelos de Admin.Web).
   - **Actualizado el 2026-10-14**: se agregaron pruebas de autorización de sesión administrativa (filtro `AdminSessionRequirement`, cobertura de todos los controladores seguros) y de paginación de `Order.Api` (límites inferior y superior, tope de 100). La autorización HTTP 401/403 además se validó de forma integrada contra servicios reales.
   - **Actualizado el 2026-11-03**: `tests/Admin.Web.UnitTests/AntiforgeryTests.cs` cubre el antiforgery de extremo a extremo con `WebApplicationFactory<Program>` (POST sin token `400`, POST con token `302`). Sigue faltando la prueba de compensación Cloudinary ante fallo del producto (la subida real ya se validó manualmente).

11. **Auditoría**
    - Es funcional pero básica. No guarda diff detallado; sólo actor, acción, entidad, ID y fecha.

12. **Rate limiting distribuido**
    - Es en memoria. Para múltiples instancias debería migrarse a un almacén distribuido o gateway.

13. **Sesiones distribuidas**
    - Admin.Web usa memoria. Para despliegue horizontal requiere Redis u otro `IDistributedCache` compartido.

14. **Eliminación física**
    - No se implementó, deliberadamente. Se utiliza activación/desactivación para conservar referencias históricas.

15. **Documento ajeno sin seguimiento**
    - `DOCUMENTACION_TECNICA_ECOMMERCE_PADUKI.docx` ya estaba sin seguimiento antes de esta implementación. No fue modificado ni debe incluirse accidentalmente.

### Siguiente incremento recomendado

1. Usar las secciones principales de este documento como estado autoritativo y este anexo como detalle administrativo; no repetir los E2E ya completados.
2. Confirmar visualmente la recepción del correo de establecimiento; la creación y aceptación SMTP ya se validaron.
3. ~~Crear categoría, marca y producto desde el panel y validar su aparición en la tienda~~ **Completado el 2026-10-28** con subida de imagen JPG a Cloudinary y visualización en `Front.Web`; se corrigió además el defecto de productos heredados sin campo `version` que provocaba `409` (ver la entrada de historial del 2026-10-28).
4. ~~Añadir pruebas de antiforgery de extremo a extremo con `WebApplicationFactory`~~ **Completado el 2026-11-03** (`tests/Admin.Web.UnitTests/AntiforgeryTests.cs`).
5. ~~Cuando existan credenciales, completar las pruebas de Cloudinary y su compensación~~ **Completado el 2026-10-28** (subida real validada; la compensación ante fallo del producto sigue sin prueba automatizada).
6. ~~Ejecutar E2E concurrente y rollback parcial de checkout~~ **Completado el 2026-10-20** (incluyó reintento ante `TransientTransactionError`, una sola orden/descuento bajo concurrencia y rollback total ante segundo producto sin stock).
7. ~~Extraer `Cart.Api` y `Location.Api` para retirar `MongoDB.Driver` de `Front.Web`~~ **Completado el 2026-10-14**.
8. Formatear las vistas/controladores administrativos restantes que todavía contienen líneas excesivamente largas.
9. No incluir `appsettings.Local.json`, secretos ni `DOCUMENTACION_TECNICA_ECOMMERCE_PADUKI.docx` en un commit.
