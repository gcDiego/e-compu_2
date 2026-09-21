# Ejecución del e-commerce legado en macOS con Mono

## Objetivo

Este documento reúne el contexto técnico y los pasos validados para compilar y ejecutar en macOS la solución e-commerce heredada, escrita con ASP.NET MVC 5 y .NET Framework. También delimita el alcance de esta ejecución durante su migración a microservicios.

## Conclusión

Sí es posible ejecutar este proyecto específico en macOS mediante Mono, MSBuild y XSP4.

La solución completa compiló y las dos aplicaciones web iniciaron correctamente. Tanto el panel administrativo como la tienda respondieron con HTTP 200.

Esta no es una ejecución nativa mediante `dotnet run`. Mono y XSP funcionan como una solución temporal para examinar, probar y comparar el sistema legado durante la migración. No debe asumirse que todas las APIs de .NET Framework o todos los comportamientos de IIS serán compatibles.

## Arquitectura del proyecto

La solución `ecommerce.sln` contiene cinco proyectos:

- `CapaEntidad`: entidades y modelos del dominio.
- `CapaDatos`: acceso a SQL Server mediante ADO.NET y `System.Data.SqlClient`.
- `CapaNegocio`: reglas y servicios de negocio.
- `CapaPresentacionAdmin`: aplicación ASP.NET MVC del panel administrativo.
- `CapaPresentacionTienda`: aplicación ASP.NET MVC de la tienda.

Los proyectos web utilizan:

- .NET Framework 4.7.2.
- ASP.NET MVC 5.2.9.
- `System.Web`.
- Razor.
- Forms Authentication.
- Configuración mediante `Web.config`.
- IIS Express en su configuración original de Visual Studio.

## Entorno validado

La ejecución fue comprobada con:

- macOS en Apple Silicon.
- Mono JIT 6.12.0.
- MSBuild para Mono 16.10.1.
- XSP4.
- SQL Server 2022 ejecutado en Docker.
- Base de datos `ecommerce`.
- SQL Server disponible en `localhost:1433`.

Los comandos disponibles se comprobaron con:

```bash
mono --version
command -v msbuild
command -v xsp4
```

## Base de datos

SQL Server se ejecuta en Docker con el contenedor:

```text
mi_sql_server_2022
```

La aplicación no utiliza JDBC. JDBC corresponde a aplicaciones Java. Este proyecto utiliza ADO.NET y `System.Data.SqlClient`.

La clase de conexión obtiene una cadena denominada `cadena` desde `ConfigurationManager.ConnectionStrings`.

La cadena configurada para ejecución local tiene esta estructura:

```text
Data Source=localhost,1433;Initial Catalog=ecommerce;User ID=sa;Password=<PASSWORD>;Encrypt=False
```

El proveedor configurado es:

```text
System.Data.SqlClient
```

La contraseña real no debe documentarse ni almacenarse en repositorios públicos. Debe sustituirse `<PASSWORD>` por la contraseña local de SQL Server.

Para comprobar el contenedor:

```bash
docker ps --filter name=mi_sql_server_2022
```

Para iniciarlo si está detenido:

```bash
docker start mi_sql_server_2022
```

Para comprobar el puerto:

```bash
nc -zv localhost 1433
```

## Compilación

Desde la raíz de la solución:

```bash
cd "/Users/gcdiego/Downloads/ecommerce"
msbuild ecommerce.sln /t:Build /p:Configuration=Debug /verbosity:minimal
```

La compilación de los cinco proyectos terminó correctamente.

Mono mostró una advertencia indicando que no pudo resolver `System.Web.Entity` en los proyectos web. La advertencia no impidió compilar ni iniciar las aplicaciones, pero debe considerarse al probar funcionalidades que pudieran depender de esa biblioteca.

## Ejecución del panel administrativo

Ejecutar en una terminal:

```bash
cd "/Users/gcdiego/Downloads/ecommerce/CapaPresentacionAdmin"
xsp4 --port 8080 --nonstop
```

Abrir:

```text
http://localhost:8080
```

La ruta de almacenamiento de imágenes debe ser válida para macOS. La configuración local apunta al directorio `Imagenes_Carrito` dentro de `CapaPresentacionAdmin`.

## Ejecución de la tienda

Ejecutar en otra terminal:

