# Handoff técnico de la migración a microservicios

## Instrucción para la siguiente conversación

Leer este documento primero y usarlo como resumen autoritativo del estado actual. Consultar `REGISTRO_CAMBIOS_MICROSERVICIOS.md` para el historial y `README.md` para configuración local.

No asumir que los cambios del working tree están confirmados en Git. No revertir archivos ajenos ni cambios dentro de `packages/` sin autorización.

## Objetivo vigente

Completar la validación reversible de las mutaciones de Cart Service y después endurecer el checkout para que precios, cantidades y totales se reconstruyan en servidor.

La base SQL Server seguirá compartida temporalmente. No crear, alterar, eliminar ni migrar tablas, columnas, funciones, procedimientos o datos salvo autorización explícita.

## Restricciones obligatorias

- Mantener el esquema actual de la base `ecommerce`.
- Mantener las aplicaciones MVC heredadas operativas durante la transición.
- Conservar feature flags y caminos legados para rollback.
- No eliminar todavía referencias MVC a `CapaNegocio` o `CapaEntidad`.
- No versionar cadenas de conexión, contraseñas, JWT, Client ID, secretos ni datos personales.
- No copiar rutas físicas de imágenes a contratos públicos.
- No migrar todavía recuperación de contraseña; fue pospuesta porque necesita almacenamiento seguro aprobado.
- No migrar todavía administración de productos ni pagos completos.
- No aceptar identificadores de cliente, precios o totales confiando en datos enviados por navegador.

## Arquitectura vigente

```text
Tienda MVC (.NET Framework 4.7.2 / Mono)
  ├── Identity Service (.NET 8) para login, mediante feature flag
  ├── Catalog Service (.NET 8) para lecturas, mediante feature flag
  ├── Cart Service (.NET 8) para carrito, mediante feature flag
  └── CapaNegocio/CapaDatos como rollback y funciones aún no migradas

Identity emite JWT
  ├── Catalog valida JWT localmente
  └── Cart valida JWT localmente y exige rol Customer

Identity, Catalog, Cart y legado
  └── SQL Server compartido / base ecommerce
```

No hay llamadas HTTP directas entre los microservicios. Identity emite el JWT y los demás servicios validan firma, emisor, audiencia y vigencia localmente. Los tres deben usar la misma clave, emisor y audiencia.

## Componentes implementados

### Catalog Service

Proyectos:

- `src/Services/Catalog/Catalog.Domain`
- `src/Services/Catalog/Catalog.Application`
- `src/Services/Catalog/Catalog.Infrastructure`
- `src/Services/Catalog/Catalog.Api`

Estado:

- Validado contra SQL Server real.
- Lecturas públicas de categorías, marcas, productos y detalle.
- Health check SQL.
- OpenAPI en Development.
- Validación JWT y política `AdministratorOnly` preparadas para operaciones futuras.
- La API no devuelve rutas físicas de imágenes.

Contratos:

- `GET /api/v1/categories`
- `GET /api/v1/brands`
- `GET /api/v1/products`
- `GET /api/v1/products/{id}`
- `GET /health`

Integración MVC:

- `CapaPresentacionTienda/CatalogApiClient.cs`.
- `TiendaController` alterna mediante `Features:UseCatalogApi`.
- Las imágenes se enriquecen temporalmente desde el legado.

### Identity Service

Proyectos:

- `src/Services/Identity/Identity.Domain`
- `src/Services/Identity/Identity.Application`
- `src/Services/Identity/Identity.Infrastructure`
- `src/Services/Identity/Identity.Api`

Estado:

- Login de clientes y administradores validado contra SQL Server real.
- JWT firmado con identidad, correo, rol y `must_reset_password`.
- El claim de rol debe emitirse como `role`; los consumidores usan `MapInboundClaims=false`.
- SHA-256 heredado se verifica sólo para compatibilidad.
- Un login heredado válido migra el hash de forma optimista a PBKDF2 de ASP.NET Core Identity.
- El rehash fue validado con una cuenta autorizada real.
- Cambio autenticado de contraseña implementado.
- Recuperación de contraseña pospuesta.

