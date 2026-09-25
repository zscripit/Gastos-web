using System.Net.Http.Headers;

namespace GastosApp.Web.Services;

/// <summary>
/// Adjunta el JWT como 'Authorization: Bearer {token}' a cada petición saliente
/// hacia la API. Si no hay sesión, la petición sale sin cabecera y la API
/// responderá 401, que es lo que el cliente espera para redirigir al login.
/// </summary>
public sealed class AuthHeaderHandler(TokenStore tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = await tokens.GetSessionAsync();

        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
