# Guía para instalar y ejecutar Ecommerce Paduki

Este archivo está dirigido a un agente de IA que recibe el repositorio y debe dejar la aplicación funcionando. Ejecuta los pasos en orden, no inventes secretos y no publiques el archivo `.env`.

## 1. Alcance

El repositorio contiene una demo de e-commerce formada por ocho procesos ASP.NET Core. El despliegue soportado por el proyecto es local o en un único servidor accesible mediante `localhost`. No hay Dockerfile, Docker Compose, manifiestos de nube, gateway, proxy inverso ni CI/CD. No presentes `start.sh` como un despliegue de producción.

Aplicaciones:

| Aplicación | Puerto | Función |
| --- | ---: | --- |
| `Front.Web` | 5160 | Tienda |
| `Customer.Api` | 5161 | Clientes y autenticación |
| `Order.Api` | 5162 | Pedidos |
| `Admin.Web` | 5163 | Panel administrativo |
| `Administration.Api` | 5164 | Administradores |
| `Catalog.Api` | 5165 | Catálogo e imágenes |
| `Cart.Api` | 5166 | Carrito |
| `Location.Api` | 5167 | Ubicaciones |

PayPal funciona mediante el simulador `PAYPAL-SIM-*`; no solicites credenciales reales.

## 2. Requisitos del equipo

Comprueba antes de modificar el repositorio:

- macOS o Linux con Bash. En Windows usa WSL; los scripts son Bash.
- .NET SDK `8.0.420` o un parche posterior de .NET 8 compatible con `global.json`.
- `curl` y `lsof`, usados por `start.sh`.
- Acceso de red a una instancia MongoDB compatible con transacciones. Para el checkout concurrente se recomienda un replica set o MongoDB Atlas.
- Opcional: MongoDB Database Tools (`mongoimport`) si se restaurarán los dumps de `mongo/`.
- Acceso saliente HTTPS a Cloudinary si se cargarán imágenes.
- Acceso SMTP si se probarán recuperación de contraseña o correos de estado.

Verifica:

```bash
dotnet --version
curl --version
lsof -v
```

La versión de `dotnet` debe ser 8.0.420 o un parche de .NET 8 aceptado por `global.json`.

## 3. Restaurar dependencias y comprobar el código

Desde la raíz del repositorio:

```bash
dotnet restore Front.Web.sln
dotnet build Front.Web.sln --configuration Release
dotnet test Front.Web.sln --configuration Release --no-build
```

No continúes si el build o las pruebas fallan. No desactives validaciones, hooks ni controles de seguridad para forzar el arranque.

## 4. Crear la configuración local

Toda la configuración privada debe vivir en un único archivo `.env` en la raíz. Está ignorado por Git. No crees `appsettings.Local.json`: aunque los programas todavía lo admiten como archivo opcional, las variables de entorno tienen prioridad y `.env` es la fuente local oficial.

Si el propietario proporciona un `.env`, colócalo como:

```text
<raíz-del-repositorio>/.env
```

Si debes prepararlo, crea esta plantilla y pide al propietario los valores secretos. Nunca inventes, imprimas, registres ni confirmes en Git valores reales:

