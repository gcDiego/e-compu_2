# Ecommerce Paduki

Ecommerce en migración incremental desde ASP.NET MVC 5 y SQL Server hacia ASP.NET Core 8, MongoDB y APIs separadas por dominio.

La fuente autoritativa del estado, historial, riesgos y roadmap es [`MIGRACION.md`](MIGRACION.md). El detalle del incremento administrativo está en [`CONTINUACION_MIGRACION_ADMIN.md`](CONTINUACION_MIGRACION_ADMIN.md).

## Estado actual

El código activo ya no depende de SQL Server. Los identificadores `idSqlOriginal` se conservan sólo por compatibilidad con los datos migrados.

La arquitectura vigente es:

```text
Navegador
  ├── Front.Web
  │     ├── Catalog.Api (HTTP)
  │     ├── Customer.Api (HTTP/JWT)
  │     ├── Cart.Api (HTTP/JWT)
  │     ├── Location.Api (HTTP)
  │     └── Order.Api (HTTP/JWT)
  └── Admin.Web
        ├── Administration.Api (HTTP/JWT admin)
        ├── Catalog.Api (HTTP/JWT admin)
        ├── Customer.Api (métricas administrativas)
        └── Order.Api (pedidos, dashboard y reportes)
```

`Front.Web` ya no accede directamente a MongoDB: no referencia `MongoDB.Driver` ni contiene lógica de base de datos. La única excepción transitoria de propiedad es la colección `carritos`, propiedad de `Cart.Api` y leída/eliminada por `Order.Api` dentro de la transacción de checkout.

## Componentes activos

- `src/Services/Front/Front.Web`: tienda ASP.NET Core 8.
- `src/Services/Front/Admin.Web`: panel administrativo ASP.NET Core 8.
- `src/Services/Administration/Administration.Api`: autenticación y gestión administrativa.
- `src/Services/Catalog/Catalog.Api`: catálogo público y administrativo, stock e imágenes Cloudinary.
- `src/Services/Customer/Customer.Api`: registro, autenticación y recuperación de clientes.
- `src/Services/Cart/Cart.Api`: carrito persistente por cliente autenticado con JWT.
- `src/Services/Location/Location.Api`: estados, municipios y localidades de lectura pública.
- `src/Services/Order/Order.Api`: checkout, historial, pedidos, dashboard y reportes.

## Funcionalidad implementada

- Registro, login y recuperación de contraseña de clientes.
- Catálogo público mediante `Catalog.Api`.
- Carrito persistente mediante `Cart.Api` y ubicaciones dependientes mediante `Location.Api`.
- Checkout autenticado, transaccional e idempotente mediante `Order.Api`.
- Historial de compras.
- Login, recuperación y gestión de administradores.
- Gestión de categorías, marcas, productos, stock e imágenes.
- Pedidos administrativos de sólo lectura, dashboard, reportes y exportación XLSX.
- JWT por rol, rate limiting, bloqueo temporal, antiforgery y cabeceras de seguridad.
- PBKDF2-SHA256 y migración progresiva de hashes SHA-256 heredados.

## Configuración local

Requisitos:

- .NET SDK 8.
- Acceso autorizado a MongoDB.
- Un mismo `Jwt:Issuer`, `Jwt:Audience` y `Jwt:SigningKey` en todos los servicios.
- SMTP para recuperación de clientes y administradores.
- Cloudinary para cargar imágenes; sin credenciales, la carga responde `503` de forma controlada.

Los secretos se configuran mediante `appsettings.Local.json` ignorados por Git, user-secrets o variables de entorno. Nunca deben incluirse conexiones, contraseñas, tokens ni datos personales en el repositorio.

Puertos locales:

- Front.Web: `5160`
- Customer.Api: `5161`
- Order.Api: `5162`
- Admin.Web: `5163`
- Administration.Api: `5164`
- Catalog.Api: `5165`
- Cart.Api: `5166`
- Location.Api: `5167`

## Compilar y ejecutar

```bash
dotnet restore Front.Web.sln
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
./start.sh
```

`start.sh` comprueba los puertos, levanta los ocho servicios y guarda PID y logs en `.logs/`. Para detener únicamente esos procesos:

```bash
./stop.sh
```

Abrir `http://localhost:5160` para la tienda y `http://localhost:5163` para administración.

## Validación mínima

```bash
dotnet list Front.Web.sln package --vulnerable --include-transitive
git diff --check
```

Con los servicios y la infraestructura local disponibles, validar también:

- Health checks de los seis APIs.
- Registro o login, catálogo, carrito, checkout e historial.
- Autorización administrativa `401`, `403` y `200` según token y rol.
- ~~Creación de administrador y envío SMTP del correo de establecimiento~~ **Validado el 2026-10-20**; resta confirmar visualmente su recepción.
- Alta y edición de categoría, marca y producto.
- Carga y reemplazo de imagen en Cloudinary.
- ~~Checkout concurrente y rollback ante fallo parcial~~ **Validados el 2026-10-20**: dos requests paralelos generan una sola orden y un solo descuento; un carrito multiartículo con el segundo producto sin stock no crea orden, restaura el stock y conserva el carrito.

## Pendientes prioritarios

1. Confirmar visualmente la recepción del correo de establecimiento enviado y aceptado por SMTP.
2. Completar las pruebas manuales de catálogo administrativo; Cloudinary real queda pendiente hasta crear una cuenta y configurar credenciales.
3. Automatizar antiforgery de extremo a extremo y carga de archivos (autorización de sesión administrativa, paginación y validación local de imágenes ya tienen cobertura).
4. Mantener el E2E de concurrencia y rollback de `Integration.Seed` como regresión.
5. Implementar pagos, webhooks e idempotencia/conciliación de PayPal.
6. Completar observabilidad, CI/CD, backups y preparación para despliegue horizontal.

## Seguridad

- No versionar conexiones, contraseñas, JWT, secretos de PayPal, SMTP o Cloudinary.
- No copiar datos personales ni tokens a documentación o logs.
- No publicar respaldos o exportaciones de bases reales.
- No confiar en identificadores de cliente, precios o totales enviados por el navegador.
- Rotar cualquier secreto expuesto.
