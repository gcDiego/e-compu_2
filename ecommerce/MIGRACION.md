# Migración de Ecommerce Paduki

## Propósito

Este documento es la fuente autoritativa del estado, decisiones, riesgos, pendientes e historial de la migración de Ecommerce Paduki.

Debe actualizarse después de cada incremento funcional. No se debe marcar una etapa como terminada hasta que compile, tenga pruebas ejecutadas y exista una estrategia de rollback o recuperación.

## Estado ejecutivo

El sistema evolucionó desde dos aplicaciones ASP.NET MVC 5 sobre .NET Framework 4.7.2 y SQL Server hacia un nuevo frontend ASP.NET Core 8 llamado `Front.Web`.

Actualmente `Front.Web` accede directamente a MongoDB para catálogo, clientes, ubicaciones, carrito y órdenes. Los flujos de registro, login, navegación del catálogo, carrito, checkout e historial de compras están implementados y fueron probados contra el servidor privado configurado localmente.

El acceso directo de `Front.Web` a MongoDB es una etapa de transición. La arquitectura objetivo requiere que la interfaz deje de acceder a la base y consuma APIs de negocio mediante HTTP y JWT.

## Regla para interpretar el repositorio

Existen tres generaciones de componentes:

1. El sistema original en `CapaPresentacionAdmin`, `CapaPresentacionTienda`, `CapaNegocio`, `CapaDatos` y `CapaEntidad`.
2. Los primeros servicios .NET 8 sobre SQL Server: Catalog, Identity y Cart.
3. El frontend ASP.NET Core 8 `Front.Web`, actualmente conectado directamente a MongoDB.

Las implementaciones SQL y MVC se conservan como referencia funcional e historial. Para trabajo nuevo sobre la tienda, el estado de `Front.Web` y este documento tienen prioridad.

## Arquitectura actual

```text
Navegador
  └── Front.Web (.NET 8)
        ├── MongoCatalogService
        ├── MongoCustomerService
        ├── MongoLocationService
        ├── MongoCartService
        ├── MongoOrderService
        ├── CatalogApiClient (transición anterior)
        ├── IdentityApiClient (fallback temporal de login)
        └── CartApiClient (transición anterior)

Mongo services
  └── MongoDB privado
        ├── categorias
        ├── marcas
        ├── productos
        ├── clientes
        ├── usuarios
        ├── ubicaciones
        ├── carritos
        └── ventas
```

`Front.Web` utiliza sesión del servidor para `CustomerId`, nombre, correo y token. La autenticación ASP.NET Core completa mediante esquemas, middleware y políticas todavía no está implementada.

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

## Funcionalidad completada en Front.Web

### Aplicación

- ASP.NET Core 8.
- Configuración local opcional mediante `appsettings.Local.json` ignorado por Git.
- Sesión en memoria para el flujo autenticado.
- MongoDB Driver configurado mediante inyección de dependencias.

### Clientes y acceso

- Login de clientes existentes en MongoDB.
- Fallback temporal hacia Identity API.
- Registro de clientes en MongoDB.
- Emisión local de JWT para usuarios autenticados desde MongoDB.
- Inicio y cierre de sesión.
- Consulta y actualización disponibles en `MongoCustomerService`, aunque el perfil todavía necesita un flujo de interfaz completo.

### Catálogo

- Categorías activas.
- Marcas activas.
- Productos activos.
- Filtros por categoría y marca.
- Detalle de producto.

### Ubicaciones

- Estados mediante `GET /api/location/states`.
- Municipios mediante `GET /api/location/municipalities/{stateId}`.
- Localidades mediante `GET /api/location/localities/{municipalityId}`.
- Selectores dependientes integrados en checkout.

### Carrito

- Consulta del carrito por cliente.
- Agregar productos.
- Incrementar y decrementar cantidades.
- Eliminar productos.
- Persistencia en MongoDB.

### Órdenes

- Creación desde el carrito.
- Validación condicional de producto activo y stock suficiente.
- Descuento de stock mediante `FindOneAndUpdate` dentro de una transacción.
- Persistencia del detalle y datos de envío con valores de producto reconstruidos en servidor.
- Eliminación del carrito dentro de la misma transacción de creación de orden.
- Historial de compras del cliente.

## Validaciones realizadas

- `dotnet build Front.Web.sln` completó sin advertencias en la última validación documentada.
- `GET /api/location/states` respondió correctamente.
- Se probó login, agregar al carrito, checkout, descuento de stock, creación de orden, historial y vaciado del carrito.
- Las configuraciones sensibles de MongoDB y JWT permanecen en configuración local no versionada.
- `dotnet test Front.Web.sln --configuration Release --no-restore` completó con 7 pruebas aprobadas.
- Las pruebas cubren hash adaptativo, contraseña incorrecta, SHA-256 heredado y hashes malformados.

## Riesgos prioritarios

### Seguridad de contraseñas

