using System.Text.Json;
using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Guarda la sesión (token JWT, refresh token y sus expiraciones) en localStorage.
///
/// Lee siempre del almacenamiento, sin caché en memoria: IHttpClientFactory
/// construye los message handlers en un ámbito de DI propio, así que el
/// handler de autorización recibe una instancia distinta de esta clase que el
/// resto de la app. Con caché, esa instancia se quedaba con el estado del
/// arranque y nunca veía la sesión iniciada después.
/// </summary>
public sealed class TokenStore(LocalStorageService storage)
{
    private const string StorageKey = "gastosapp.session";

    public async ValueTask<AuthSession?> GetSessionAsync()
    {
        var raw = await storage.GetAsync(StorageKey);
        if (raw is null) return null;

        var session = Deserialize(raw);

        if (session is null) return null;

        // Un token de acceso vencido no termina la sesión si todavía se puede renovar.
        if (session.ExpiresAtUtc <= DateTimeOffset.UtcNow && !session.CanRefresh)
        {
            await ClearAsync();
            return null;
        }

        return session;
    }

    public ValueTask SetSessionAsync(AuthSession session) =>
        storage.SetAsync(StorageKey, JsonSerializer.Serialize(session));

    public ValueTask ClearAsync() => storage.RemoveAsync(StorageKey);

    private static AuthSession? Deserialize(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<AuthSession>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