```bash
cd "/Users/gcdiego/Downloads/ecommerce/CapaPresentacionTienda"
xsp4 --port 8081 --nonstop
```

Abrir:

```text
http://localhost:8081
```

## Evidencia de ejecución

Ambos sitios respondieron con HTTP 200. XSP devolvió encabezados equivalentes a:

```text
Server: Mono.WebServer.XSP/4.6.0.0 MacOSX
X-AspNetMvc-Version: 5.2
X-AspNet-Version: 4.0.30319
```

Esto confirma que las aplicaciones ASP.NET MVC se cargaron mediante Mono/XSP.

## Detener las aplicaciones

Si cada aplicación se ejecuta en una terminal visible, presionar `Control + C` en cada terminal.

Para detener todas las instancias de XSP4:

```bash
pkill -f xsp4
```

Para comprobar que no quedan procesos:

```bash
pgrep -fl xsp4
```

Para detener también SQL Server:

```bash
docker stop mi_sql_server_2022
```

## Secuencia completa para volver a ejecutar

1. Iniciar SQL Server:

```bash
docker start mi_sql_server_2022
```

2. Compilar si hubo cambios:

```bash
cd "/Users/gcdiego/Downloads/ecommerce"
msbuild ecommerce.sln /t:Build /p:Configuration=Debug
```

3. Iniciar administración en una terminal:

```bash
cd "/Users/gcdiego/Downloads/ecommerce/CapaPresentacionAdmin"
xsp4 --port 8080 --nonstop
```

4. Iniciar tienda en otra terminal:

```bash
cd "/Users/gcdiego/Downloads/ecommerce/CapaPresentacionTienda"
xsp4 --port 8081 --nonstop
```

5. Abrir `http://localhost:8080` y `http://localhost:8081`.

## Limitaciones de Mono y XSP

Aunque el proyecto inicia correctamente, deben considerarse estas limitaciones:

- XSP no reproduce necesariamente todo el comportamiento de IIS o IIS Express.
- Algunas APIs exclusivas de Windows pueden no funcionar.
- La integración con rutas de archivos de Windows debe adaptarse a macOS.
- La advertencia de `System.Web.Entity` requiere atención si alguna funcionalidad depende de ella.
- Mono 6.12 y XSP son tecnologías heredadas y no constituyen una plataforma recomendada para nuevos servicios.
- La ejecución actual debe usarse para validar funcionalidad y comparar resultados durante la migración, no como arquitectura final de producción.

## Recomendaciones para la migración a microservicios

Los servicios nuevos deberían:

- Usar una versión soportada de .NET y ASP.NET Core.
- Ejecutarse con Kestrel mediante `dotnet run` o contenedores Linux.
- Evitar nuevas dependencias de `System.Web`, XSP y .NET Framework.
- Reemplazar Forms Authentication con un mecanismo moderno de identidad y autorización.
- Externalizar cadenas de conexión y secretos mediante variables de entorno o un gestor de secretos.
- Separar gradualmente los dominios y casos de uso antes de retirar las capas del monolito.
- Mantener pruebas de comparación entre el monolito en Mono y los microservicios nuevos.
- Definir claramente qué servicio será propietario de cada conjunto de datos antes de dividir la base de datos.

## Seguridad

Los archivos de configuración pueden contener credenciales de SQL Server y PayPal Sandbox. Antes de publicar o compartir el proyecto se debe:

- Revocar y reemplazar secretos expuestos.
- No incluir contraseñas reales en documentación o control de versiones.
- Usar variables de entorno o archivos locales excluidos del repositorio.
- Mantener separadas las credenciales de desarrollo y producción.

## Resumen para otra IA

El proyecto e-commerce legado usa ASP.NET MVC 5.2.9 y .NET Framework 4.7.2, con arquitectura por capas y dos aplicaciones web. En macOS Apple Silicon se validó su compilación con Mono/MSBuild y su ejecución con XSP4. El panel administrativo funciona en el puerto 8080 y la tienda en el 8081. Ambas aplicaciones respondieron HTTP 200 y se conectan a SQL Server 2022 en Docker mediante `System.Data.SqlClient`. Existe una advertencia por `System.Web.Entity`, pero no bloqueó la compilación ni el arranque. Esta ejecución es útil como entorno temporal de referencia durante la migración a microservicios; los servicios nuevos deben implementarse con ASP.NET Core y una versión soportada de .NET.
