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
- Descuento de stock mediante `FindOneAndUpdate`.
- Reversión manual de stock si falla la reserva de otro producto.
- Persistencia del detalle y datos de envío.
- Eliminación del carrito después de crear la orden.
- Historial de compras del cliente.

## Validaciones realizadas

- `dotnet build Front.Web.sln` completó sin advertencias en la última validación documentada.
- `GET /api/location/states` respondió correctamente.
- Se probó login, agregar al carrito, checkout, descuento de stock, creación de orden, historial y vaciado del carrito.
- Las configuraciones sensibles de MongoDB y JWT permanecen en configuración local no versionada.

Estas evidencias deben volver a ejecutarse después de cambios relevantes; una evidencia histórica no sustituye una prueba actual.

## Riesgos prioritarios

### Seguridad de contraseñas

`MongoCustomerService` crea y verifica contraseñas con SHA-256 sin salt. Este formato sólo debe conservarse para compatibilidad temporal. Los registros nuevos deben utilizar PBKDF2, Argon2id o el `PasswordHasher` de ASP.NET Core, con rehash progresivo de cuentas heredadas.

### Autenticación híbrida

El login consulta primero MongoDB y después Identity API. Esto mantiene dos fuentes de identidad y dos políticas de contraseña. Debe definirse `Customer.Api` o Identity como fuente autoritativa y retirar la emisión local de tokens desde el frontend.

### Autorización incompleta

La aplicación guarda identidad en sesión y realiza comprobaciones manuales de `CustomerId`. Faltan `AddAuthentication`, `UseAuthentication`, autorización por políticas, expiración uniforme y protección de rutas mediante atributos.

### Precio del carrito

El checkout usa el precio almacenado dentro del carrito. Debe reconstruir el precio desde el producto vigente en servidor o aplicar una política explícita de precio reservado. Ningún importe del carrito debe considerarse autoritativo.

### Consistencia de checkout

El descuento de stock, la inserción de la venta y la eliminación del carrito no forman una única transacción. Un fallo intermedio puede dejar stock descontado sin orden o una orden con carrito no eliminado.

### Concurrencia de identificadores

Clientes y ventas generan `idSqlOriginal` mediante máximo más uno. Solicitudes simultáneas pueden producir identificadores repetidos. Deben utilizarse identificadores MongoDB o contadores atómicos, además de índices únicos cuando corresponda.

### Validaciones del carrito

Al agregar productos se debe validar que:

- El cliente exista.
- El producto esté activo.
- Exista stock.
- La cantidad sea positiva y esté dentro de límites.
- No se cree una referencia ficticia si falta el cliente.

### Operación

Faltan pruebas automatizadas suficientes, health checks de MongoDB, logging estructurado, correlation ID, métricas, trazas y pipelines de CI/CD documentados.

## Roadmap vigente

### Etapa 1 — Estabilización de seguridad e integridad

Estado: siguiente incremento.

- Sustituir SHA-256 para contraseñas nuevas.
- Implementar verificación compatible y rehash progresivo.
- Recalcular precios desde productos durante checkout.
- Validar cliente, producto activo, cantidad y stock en carrito.
- Eliminar IDs calculados con máximo más uno.
- Agregar índices únicos necesarios.
- Hacer checkout transaccional o diseñar idempotencia y compensación persistente.
- Agregar pruebas de fallo parcial y concurrencia.

Criterio de terminado:

- Ninguna cuenta nueva usa SHA-256.
- Alterar el precio almacenado en carrito no altera el total final.
- Reintentar checkout no duplica una orden ni descuenta stock dos veces.
- Un fallo después de reservar stock deja un estado recuperable y trazable.
- Build y pruebas pasan sin secretos versionados.

### Etapa 2 — Customer.Api

- Convertir Customer/Identity en fuente autoritativa.
- Registro, login, perfil y actualización de datos.
- Cambio obligatorio y cambio autenticado de contraseña.
- Recuperación mediante token opaco de un solo uso.
- JWT, roles, bloqueo, rate limiting y políticas.
- Migración progresiva de hashes heredados.
- Retirar acceso de `Front.Web` a la colección `clientes`.

### Etapa 3 — Order.Api

- Crear órdenes desde identidad autenticada y carrito server-side.
- Guardar snapshots de nombre y precio.
- Usar clave de idempotencia.
- Transacción de orden, stock y carrito cuando sea viable.
- Modelar estados y transiciones.
- Exponer historial y detalle.
- Retirar `MongoOrderService` de `Front.Web`.

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