Contratos:

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/change-password`
- `GET /health`

Integración MVC:

- `CapaPresentacionTienda/IdentityApiClient.cs`.
- `AccesoController.Index` usa Identity con `Features:UseIdentityApi`.
- El JWT se guarda sólo en `Session["IdentityAccessToken"]`.
- Forms Authentication sigue activa para la navegación MVC.
- El JWT y su expiración se eliminan al cerrar sesión.
- En Mono/XSP no usar `FormsAuthentication.SetAuthCookie` después de `await`; se usa `FormsAuthentication.GetAuthCookie` y `Response.Cookies.Add`.

Riesgo de rollback:

- Una cuenta migrada a PBKDF2 ya no puede autenticarse mediante el login legado que sólo compara SHA-256. Para esas cuentas Identity Service es dependencia obligatoria hasta modernizar o retirar el login legado.

### Cart Service

Proyectos:

- `src/Services/Cart/Cart.Domain`
- `src/Services/Cart/Cart.Application`
- `src/Services/Cart/Cart.Infrastructure`
- `src/Services/Cart/Cart.Api`
- `tests/Cart.UnitTests`
- `tests/Cart.IntegrationTests`

Estado:

- Primer corte implementado y agregado a `Ecommerce.Services.sln`.
- Health check validado contra SQL Server real.
- Todos los endpoints requieren JWT con rol `Customer`.
- El identificador del cliente se obtiene exclusivamente de `sub`.
- Los contratos no reciben CustomerId, precio, nombre o total.
- Precios y cantidades se cargan desde SQL y el total se calcula en servidor.
- Lecturas MVC reales validadas mediante Mono/XSP.
- Mutaciones reales todavía no fueron probadas para evitar dejar datos alterados.

Objetos SQL heredados reutilizados sin modificarlos:

- Tabla `CARRITO`: `IdCarrito`, `IdCliente`, `IdProducto`, `Cantidad`.
- `fn_obtenerCarritoCliente`.
- `sp_ExisteCarrito`.
- `sp_OperacionCarrito`.
- `sp_EliminarCarrito`.
- FKs de `CARRITO` hacia `CLIENTE` y `PRODUCTO`.

Contratos:

- `GET /api/v1/cart`
- `POST /api/v1/cart/items/{productId}`
- `PATCH /api/v1/cart/items/{productId}` con `{ "increase": true|false }`
- `DELETE /api/v1/cart/items/{productId}`
- `GET /health`

Integración MVC:

- `CapaPresentacionTienda/CartApiClient.cs`.
- `TiendaController` alterna mediante `Features:UseCartApi`.
- JWT leído desde sesión del servidor y enviado como bearer token.
- Se preservan los contratos JSON heredados: `{ respuesta, mensaje }`, `{ cantidad }` y `{ data }`.
- Imágenes del listado se enriquecen temporalmente desde `CN_Producto`.
- `CN_Carrito` permanece como rollback cuando el flag está desactivado.

## Feature flags y URLs locales

Configuración actual de Tienda en `CapaPresentacionTienda/Web.config`:

- `Features:UseCatalogApi`
- `CatalogApi:BaseUrl` — `http://localhost:5137/`
- `Features:UseIdentityApi`
- `IdentityApi:BaseUrl` — `http://localhost:5204/`
- `Features:UseCartApi`
- `CartApi:BaseUrl` — `http://localhost:5292/`

Los flags están activos en el entorno local actual. Antes de publicar, mover URLs y secretos a transformaciones o configuración externa.

Configuración requerida para servicios:

- `ConnectionStrings__CatalogDatabase`
- `ConnectionStrings__IdentityDatabase`
- `ConnectionStrings__CartDatabase`
- `Jwt__SigningKey`, igual en Identity, Catalog y Cart.
- Opcionales, pero iguales en los tres: `Jwt__Issuer` y `Jwt__Audience`.