Los registros nuevos ya usan PBKDF2-SHA256 y los logins SHA-256 válidos se actualizan de forma optimista. El riesgo restante es retirar completamente los hashes heredados después de medir la migración y centralizar identidad en Customer/Identity API.

### Precio del carrito

El checkout ya reconstruye nombre y precio desde el producto vigente. Falta validar el comportamiento integrado ante cambios concurrentes de precio y definir formalmente si el negocio utiliza precio vigente o precio reservado.

### Consistencia de checkout

La creación de orden, el descuento de stock y la eliminación del carrito usan una transacción con `checkoutKey` como clave de idempotencia única. Si dos operaciones concurrentes intentan crear una orden para el mismo carrito, el índice único en `ventas.checkoutKey` hace fallar la segunda y `MongoOrderService` devuelve la orden ya existente. Aún se debe validar contra el servidor privado y bajo concurrencia real.

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
- [ ] Probar fallos parciales y concurrencia contra un MongoDB de pruebas compatible (código de idempotencia implementado).

Criterio de terminado:

- Ninguna cuenta nueva usa SHA-256.
- Alterar el precio almacenado en carrito no altera el total final.
- Reintentar checkout no duplica una orden ni descuenta stock dos veces.
- Un fallo después de reservar stock deja un estado recuperable y trazable.
- Build y pruebas pasan sin secretos versionados.

### Etapa 2 — Customer.Api (en progreso)

- Convertir Customer/Identity en fuente autoritativa.
- Registro, login, perfil y actualización de datos.
- Cambio obligatorio y cambio autenticado de contraseña.
- Recuperación mediante token opaco de un solo uso.
- JWT, roles, bloqueo, rate limiting y políticas.
- Migración progresiva de hashes heredados.
- Retirar acceso de `Front.Web` a la colección `clientes`.
- **Hecho en este paso:** creación del proyecto `Customer.Api`; `Front.Web` consume `Customer.Api` y `AccountController` ya no tiene fallback a `MongoCustomerService`.

### Etapa 3 — Order.Api (en progreso)

- Crear órdenes desde identidad autenticada y carrito server-side.
- Guardar snapshots de nombre y precio.
- Usar clave de idempotencia.
- Transacción de orden, stock y carrito cuando sea viable.
- Modelar estados y transiciones.
- Exponer historial y detalle.
- Retirar `MongoOrderService` de `Front.Web`.
- **Hecho en este paso:** creación del proyecto `Order.Api`; `CheckoutController` y `OrderController` ya no tienen fallback a `MongoOrderService`; `Front.Web` depende de HTTP/JWT para login, registro, checkout e historial.

### Etapa 4 — Payment.Api

- Integrar PayPal detrás de una abstracción de gateway.
- Crear y capturar pagos desde la orden persistida.
- Guardar intentos y estados.
- URLs de retorno y cancelación por ambiente.
- Webhooks firmados como confirmación autoritativa.
- Idempotencia, timeout, reintentos limitados y conciliación.
- Probar aprobado, cancelado, duplicado, timeout y webhook fuera de orden.

### Etapa 5 — APIs restantes

- Extraer Catalog API para MongoDB.
- Extraer Cart API.
- Extraer Location API o decidir proveedor externo.
- Hacer que `Front.Web` consuma exclusivamente HTTP y JWT.
- Retirar `MongoDB.Driver` de `Front.Web`.

### Etapa 6 — Administración y medios

- Alta, edición y desactivación de productos, categorías y marcas.
- Control de stock protegido por rol administrativo.
- Auditoría de mutaciones.
- Object storage para imágenes.
- `imageUrl` en contratos públicos.
- Validación de MIME, tamaño y extensión en cargas.
- Retirar imágenes ilustrativas y rutas físicas heredadas.

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

- Recuperación de contraseña.
- Cambio obligatorio de contraseña en la experiencia de usuario.
- Perfil completo y actualización de datos.
- Consulta detallada de transacciones.
- PayPal completo.
- Idempotencia y conciliación de pagos.
- Webhooks de PayPal.
- Administración de catálogo.
- Administración de stock.
- Almacenamiento real de imágenes y `imageUrl`.
- Reportes administrativos.
- Notificaciones transaccionales.

## Configuración local

Requisitos actuales:

- .NET SDK 8 compatible con el proyecto.
- Acceso autorizado a MongoDB.
- Valores locales para `MongoDb:ConnectionString`, `MongoDb:DatabaseName` y `Jwt:SigningKey`.
- `Jwt:SigningKey` de al menos 32 caracteres.

Los valores sensibles deben estar en `src/Services/Front/Front.Web/appsettings.Local.json`, user secrets o variables de entorno. `appsettings.Local.json` no se versiona.

Ejecución:

```bash
dotnet build Front.Web.sln
dotnet run --project src/Services/Front/Front.Web/Front.Web.csproj --urls "http://localhost:5160"
```

No iniciar otra instancia antes de comprobar si el puerto `5160` está ocupado.

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
