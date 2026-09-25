using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace GastosApp.Web.Services;

/// <summary>
/// Deriva el estado de autenticación del JWT guardado en localStorage.
/// La validación real la hace la API: aquí sólo se leen los claims para
/// pintar la interfaz y decidir a qué rutas se puede entrar.
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore tokens) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await tokens.GetSessionAsync();

        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return Anonymous;
        }

        var claims = ParseClaims(session.AccessToken);
        var identity = new ClaimsIdentity(claims, authenticationType: "jwt", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifySessionChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return [];

        Dictionary<string, JsonElement>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                Convert.FromBase64String(PadBase64(parts[1])));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return [];
        }

        if (payload is null) return [];

        var claims = new List<Claim>();

        foreach (var (key, value) in payload)
        {
            var type = NormalizeClaimType(key);

            if (value.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(value.EnumerateArray()
                    .Select(item => new Claim(type, item.ToString())));
            }
            else
            {
                claims.Add(new Claim(type, value.ToString()));
            }
        }

        return claims;
    }

    /// <summary>Los claims cortos del JWT se mapean a los tipos que espera ClaimsPrincipal.</summary>
    private static string NormalizeClaimType(string key) => key switch
    {
        "role" or "roles" => ClaimTypes.Role,
        "sub" or "nameid" => ClaimTypes.NameIdentifier,
        "email" => ClaimTypes.Email,
        "name" or "unique_name" => ClaimTypes.Name,
        _ => key
    };

    private static string PadBase64(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        return (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };
    }
}