```dotenv
# MongoDB: obligatorio
MongoDb__ConnectionString='REEMPLAZAR'
MongoDb__DatabaseName='ecommerce'

# JWT: obligatorio y compartido por todos los servicios
Jwt__Issuer='Ecommerce.Identity'
Jwt__Audience='Ecommerce.Services'
Jwt__SigningKey='REEMPLAZAR_CON_AL_MENOS_32_CARACTERES'
Jwt__LifetimeMinutes='30'

# Direcciones internas: necesarias para las aplicaciones web
Services__Administration='http://localhost:5164'
Services__Catalog='http://localhost:5165'
Services__Customer='http://localhost:5161'
Services__Order='http://localhost:5162'
Services__Cart='http://localhost:5166'
Services__Location='http://localhost:5167'

# SMTP: necesario para enviar correos; sin valores válidos el resto de la demo puede arrancar
Email__SmtpHost='REEMPLAZAR'
Email__SmtpPort='REEMPLAZAR'
Email__SmtpUsername='REEMPLAZAR'
Email__SmtpPassword='REEMPLAZAR'
Email__From='REEMPLAZAR'
Email__FromName='Ecommerce Paduki'
Email__SmtpUseSsl='REEMPLAZAR'
Email__CheckCertificateRevocation='REEMPLAZAR'
Email__ResetBaseUrl='http://localhost:5160'
Email__AdminResetBaseUrl='http://localhost:5163'

# Cloudinary: necesario para subir, reemplazar y eliminar imágenes
Cloudinary__CloudName='REEMPLAZAR'
Cloudinary__ApiKey='REEMPLAZAR'
Cloudinary__ApiSecret='REEMPLAZAR'
```

Reglas importantes:

- `Jwt__SigningKey` debe tener al menos 32 caracteres y ser exactamente igual para todos los procesos.
- `Services__Administration`, `Services__Catalog`, `Services__Customer` y `Services__Order` son imprescindibles para `Admin.Web`; no los omitas.
- Mantén separadas `Email__ResetBaseUrl` y `Email__AdminResetBaseUrl`.
- No adivines `Email__SmtpPort` ni `Email__SmtpUseSsl`: usa la combinación entregada por el propietario y valida recuperación tanto de cliente como de administrador, pues los servicios negocian TLS de forma distinta.
- Las imágenes existentes se leen desde URLs públicas almacenadas en MongoDB. Las credenciales Cloudinary sólo son necesarias para administrar imágenes.
- No agregues credenciales PayPal: la demo usa el simulador.
- Antes de continuar, comprueba con `git status --ignored` que `.env` aparece como ignorado.

## 5. Preparar MongoDB

### Opción A: usar una base ya preparada

Configura `MongoDb__ConnectionString` y `MongoDb__DatabaseName` con los datos entregados por el propietario. La IP del servidor debe estar autorizada si se usa MongoDB Atlas.

### Opción B: restaurar los dumps en una base vacía

Los archivos `mongo/*.json` son arreglos JSON para estas colecciones:

```text
carritos categorias clientes estados marcas productos usuarios ventas
```

Sólo sobre una base nueva y vacía, importa cada archivo con `--jsonArray`. Sustituye la URI y el nombre de base sin escribir credenciales en el historial del shell. Ejemplo conceptual:

```bash
mongoimport --uri "$MONGODB_URI" --db ecommerce --collection productos --file mongo/productos.json --jsonArray
```

Repite para cada colección. No uses `--drop` sobre una base existente y no importes dumps encima de datos reales sin autorización explícita. Las colecciones `auditoria` y `counters` se crean automáticamente.

Al arrancar, los servicios crean o comprueban los índices necesarios. Revisa los logs y confirma que no haya errores de índices únicos. Los productos sin `imageUrl` mostrarán el placeholder local.

## 6. Iniciar la aplicación

Desde la raíz:

```bash
chmod +x start.sh stop.sh
./start.sh
```

`start.sh` realiza lo siguiente:

1. Carga y exporta `.env`.
2. Comprueba que los puertos 5160–5167 estén libres.
3. Inicia los ocho proyectos.
4. Guarda PID y logs bajo `.logs/`.
5. Espera hasta 30 segundos por proceso y comprueba sus rutas de disponibilidad.

Resultado esperado:

```text
Todos los servicios están listos.
Front.Web: http://localhost:5160
Admin.Web: http://localhost:5163
```

Si falla un proceso, no ejecutes repetidamente el script. Lee primero su log:

```bash
ls -la .logs
tail -n 200 .logs/catalog-api.log
tail -n 200 .logs/administration-api.log
```

Causas frecuentes:

