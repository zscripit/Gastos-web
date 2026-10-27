# Gastos — app web (Blazor WebAssembly PWA)

Cliente instalable en móvil para `gastos-api`. No tiene lógica de negocio propia:
todo sale de la API REST, que es la única fuente de verdad.

## Cómo correrlo

```bash
dotnet run --launch-profile http     # http://localhost:5025
```

La URL de la API se configura con `ApiBaseUrl`:

| Archivo | Cuándo se carga | Valor |
|---|---|---|
| `wwwroot/appsettings.json` | Siempre (base) | `http://localhost:5123` |
| `wwwroot/appsettings.Production.json` | Al publicar (`dotnet publish`, fuera de Development) | `gastos-api` en Azure Container Apps |

### Requisito en el backend: CORS

La API debe permitir el origen de la app web, o el navegador bloquea toda
petición en el preflight. Los orígenes se configuran en `gastos-api` con
`Cors:AllowedOrigins` (en producción, `Cors__AllowedOrigins__0` con
`https://gastosapp-web.scrip-dev.workers.dev`, sin `/` final).

Ese mismo origen define el dominio de las passkeys: si el Worker pasa a un
dominio propio, hay que cambiarlo en la API y las passkeys ya registradas
dejan de servir (cada usuario las vuelve a activar).

### Requisito en el storage: CORS del bucket de R2

Los comprobantes se suben y se leen directo desde el navegador, así que el
bucket de R2 debe permitir los mismos orígenes con los métodos `PUT` (subida,
header `content-type`) y `GET` (el visor lee PDF y XML con `fetch`). Sin esa
regla, toda subida falla con "No se pudo subir el archivo".

## Arquitectura

| Capa | Ubicación | Qué hace |
|---|---|---|
| Modelos | `Models/` | DTOs y requests espejo de los schemas de OpenAPI |
| Infraestructura | `Services/ApiClient.cs` | Única puerta a la API; traduce toda respuesta a `ApiResult<T>` |
| Sesión | `Services/AuthService.cs`, `TokenStore.cs`, `LocalStorageService.cs`, `AuthHeaderHandler.cs`, `SessionRefresher.cs`, `JwtAuthenticationStateProvider.cs` | Login/registro, guarda el JWT y el refresh token en localStorage, renueva la sesión antes de que expire (y tras un 401), adjunta el token y deriva los claims |
| Huella o rostro | `Services/PasskeyService.cs`, `wwwroot/js/passkeys.js`, `Shared/PasskeySettings.razor` | Passkeys (WebAuthn): activar en Perfil y entrar desde el login sin contraseña |
| Recordatorios | `Services/PushService.cs`, `wwwroot/js/push.js`, `wwwroot/js/push-sw.js`, `Shared/PushSettings.razor` | Suscripción Web Push del dispositivo; el service worker muestra el aviso y abre el saldo de la tarjeta |
| Sin conexión | `Services/OfflineStore.cs`, `OfflineSync.cs`, `wwwroot/js/offline-db.js`, `Shared/OfflineBanner.razor` | IndexedDB: últimas consultas para verlas sin red y cola de movimientos que se envía al volver la conexión |
| Features | `Services/AccountService.cs`, `CategoryService.cs`, `CatalogService.cs` | Un servicio por entidad de la API |
| Análisis | `Services/AnalyticsService.cs`, `Shared/BarChart.razor`, `Pages/Analytics/AnalyticsPage.razor` | Indicadores; las gráficas son barras en HTML/CSS, sin librería |
| Operación | `Services/OperationsService.cs` | Servicios, movimientos, pagos de tarjeta, compras a meses y saldos |
| Comprobantes | `Services/AttachmentService.cs`, `StorageClient.cs`, `Shared/AttachmentManager.razor`, `AttachmentViewer.razor` | Subida directa a R2 con URLs prefirmadas (sin JWT), reducción de fotos, visor en la app (pdf.js en `wwwroot/lib/pdfjs`) con descarga; `AttachmentManager` tiene modo `ReadOnly` para el detalle |
| UI | `Pages/`, `Layout/`, `Shared/` | Pantallas y componentes reutilizables |

## Rutas

| Ruta | Acceso | Pantalla |
|---|---|---|
| `/` | Público | Landing (`Pages/Landing`, estilos en `css/landing.css`) |
| `/login`, `/registro` | Público | Sesión |
| `/inicio` | Con sesión | Resumen; es también el `start_url` de la PWA instalada |
| `/cuentas`, `/cuentas/nueva`, `/cuentas/{id}/editar`, `/cuentas/{id}/tarjeta` | Usuario | Alta y edición de cuentas y tarjetas de crédito |
| `/cuentas/{id}/saldo` | Usuario | Saldo de la cuenta (con periodo si es tarjeta) |
| `/categorias` | Usuario | Categorías |
| `/servicios`, `/movimientos`, `/planes` | Usuario | Servicios, movimientos y compras a meses |
| `/analisis` | Usuario | Gasto por mes con proyección, por categoría contra el promedio, compras a meses por cobrar, uso y pagos por corte de cada tarjeta, ingresos y ahorro, y servicios |
| `/admin/bancos`, `/admin/proveedores` | SuperAdmin | Catálogos globales |
| `/perfil` | Cualquiera con sesión | Datos de la cuenta; para usuarios, acceso con huella o rostro y recordatorios de pago |

Las ilustraciones de la landing son SVG en línea pintados con las variables de
`app.css`: siguen al tema, no pesan y funcionan sin conexión.

