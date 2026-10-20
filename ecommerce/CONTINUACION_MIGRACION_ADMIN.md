# Continuación de la migración administrativa

## Objetivo

Migrar el administrador legado a ASP.NET Core 8 usando exclusivamente MongoDB, sin SQL Server, con una aplicación `Admin.Web` separada, APIs por dominio, JWT con rol administrativo, Cloudinary y controles de ciberseguridad.

## Estado general

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

## Trabajo implementado

### 1. Administration.Api

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

### 2. Catalog.Api

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

### 3. Admin.Web

Ruta: `src/Services/Front/Admin.Web`

Incluye:

- Aplicación MVC separada de la tienda.
- Diseño inspirado en `src/Services/Front/admin.html`.
- Login, recuperación, reset y logout POST.
- Dashboard con clientes, ventas, ingresos, productos y stock bajo.
- Pantallas para productos, categorías, marcas y administradores.
- Formularios de alta y edición.
- Carga de imágenes de productos.
- Pedidos de sólo lectura.
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

### 4. Customer.Api

Cambios:

- Se agregó validación JWT Bearer.
- Se agregó política `admin`.
- Se agregó `GET /api/admin/customers/count`.
- Se agregó `CustomerStore.CountAsync`.
- Se corrigieron atributos de validación de records usando `property:` para que `Validator.TryValidateObject` realmente valide sus propiedades.

### 5. Order.Api

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

### 6. Front.Web

Cambios:

- Se creó `Services/CatalogApiClient.cs`.
- `StoreController` ahora consume `Catalog.Api`.
- Se retiró el registro de `MongoCatalogService` del contenedor.
- `Front.Web` todavía conserva MongoDB para carrito y ubicaciones, tal como estaba previsto.
- `appsettings.json` ahora contiene URLs vigentes de Catalog, Customer y Order.

### 7. Solución y pruebas

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

## Verificaciones completadas

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

## Estado de dependencias y trabajo interrumpido

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

## Comandos prioritarios para continuar

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

## Configuración local requerida

Todos los servicios que validan JWT deben compartir exactamente:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey` de al menos 32 caracteres

Crear archivos `appsettings.Local.json` ignorados por Git.

### Administration.Api

Requiere:

- `MongoDb:ConnectionString`
- `MongoDb:DatabaseName`
- JWT compartido
- SMTP opcional pero necesario para recuperación funcional
- `Email:ResetBaseUrl` apuntando a `Admin.Web`, por ejemplo `http://localhost:5163`

### Catalog.Api

Requiere:

- MongoDB
- JWT compartido
- `Cloudinary:CloudName`
- `Cloudinary:ApiKey`
- `Cloudinary:ApiSecret`

### Customer.Api y Order.Api

Requieren MongoDB y el mismo JWT. Customer.Api también requiere SMTP para recuperación de clientes.

### Puertos documentados

- Front.Web: `5160`
- Customer.Api: `5161`
- Order.Api: `5162`
- Admin.Web: `5163`
- Administration.Api: `5164`
- Catalog.Api: `5165`
- Cart.Api: `5166`
- Location.Api: `5167`

## Validación integrada

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
4. Cuando exista una cuenta Cloudinary, probar alta/reemplazo real y compensación; SVG, formato inválido y archivo mayor de 5 MB ya tienen validación local automatizada.
5. ~~Ejecutar dos checkouts simultáneos y confirmar una sola orden y un solo descuento de stock~~ **Completado el 2026-10-20** mediante la sección 9 de `Integration.Seed`: dos `POST /api/orders` paralelos devolvieron `200` y `400`, con una sola orden en `ventas` y un solo descuento de stock.
6. ~~Validar rollback ante un fallo parcial de checkout~~ **Completado el 2026-10-20**: un carrito multiartículo cuyo segundo producto no tenía stock devolvió `400`, no creó orden, no descontó stock y conservó el carrito.
7. ~~Ejecutar regresión completa de carrito, checkout e historial después de futuras extracciones~~ **Completado el 2026-10-14** mediante `Integration.Seed` extendido con Cart.Api.

