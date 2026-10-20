# Migración de Ecommerce Paduki

## Propósito

Este documento es la fuente autoritativa del estado, decisiones, riesgos, pendientes e historial de la migración de Ecommerce Paduki.

Debe actualizarse después de cada incremento funcional. No se debe marcar una etapa como terminada hasta que compile, tenga pruebas ejecutadas y exista una estrategia de rollback o recuperación.

## Estado ejecutivo

El sistema evolucionó desde dos aplicaciones ASP.NET MVC 5 sobre .NET Framework 4.7.2 y SQL Server hacia una solución ASP.NET Core 8 con interfaces separadas y APIs por dominio sobre MongoDB.

Actualmente `Front.Web` consume `Catalog.Api`, `Customer.Api`, `Order.Api`, `Cart.Api` y `Location.Api` mediante HTTP/JWT y ya no accede directamente a MongoDB ni referencia `MongoDB.Driver`. Los flujos de registro, login, recuperación, catálogo, carrito, checkout e historial están implementados y fueron probados de extremo a extremo contra el servidor privado configurado localmente.

`Admin.Web` consume `Administration.Api`, `Catalog.Api`, `Customer.Api` y `Order.Api` para autenticación administrativa, catálogo, métricas, pedidos y reportes. La propiedad de datos es por dominio; la única excepción transitoria es la colección `carritos`, propiedad de `Cart.Api` y leída/eliminada por `Order.Api` dentro de la transacción de checkout.

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
        └── auditoria       (Administration.Api/Catalog.Api)
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
- Catálogo, filtros y detalle mediante `Catalog.Api`.
- Ubicaciones dependientes mediante `Location.Api` y carrito persistente mediante `Cart.Api`.
- Checkout autenticado e historial mediante `Order.Api`.
- Creación transaccional e idempotente de órdenes, reconstrucción de precios, descuento condicional de stock y vaciado del carrito.
- Sin `MongoDB.Driver` ni acceso directo a la base de datos.

### Admin.Web

- Login, recuperación, reset y cierre de sesión administrativos.
- Dashboard de clientes, ventas, ingresos, productos y stock bajo.
- Gestión de administradores, categorías, marcas, productos, stock e imágenes.
- Pedidos de sólo lectura, detalle, reportes y exportación XLSX.
- JWT en sesión del servidor, antiforgery global, cookies seguras y cabeceras defensivas.

### APIs

- `Administration.Api`: identidad administrativa, bloqueo, rate limiting, recuperación, gestión de usuarios y auditoría.
- `Catalog.Api`: lectura pública y administración protegida de catálogo, stock e imágenes Cloudinary.
- `Customer.Api`: identidad y recuperación de clientes, además de métrica administrativa protegida.
- `Order.Api`: checkout, historial, pedidos administrativos, dashboard y reportes.
- `Cart.Api`: carrito por cliente autenticado con JWT; valida producto activo, stock y máximo 99 unidades.
- `Location.Api`: lectura pública de estados, municipios y localidades.

## Validaciones realizadas

- `dotnet test Front.Web.sln --configuration Release` completó con 72 pruebas aprobadas el 2026-10-14; el comando también restauró y compiló la solución sin errores.
- La validación integrada más reciente comprobó health checks, registro, login, carrito mediante `Cart.Api`, ubicaciones mediante `Location.Api`, checkout, historial, autorización administrativa, recuperación SMTP, dashboard, catálogo, pedidos y reportes.
- Las configuraciones sensibles de MongoDB, JWT, SMTP y Cloudinary permanecen en configuración local no versionada.
- Los inicializadores de Administration y Catalog detectan índices únicos equivalentes por clave y opciones, sin depender de su nombre; se validaron en vivo contra MongoDB sin código 85.
- La auditoría de paquetes detectó y corrigió transitivos vulnerables en Customer.Api, Order.Api.UnitTests y Admin.Web.UnitTests; la repetición quedó limpia.
- Continúan pendientes Cloudinary real, correo de establecimiento al crear administrador, checkout concurrente y rollback de fallo parcial.

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
- [ ] Validar Cloudinary con credenciales reales y sus casos de error.
- [ ] Retirar imágenes ilustrativas y rutas físicas heredadas de los datos.

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

- Cambio obligatorio y cambio autenticado de contraseña en la experiencia de cliente.
- Perfil completo y actualización de datos del cliente.
- Estados y transiciones de pedidos, paquetería y guía.
- PayPal completo, webhooks, idempotencia y conciliación.
- Validación real de cargas y reemplazos de imágenes en Cloudinary.
- Reportes administrativos adicionales a los ya implementados.
- Notificaciones transaccionales.

## Configuración local

Requisitos actuales:

- .NET SDK 8 compatible con el proyecto.
- Acceso autorizado a MongoDB.
- Valores locales para `MongoDb:ConnectionString`, `MongoDb:DatabaseName` y JWT compartido.
- `Jwt:SigningKey` de al menos 32 caracteres; issuer, audience y clave deben coincidir en todos los servicios.
- SMTP para recuperación funcional y Cloudinary para cargas de imágenes.

Los valores sensibles deben estar en archivos `appsettings.Local.json` de cada servicio, user-secrets o variables de entorno. `appsettings.Local.json` no se versiona.

Ejecución:

```bash
dotnet restore Front.Web.sln
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
./start.sh
```

`start.sh` comprueba y utiliza los puertos `5160`–`5167`; `stop.sh` sólo detiene los procesos que registró.

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
