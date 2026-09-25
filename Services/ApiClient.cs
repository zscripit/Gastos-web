using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Punto único de contacto con gastos-api. Traduce cualquier respuesta
/// (éxito, validación, 401, caída de red) a un <see cref="ApiResult{T}"/>,
/// para que ninguna página tenga que manejar HttpResponseMessage ni excepciones.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Se dispara cuando la API rechaza el token; la usa el layout para cerrar sesión.</summary>
    public event Func<Task>? Unauthorized;

    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<ApiResult<T>> PostAsync<T>(string url, object body) =>
        SendAsync<T>(Build(HttpMethod.Post, url, body));

    public Task<ApiResult<T>> PutAsync<T>(string url, object body) =>
        SendAsync<T>(Build(HttpMethod.Put, url, body));

    public Task<ApiResult<T>> PatchAsync<T>(string url, object body) =>
        SendAsync<T>(Build(HttpMethod.Patch, url, body));

    private static HttpRequestMessage Build(HttpMethod method, string url, object body) =>
        new(method, url) { Content = JsonContent.Create(body, options: JsonOptions) };

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail("No se pudo conectar con el servidor. Revisa tu conexión.", 0);
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail("La petición tardó demasiado. Inténtalo de nuevo.", 0);
        }

        var status = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.Unauthorized && Unauthorized is not null)
        {
            await Unauthorized.Invoke();
        }

        var raw = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var ok = Parse<ApiResponse<T>>(raw);
            return ApiResult<T>.Ok(ok is null ? default : ok.Data, status);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = Parse<ApiResponse<Dictionary<string, string[]>>>(raw);
            return ApiResult<T>.Fail(
                validation?.Error ?? validation?.Message ?? "Revisa los datos capturados.",
                status,
                validation?.Data);
        }

        var failure = Parse<ApiResponse<object>>(raw);
        return ApiResult<T>.Fail(
            failure?.Error ?? failure?.Message ?? DefaultMessage(response.StatusCode),
            status);
    }

    private static TValue? Parse<TValue>(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return default;

        try
        {
            return JsonSerializer.Deserialize<TValue>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string DefaultMessage(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "Tu sesión expiró. Inicia sesión de nuevo.",
        HttpStatusCode.Forbidden => "No tienes permiso para realizar esta acción.",
        HttpStatusCode.NotFound => "No se encontró el recurso solicitado.",
        HttpStatusCode.Conflict => "El registro ya existe.",
        HttpStatusCode.Locked => "La cuenta está bloqueada temporalmente.",
        _ => "Ocurrió un error inesperado."
    };
}