## Problemas y limitaciones conocidas

1. ~~**Dependencias pendientes de revalidación**~~ **Resuelto**
   - Restore, build y auditoría de vulnerabilidades completados correctamente; la suite local más reciente ejecutó 75 pruebas.

2. **E2E parcialmente pendiente**
   - MongoDB, SMTP, recuperación, cambio de contraseña, login, dashboard, catálogo, pedidos, reportes y regresión básica están validados.
   - Checkout concurrente, rollback parcial y aceptación SMTP del correo de establecimiento se validaron el 2026-10-20. Cloudinary real sigue pendiente porque no existe cuenta ni credenciales.

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
   - Las rutas físicas Windows antiguas no se migran automáticamente. Los productos sin `imageUrl` se muestran sin imagen hasta cargar una nueva en Cloudinary.

7. **Pedidos**
   - Son deliberadamente de sólo lectura; no se implementaron cambios de estado, paquetería ni guía.

8. **Filtros de Admin.Web**
   - La API admite filtros tipados, pero la primera interfaz de pedidos muestra principalmente paginación básica. El reporte sí admite fechas y transacción.

9. ~~Detalle de pedido en UI~~ **Resuelto el 2026-10-08**: se agregó `Orders/Details` en `Admin.Web` (acción, vista, `AdminApiClient.OrderDetail`, `OrderDetailModel`) con enlace desde el listado.

10. **Pruebas faltantes respecto al plan completo**
   - ~~No se crearon proyectos separados para Order.Api.UnitTests y Admin.Web.UnitTests.~~ **Resuelto el 2026-10-13**: se crearon ambos proyectos y se añadieron a la solución (8 y 7 pruebas respectivamente: validación de checkout, claims de cliente, modelos de Admin.Web).
   - **Actualizado el 2026-10-14**: se agregaron pruebas de autorización de sesión administrativa (filtro `AdminSessionRequirement`, cobertura de todos los controladores seguros) y de paginación de `Order.Api` (límites inferior y superior, tope de 100). La autorización HTTP 401/403 además se validó de forma integrada contra servicios reales.
   - Siguen faltando pruebas de antiforgery de extremo a extremo con `WebApplicationFactory`, carga de archivos y compensación Cloudinary (bloqueada hasta tener credenciales).

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

## Archivos de documentación actualizados

- `README.md`
- `MIGRACION.md`
- Este documento de continuación.

## Git

No se creó commit ni se hizo push.

Revisar cuidadosamente `git status` y `git diff` antes de confirmar. No incluir secretos, `appsettings.Local.json`, datos personales ni el DOCX ajeno sin decisión explícita.

## Siguiente incremento recomendado

1. Leer `MIGRACION.md` como fuente autoritativa y este archivo como detalle administrativo; no repetir los E2E ya completados.
2. Confirmar visualmente la recepción del correo de establecimiento; la creación y aceptación SMTP ya se validaron.
3. Crear categoría, marca y producto desde el panel y validar su aparición en la tienda.
4. Añadir pruebas de antiforgery de extremo a extremo con `WebApplicationFactory` (autorización y paginación ya tienen cobertura unitaria).
5. Cuando existan credenciales, completar las pruebas de Cloudinary y su compensación.
6. ~~Ejecutar E2E concurrente y rollback parcial de checkout~~ **Completado el 2026-10-20** (incluyó reintento ante `TransientTransactionError`, una sola orden/descuento bajo concurrencia y rollback total ante segundo producto sin stock).
7. ~~Extraer `Cart.Api` y `Location.Api` para retirar `MongoDB.Driver` de `Front.Web`~~ **Completado el 2026-10-14**.
8. Formatear las vistas/controladores administrativos restantes que todavía contienen líneas excesivamente largas.
9. No incluir `appsettings.Local.json`, secretos ni `DOCUMENTACION_TECNICA_ECOMMERCE_PADUKI.docx` en un commit.