Valores predeterminados de JWT:

- Issuer: `Ecommerce.Identity`.
- Audience: `Ecommerce.Services`.
- Duración del token: 30 minutos.
- Clock skew de consumidores: 1 minuto.

No agregar valores reales a este documento.

## Base de datos

Entorno validado:

- SQL Server 2022 en Docker.
- Contenedor local usado: `mi_sql_server_2022`.
- Puerto: `localhost:1433`.
- Base: `ecommerce`.

Decisión vigente:

- La base y el esquema no se mueven en esta fase.
- Cada microservicio limita su acceso a sus tablas y objetos de dominio.
- Catalog consulta catálogo.
- Identity consulta y actualiza credenciales de `CLIENTE` y `USUARIO`.
- Cart consulta y modifica `CARRITO` mediante objetos SQL heredados.

## Ejecución del legado en macOS

La ejecución fue validada y está documentada en `EJECUCION_MONO_MACOS.md`.

Herramientas:

- Mono 6.12.
- MSBuild para Mono 16.10.
- XSP4.

Comandos desde la carpeta `ecommerce`:

```bash
msbuild ecommerce.sln /t:Build /p:Configuration=Debug /verbosity:minimal
```

Tienda:

```bash
xsp4 --port 8081 --nonstop
```

Administración:

```bash
xsp4 --port 8080 --nonstop
```

Existe una advertencia conocida al resolver `System.Web.Entity`, pero no bloquea compilación ni arranque.

## Evidencia vigente

Automatización:

```bash
dotnet test Ecommerce.Services.sln --configuration Release --no-restore
```

Resultado más reciente:

- 30 pruebas aprobadas.
- Catalog: 2 unitarias y 6 HTTP.
- Identity: 13 unitarias y 4 HTTP.
- Cart: 2 unitarias y 3 HTTP.

Compilación legado:

```bash
msbuild ecommerce.sln /t:Build /p:Configuration=Debug /verbosity:minimal
```

Resultado:

- `CapaEntidad`, `CapaDatos`, `CapaNegocio`, Admin y Tienda compilan.

Validación integrada real, sin mutaciones de carrito:

- Login MVC mediante Identity: `302`.
- Página Tienda: `200`.
- Cantidad de carrito mediante Cart: JSON correcto.
- Listado de carrito mediante Cart: `200`.
- Cart `/health`: `200 Healthy`.
- Cart sin JWT: `401 Unauthorized`.

No se documentaron credenciales, tokens ni hashes.

## Working tree al crear este handoff

Hay cambios sin commit y archivos nuevos. No ejecutar `git reset`, `git clean` ni restauraciones globales.

Cambios principales pendientes:

- Clientes API y controladores MVC de Tienda.
- Feature flags y URLs en `Web.config`.
- Claim `role` de Identity.
- Todo Cart Service y sus pruebas.
- `Ecommerce.Services.sln`.
- `README.md` ya reconoce la ejecución mediante Mono/XSP; mantenerlo sincronizado con `EJECUCION_MONO_MACOS.md` cuando cambien comandos o puertos.
- `REGISTRO_CAMBIOS_MICROSERVICIOS.md`.
- `EJECUCION_MONO_MACOS.md` todavía aparece como archivo nuevo.

Antes de hacer commit, revisar especialmente que `Web.config` contiene configuración local y credenciales heredadas preexistentes. No publicar esos secretos; deben rotarse y externalizarse en un incremento de seguridad.

## Hallazgos y riesgos prioritarios

