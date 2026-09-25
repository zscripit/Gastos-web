using GastosApp.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace GastosApp.Web.Services;

public sealed class AuthService(ApiClient api, TokenStore tokens, AuthenticationStateProvider stateProvider)
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