## Roles

El **SuperAdmin es un rol de sistema**, no un usuario con extras: sólo
administra los catálogos globales y nunca ve la parte operativa. Las reglas
viven en `Services/AppRoles.cs` y las usan por igual las políticas de las
páginas, la navegación y las redirecciones:

- Si el token trae `SuperAdmin`, ese rol manda aunque también venga `User`.
- Quien tiene sesión pero entra a una página que no le toca va a *su* inicio
  (`/admin/bancos` o `/inicio`), no al login; mandarlo al login crearía un
  bucle, porque el login lo devolvería a la misma página.

Esto es sólo la interfaz: **la API debe aplicar la misma regla**, porque un
token se puede usar directamente con curl sin pasar por la app.

## Diseño responsivo

Todo vive en `wwwroot/css/app.css`, mobile-first, con un único marcado de
navegación que el CSS presenta distinto en cada registro:

| Ancho | Navegación | Contenido |
|---|---|---|
| < 640 px | Barra inferior fija | Una columna, 520 px máx. |
| 640–1023 px | Barra inferior fija | 660 px máx., tarjetas en rejilla |
| ≥ 1024 px | Barra lateral fija de 236 px | 1040 px máx., formularios a 580 px |
| ≥ 1160 px | Barra lateral | Tablero a dos columnas |

Constantes en todos los tamaños: áreas táctiles de 44 px, campos de 16 px para
que iOS no haga zoom al enfocar, `safe-area-inset` para el notch, y un ancho
máximo de línea aunque sobre monitor.

## Estado

Implementado: landing pública en `/` (quien ya tiene sesión salta a `/inicio`), registro, login, sesión persistente y expiración, resumen con
próximos pagos de tarjeta, alta/edición de cuentas de débito-ahorro-efectivo,
alta/edición de tarjetas de crédito, listado de cuentas en tarjetas con saldo, crédito disponible y uso del límite, categorías (alta y archivar/reactivar).

También implementado según OpenAPI: servicios (alta, edición y archivo), movimientos (alta, edición, eliminación, filtros, paginación y vista de detalle de solo lectura al tocar la tarjeta), comprobantes por movimiento (tocar el comprobante lo abre en el visor, que permite descargarlo), pagos de tarjeta vinculados a una cuenta de origen, compras a meses (alta con selector de cuotas ya pagadas, calendario, liquidación anticipada y eliminación) y saldos por cuenta con periodo de tarjeta.

Funciones móviles (etapa 2 de la materia):

- **Huella o rostro**: en Perfil se activa el acceso con passkey en el dispositivo; el login ofrece "Entrar con huella o rostro". La sesión se mantiene con refresh tokens (30 días sin abrir la app).
- **Comprobantes con la cámara**: el selector de archivo de los tipos de imagen ofrece cámara o galería; las fotos se reducen a JPEG antes de subirlas.
- **Recordatorios push**: en Perfil se activan por dispositivo; avisan un día antes del corte, tres días antes del pago y el día del pago, aun con la app cerrada. En iPhone solo funcionan con la app instalada en la pantalla de inicio (iOS 16.4+).
- **Sin conexión**: la app abre sin red (service worker), muestra los últimos datos consultados y permite registrar gastos e ingresos; quedan como "Pendiente de sincronizar" y se envían solos al volver la conexión. Editar, borrar, pagos de tarjeta y comprobantes requieren conexión.
- **Atajo "Registrar gasto"**: al mantener presionado el ícono de la app instalada (Android y escritorio).

Los días de corte y pago de las tarjetas son números del 1 al 31. Los movimientos que pertenecen a una compra a meses se gestionan desde su plan. Liquidar un plan cambia su calendario; no registra un pago a la tarjeta.

## Verificación

```bash
dotnet run --project ContractTests/ContractTests.csproj
dotnet publish GastosApp.Web.csproj -c Release
```

Las pruebas comprueban contratos HTTP, validaciones, fechas, pagos vinculados y respuestas de error sin usar una base de datos. Para revisar la interfaz con datos ficticios, iniciar la app en el puerto 5025 y ejecutar:

```bash
dotnet run --project ContractTests/ContractTests.csproj -- --preview
```

La vista de prueba vive en `http://localhost:15025`; simula la API y una sesión exclusivamente en ese servidor local. `ContractTests` está excluido del cliente publicado. La verificación con la API real requiere iniciar sesión en la app normal y utilizar datos de prueba.

## Despliegue

La app se publica como un Worker de Cloudflare que sólo sirve archivos
estáticos (sin script propio). La configuración está en `wrangler.jsonc`:
sube `out/wwwroot`, la salida de `dotnet publish ... -o out`, y con
`not_found_handling: "single-page-application"` cualquier ruta desconocida
devuelve `index.html` para que Blazor la resuelva al recargar.

| Workflow | Disparador | Qué hace |
|---|---|---|
| `.github/workflows/ci.yml` | PR hacia `main` o `develop` | `dotnet publish` en Release; no despliega ni corre `ContractTests` |
| `.github/workflows/deploy.yml` | Push a `main` (o manual) | Publica en Release y despliega con Wrangler 4 |

El despliegue necesita los secrets `CLOUDFLARE_API_TOKEN` y
`CLOUDFLARE_ACCOUNT_ID` en el repositorio. Como se publica fuera de
Development, la app desplegada usa `appsettings.Production.json`; el origen
del Worker debe estar en la política CORS de la API (ver arriba).