1. `ProcesarPago` todavía recibe productos, precios y cantidades construidos por JavaScript. Debe reconstruir el carrito y los precios desde servicios o SQL antes de crear una orden PayPal.
2. Las cuentas PBKDF2 ya dependen de Identity Service para iniciar sesión.
3. No existe renovación silenciosa del JWT guardado en sesión. Al expirar, Cart indica que debe iniciarse sesión nuevamente.
4. Las imágenes todavía dependen del almacenamiento físico legado.
5. PayPal y SMTP mantienen deuda de secretos en el legado. No copiarlos a servicios nuevos.
6. Los errores de Cart API se propagan; no implementar fallback silencioso cuando el flag esté activo.
7. Las mutaciones reales de Cart deben probarse con snapshot previo y restauración final.
8. La guía principal aún puede contener texto antiguo que dice que MVC requiere Windows; `EJECUCION_MONO_MACOS.md` es la evidencia actual de que este proyecto compila y corre en macOS mediante Mono/XSP.

## Siguiente incremento exacto

### Parte 1: validación reversible de Cart

1. Elegir una cuenta y un producto de prueba autorizados.
2. Consultar y conservar en memoria el estado inicial del carrito; no escribir hashes, credenciales ni datos personales en logs.
3. Obtener JWT mediante Identity.
4. Probar agregar un producto ausente.
5. Repetir agregado y verificar conflicto por duplicado.
6. Incrementar cantidad.
7. Decrementar cantidad.
8. Eliminar el producto.
9. Restaurar el estado inicial si no estaba vacío o si una operación falla.
10. Confirmar aislamiento: un JWT de otro cliente no debe ver ni modificar ese carrito.
11. Documentar sólo códigos HTTP, conteos y resultado; nunca token ni credenciales.

### Parte 2: endurecimiento de checkout

1. Modificar `ProcesarPago` para no confiar en `oListarCarrito` ni precios enviados por JavaScript.
2. Obtener identidad desde sesión/JWT.
3. Reconstruir productos, cantidades y precios desde Cart/Catalog o desde adaptadores server-side durante la transición.
4. Calcular subtotal y total exclusivamente en servidor.
5. Rechazar carrito vacío, producto inactivo, stock insuficiente o inconsistencias.
6. Mantener PayPal y `CN_Venta` como legado durante este corte.
7. Agregar pruebas de manipulación de precio y cantidad.
8. Validar en Mono/XSP con feature flag activo y rollback desactivándolo.

## Criterio de terminado del próximo incremento

- Las operaciones de Cart quedan verificadas contra SQL Server y dejan los datos de prueba en su estado inicial.
- Un cliente no puede acceder al carrito de otro cambiando payload o rutas.
- Checkout ignora precios y totales enviados por el navegador.
- La solución .NET 8 y el legado compilan.
- Las pruebas pasan.
- La base conserva exactamente el mismo esquema.
- El resultado se registra en `REGISTRO_CAMBIOS_MICROSERVICIOS.md`.

## Archivos que deben leerse antes de continuar

1. `HANDOFF_MIGRACION.md`.
2. `REGISTRO_CAMBIOS_MICROSERVICIOS.md`.
3. `EJECUCION_MONO_MACOS.md`.
4. `README.md`.
5. `CapaPresentacionTienda/Controllers/AccesoController.cs`.
6. `CapaPresentacionTienda/Controllers/TiendaController.cs`.
7. `CapaPresentacionTienda/IdentityApiClient.cs`.
8. `CapaPresentacionTienda/CartApiClient.cs`.
9. `src/Services/Cart/Cart.Application/CartService.cs`.
10. `src/Services/Cart/Cart.Infrastructure/SqlCartRepository.cs`.
11. `src/Services/Cart/Cart.Api/Controllers/CartController.cs`.
12. `src/Services/Identity/Identity.Infrastructure/JwtTokenIssuer.cs`.

## Comandos de control antes de editar

```bash
git status --short --untracked-files=all
dotnet test Ecommerce.Services.sln --configuration Release --no-restore
msbuild ecommerce.sln /t:Build /p:Configuration=Debug /verbosity:minimal
docker ps --filter name=mi_sql_server_2022
```

No iniciar servidores duplicados. Comprobar primero los puertos `5137`, `5204`, `5292`, `8080` y `8081`.
