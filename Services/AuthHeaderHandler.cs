using System.Net.Http.Headers;

namespace GastosApp.Web.Services;

/// <summary>
/// Adjunta el JWT como 'Authorization: Bearer {token}' a cada petición saliente
/// hacia la API, renovándolo antes con el refresh token si está por expirar.
/// Si no hay sesión, la petición sale sin cabecera y la API responderá 401,
/// que es lo que el cliente espera para redirigir al login.
/// </summary>
public sealed class AuthHeaderHandler(SessionRefresher refresher) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Renueva el token si está por expirar, para no gastar una petición en un 401.
        var session = await refresher.EnsureFreshAsync();

        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
