# Gastos — app web (Blazor WebAssembly PWA)

Cliente instalable en móvil para `gastos-api`. No tiene lógica de negocio propia:
todo sale de la API REST, que es la única fuente de verdad.

## Cómo correrlo

```bash
dotnet run --launch-profile http     # http://localhost:5025
```

La URL de la API se configura en `wwwroot/appsettings.json` (`ApiBaseUrl`),
por defecto `http://localhost:5123`.

### Requisito en el backend: CORS

La API debe permitir el origen de la app web, o el navegador bloquea toda
petición en el preflight. En `Program.cs` de `gastos-api`:

```csharp
builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins("http://localhost:5025", "https://localhost:7244")
    .AllowAnyHeader()
    .AllowAnyMethod()));

app.UseCors("web");   // antes de UseAuthentication/UseAuthorization
```

## Arquitectura

| Capa | Ubicación | Qué hace |
|---|---|---|
| Modelos | `Models/` | DTOs y requests espejo de los schemas de OpenAPI |
| Infraestructura | `Services/ApiClient.cs` | Única puerta a la API; traduce toda respuesta a `ApiResult<T>` |
| Sesión | `Services/TokenStore.cs`, `AuthHeaderHandler.cs`, `JwtAuthenticationStateProvider.cs` | Guarda el JWT en localStorage, lo adjunta a cada petición y deriva los claims |
| Features | `Services/AccountService.cs`, `CategoryService.cs`, `CatalogService.cs` | Un servicio por entidad de la API |
| UI | `Pages/`, `Layout/`, `Shared/` | Pantallas y componentes reutilizables |

## Rutas

| Ruta | Acceso | Pantalla |
|---|---|---|
| `/` | Público | Landing (`Pages/Landing`, estilos en `css/landing.css`) |
| `/login`, `/registro` | Público | Sesión |
| `/inicio` | Con sesión | Resumen; es también el `start_url` de la PWA instalada |
| `/cuentas`, `/categorias` | Usuario | Administración de sus finanzas |
| `/admin/bancos`, `/admin/proveedores` | SuperAdmin | Catálogos globales |
| `/perfil` | Cualquiera con sesión | Datos de la cuenta |

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
alta/edición de tarjetas de crédito, categorías (alta y archivar/reactivar).

También implementado según OpenAPI: servicios (alta, edición y archivo), movimientos (alta, edición, eliminación, filtros y paginación), pagos de tarjeta vinculados a una cuenta de origen, compras a meses (alta, calendario, liquidación anticipada y eliminación) y saldos por cuenta con periodo de tarjeta.

Rutas operativas nuevas: `/servicios`, `/movimientos`, `/planes` y `/cuentas/{id}/saldo`. Los días de corte y pago de las tarjetas son números del 1 al 31. Los movimientos que pertenecen a una compra a meses se gestionan desde su plan. Liquidar un plan cambia su calendario; no registra un pago a la tarjeta.

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