- Puerto ocupado: identifica el proceso con `lsof -iTCP:<puerto> -sTCP:LISTEN`; no lo mates sin confirmar que pertenece a esta aplicación.
- MongoDB inaccesible: revisa URI, DNS, red y lista de IP permitidas.
- Error JWT: la clave falta o tiene menos de 32 caracteres.
- `Catalog.Api` devuelve `503` al cargar una imagen: faltan credenciales Cloudinary.
- `Admin.Web` no inicia: falta alguna URL `Services__*` requerida.

Para detener únicamente los procesos registrados por `start.sh`:

```bash
./stop.sh
```

No uses `killall dotnet` porque podría detener aplicaciones ajenas.

## 7. Verificación posterior al arranque

Comprueba los APIs:

```bash
curl -f http://localhost:5161/health
curl -f http://localhost:5162/health
curl -f http://localhost:5164/health
curl -f http://localhost:5165/health
curl -f http://localhost:5166/health
curl -f http://localhost:5167/health
```

Comprueba las aplicaciones web:

```bash
curl -f http://localhost:5160/Store
curl -f http://localhost:5163/Account/Login
```

Después verifica manualmente:

1. La tienda muestra categorías, marcas, productos e imágenes.
2. Un cliente puede registrarse e iniciar sesión.
3. El carrito permite agregar productos y completar un checkout simulado.
4. El historial aparece en `/Order`.
5. Un administrador autorizado puede entrar al panel, administrar catálogo y pedidos.
6. Si Cloudinary está configurado, una imagen JPG, PNG o WebP menor de 5 MB puede cargarse y verse en tienda.

Con los servicios levantados y una base descartable o autorizada, ejecuta la regresión integrada:

```bash
dotnet run --project tests/Integration.Seed
```

Esta prueba crea y limpia datos, prueba concurrencia, idempotencia y rollback. No la ejecutes contra una base que no esté autorizada para pruebas.

## 8. Ejecutar un servicio aislado

Un `dotnet run` directo no lee `.env` por sí solo. Exporta primero sus variables:

```bash
set -a
source .env
set +a
dotnet run --project src/Services/Catalog/Catalog.Api/Catalog.Api.csproj --urls http://localhost:5165
```

Usa el puerto correspondiente de la tabla inicial.

## 9. Despliegue en otro servidor

Para una demo en otro macOS/Linux, copia el repositorio sin `.logs`, `bin` ni `obj`; instala los requisitos; transfiere `.env` por un canal privado; restaura, compila y ejecuta `./start.sh` en el servidor.

Los scripts enlazan explícitamente a `localhost`. Para acceder desde otra computadora hace falta un proxy inverso o cambiar el binding y las URL públicas. Eso no está implementado en el repositorio. Antes de exponerlo a Internet se requieren, como mínimo:

- HTTPS y proxy inverso.
- Gestión externa de secretos; no dejar `.env` legible por otros usuarios.
- Un supervisor de procesos (`systemd`, equivalente o contenedores).
- URLs públicas correctas para servicios y recuperación de contraseña.
- Restricción de red para APIs internas y MongoDB.
- Sesiones y rate limiting distribuidos si habrá más de una instancia.
- Observabilidad, backups y procedimiento de rollback.

Si el objetivo es producción o Internet público, detente y pide al propietario elegir infraestructura. No inventes Docker, DNS, certificados, cuentas cloud ni reglas de firewall sin autorización.

## 10. Criterio de entrega

La instalación queda terminada únicamente cuando:

- `dotnet build` y `dotnet test` finalizan sin errores.
- Los ocho procesos aparecen listos en `start.sh`.
- Los seis endpoints `/health` responden correctamente.
- Tienda y panel administrativo responden.
- Catálogo, autenticación, carrito y checkout simulado funcionan.
- Los secretos siguen fuera de Git y no aparecen en logs ni en la respuesta del agente.
- El agente informa qué verificó realmente, qué omitió y cualquier limitación del entorno.
