using GastosApp.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace GastosApp.Web.Services;

public sealed class AuthService(ApiClient api, TokenStore tokens, AuthenticationStateProvider stateProvider,
    PasskeyService passkeys, OfflineStore offline)
{
    public async Task<ApiResult<AuthSession>> LoginAsync(LoginRequest request)
    {
        var result = await api.PostAsync<AuthSession>("/api/Auth/Login", request);

        if (result.Success && result.Data is not null)
        {
            await StartSessionAsync(result.Data);
        }

        return result;
    }

    /// <summary>Inicia sesión con la huella o el rostro (passkey del dispositivo).</summary>
    public async Task<ApiResult<AuthSession>> LoginWithPasskeyAsync()
    {
        var result = await passkeys.LoginAsync();

        if (result.Success && result.Data is not null)
        {
            await StartSessionAsync(result.Data);
        }

        return result;
    }

    public async Task<ApiResult<AuthSession>> RegisterAsync(RegisterRequest request)
    {
        var result = await api.PostAsync<AuthSession>("/api/Auth/Register", request);

        if (result.Success && result.Data is not null)
        {
            await StartSessionAsync(result.Data);
        }

        return result;
    }

    public async Task LogoutAsync()
    {
        // Revoca el refresh token en la API (mejor esfuerzo: sin conexión igual se cierra localmente).
        var session = await tokens.GetSessionAsync();
        if (!string.IsNullOrEmpty(session?.RefreshToken))
            await api.PostAsync<bool>("/api/Auth/Logout", new { refreshToken = session.RefreshToken });

        // Los datos guardados para usar sin conexión no se quedan en un dispositivo compartido.
        // La cola de movimientos pendientes sí: es por usuario y se envía cuando esa cuenta vuelva a entrar.
        await offline.ClearCacheAsync();
        await tokens.ClearAsync();
        Notify();
    }

    private async Task StartSessionAsync(AuthSession session)
    {
        await tokens.SetSessionAsync(session);
        Notify();
    }

    private void Notify() =>
        ((JwtAuthenticationStateProvider)stateProvider).NotifySessionChanged();
}
