# Ecommerce Paduki

Ecommerce en migración incremental desde ASP.NET MVC 5 y SQL Server hacia ASP.NET Core 8, MongoDB y APIs separadas por dominio.

La fuente autoritativa del estado, historial, riesgos y roadmap es [`MIGRACION.md`](MIGRACION.md). Debe leerse antes de continuar cualquier incremento.

## Estado actual

`Front.Web` es la interfaz vigente de la tienda. Actualmente consume MongoDB directamente para catálogo, registro y login de clientes, ubicaciones, carrito, checkout, órdenes, stock e historial de compras.

Este acceso directo es temporal. El objetivo es que `Front.Web` consuma APIs HTTP y deje de incluir lógica de negocio o acceso a MongoDB.

## Componentes

- `src/Services/Front/Front.Web`: frontend ASP.NET Core 8 vigente.
- `src/Services/Catalog`: primera implementación de Catalog Service sobre SQL Server.
- `src/Services/Identity`: primera implementación de Identity Service sobre SQL Server.
- `src/Services/Cart`: primera implementación de Cart Service sobre SQL Server.
- `CapaPresentacionAdmin` y `CapaPresentacionTienda`: aplicaciones heredadas.
- `CapaNegocio`, `CapaDatos` y `CapaEntidad`: capas del sistema original.

Los servicios SQL y las aplicaciones heredadas se conservan como referencia e historial durante la transición.

## Requisitos para Front.Web

- .NET SDK 8.
- Acceso autorizado al servidor MongoDB.
- Configuración local de MongoDB y JWT.

## Configuración local

Crear `src/Services/Front/Front.Web/appsettings.Local.json` únicamente en el entorno local:

```json
{
  "MongoDb": {
    "ConnectionString": "<CONEXION_MONGODB>",
    "DatabaseName": "<BASE_MONGODB>"
  },
  "Jwt": {
    "Issuer": "Ecommerce.Identity",
    "Audience": "Ecommerce.Services",
    "SigningKey": "<CLAVE_LOCAL_DE_AL_MENOS_32_CARACTERES>",
    "LifetimeMinutes": 30
  }
}
```

`appsettings.Local.json` debe permanecer fuera de Git. También pueden utilizarse user secrets o variables de entorno.

## Compilar y ejecutar

```bash
dotnet build Front.Web.sln
dotnet run --project src/Services/Front/Front.Web/Front.Web.csproj --urls "http://localhost:5160"
```

Abrir `http://localhost:5160`. Antes de iniciar otra instancia, comprobar que el puerto no esté ocupado.

## Validación mínima

```bash
dotnet build Front.Web.sln
curl -i http://localhost:5160/api/location/states
```

Validar también registro o login, catálogo, carrito, checkout, creación de orden, descuento de stock, historial y vaciado del carrito.

## Sistema heredado

El sistema original utiliza .NET Framework 4.7.2, ASP.NET MVC 5, Forms Authentication y SQL Server. En macOS se validó temporalmente con Mono, MSBuild y XSP4:

```bash
msbuild ecommerce.sln /t:Build /p:Configuration=Debug /verbosity:minimal
```

La tienda heredada usa el puerto `8081` y el panel administrativo el `8080`. Mono/XSP es sólo una referencia durante la migración, no la plataforma objetivo.

## Seguridad

- No versionar conexiones, contraseñas, JWT, secretos de PayPal o SMTP.
- No copiar datos personales ni tokens a documentación o logs.
- No publicar respaldos o exportaciones de bases reales.
- No confiar en identificadores de cliente, precios o totales enviados por el navegador.
- Rotar cualquier secreto expuesto.

## Próximo incremento

1. Dejar de crear contraseñas SHA-256.
2. Recalcular precios desde productos en servidor.
3. Validar cliente, producto activo, stock y cantidades.
4. Eliminar identificadores generados mediante máximo más uno.
5. Hacer la creación de órdenes transaccional o idempotente.
6. Agregar pruebas de concurrencia y fallos parciales.

El detalle y los criterios de terminado están en [`MIGRACION.md`](MIGRACION.md).
