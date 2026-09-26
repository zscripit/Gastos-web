using System.Security.Claims;

namespace GastosApp.Web.Services;

/// <summary>
/// Roles que emite gastos-api y cómo los interpreta la app.
///
/// El SuperAdmin es un rol de sistema, no un usuario con permisos extra: sólo
/// administra catálogos (bancos, proveedores) y nunca usa la parte operativa.
/// Por eso, si el token trae SuperAdmin, ese rol manda aunque también venga
/// User; así una cuenta mal configurada con ambos roles no mezcla las dos
/// experiencias.
/// </summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string User = "User";

    public static bool IsSystemAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(SuperAdmin);

    public static bool IsOperationalUser(this ClaimsPrincipal principal) =>
        principal.IsInRole(User) && !principal.IsInRole(SuperAdmin);

    /// <summary>Pantalla de inicio que le corresponde a cada tipo de cuenta.</summary>
    public static string HomeRoute(this ClaimsPrincipal principal) =>
        principal.IsSystemAdmin() ? "admin/bancos" : "inicio";
}

/// <summary>Nombres de las políticas de autorización que usan las páginas.</summary>
public static class Policies
{
    /// <summary>Uso normal de la app: cuentas, tarjetas, categorías.</summary>
    public const string Operational = "Operational";

    /// <summary>Administración de catálogos del sistema.</summary>
    public const string SystemAdmin = "SystemAdmin";
}
