using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Renueva el token de acceso con el refresh token antes de que expire (o cuando la API responde 401).
/// Usa un HttpClient propio, sin <see cref="AuthHeaderHandler"/>, para no entrar en bucle.
/// </summary>
public sealed class SessionRefresher(IHttpClientFactory factory, TokenStore tokens)
{
    public const string ClientName = "auth-refresh";

    // Margen para renovar antes de que el token expire durante una petición.
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    // Estático a propósito: el handler de autorización vive en otro ámbito de DI (ver TokenStore)
    // y las renovaciones de toda la app deben pasar por la misma puerta.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Sesión lista para usarse, renovada si hacía falta. null si no hay sesión o ya no se puede renovar.
    /// Sin conexión devuelve la sesión que hay: el modo sin conexión no debe cerrar la sesión.
    /// </summary>
    public async Task<AuthSession?> EnsureFreshAsync(bool force = false)
    {
        var session = await tokens.GetSessionAsync();
        if (session is null) return null;
        if (!force && session.ExpiresAtUtc > DateTimeOffset.UtcNow.Add(RefreshMargin)) return session;
        if (string.IsNullOrEmpty(session.RefreshToken)) return force ? null : session;

        await Gate.WaitAsync();
        try
        {
            // Otra llamada pudo renovarla mientras esperábamos.
            var current = await tokens.GetSessionAsync();
            if (current is null || current.AccessToken != session.AccessToken) return current;
            return await RefreshAsync(current);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<AuthSession?> RefreshAsync(AuthSession session)
    {
        HttpResponseMessage response;
        try
        {
            response = await factory.CreateClient(ClientName)
                .PostAsJsonAsync("/api/Auth/Refresh", new { refreshToken = session.RefreshToken }, Json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return session;
        }

        if (response.IsSuccessStatusCode)
        {
            var renewed = (await response.Content.ReadFromJsonAsync<ApiResponse<AuthSession>>(Json))?.Data;
            if (renewed is null) return session;
            await tokens.SetSessionAsync(renewed);
            return renewed;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Otra pestaña renovó con el mismo token: su sesión nueva ya está (o estará) guardada.
            await Task.Delay(800);
            return await tokens.GetSessionAsync();
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Locked)
        {
            await tokens.ClearAsync();
            return null;
        }

        return session;
    }
}
