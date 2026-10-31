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
public sealed class ApiClient(HttpClient http, SessionRefresher? refresher = null, OfflineStore? offline = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Consultas que se guardan para mostrarlas sin conexión (no las URLs temporales de comprobantes).
    private static readonly string[] CacheablePrefixes =
        ["/api/FinanceAccount", "/api/CreditAccount", "/api/Category", "/api/Service", "/api/Transaction", "/api/FinancePlan", "/api/Bank", "/api/Provider", "/api/Analytics"];

    // Estático: ApiClient es transitorio (cliente tipado) y cada servicio recibe su propia instancia;
    // el layout debe enterarse del 401 venga de la instancia que venga.
    private static Func<Task>? s_unauthorized;

    /// <summary>Se dispara cuando la API rechaza el token; la usa el layout para cerrar sesión.</summary>
    public event Func<Task>? Unauthorized
    {
        add => s_unauthorized += value;
        remove => s_unauthorized -= value;
    }

    // Las peticiones se pasan como fábrica: un HttpRequestMessage no se puede enviar dos veces
    // y tras un 401 se reintenta una vez con la sesión renovada.
    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url),
            CacheablePrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? url : null);

    public Task<ApiResult<T>> PostAsync<T>(string url, object body) =>
        SendAsync<T>(() => Build(HttpMethod.Post, url, body));

    public Task<ApiResult<T>> PutAsync<T>(string url, object body) =>
        SendAsync<T>(() => Build(HttpMethod.Put, url, body));

    public Task<ApiResult<T>> PatchAsync<T>(string url, object body) =>
        SendAsync<T>(() => Build(HttpMethod.Patch, url, body));

    public Task<ApiResult<T>> DeleteAsync<T>(string url) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Delete, url));

    private static HttpRequestMessage Build(HttpMethod method, string url, object body) =>
        new(method, url) { Content = JsonContent.Create(body, options: JsonOptions) };

    /// <param name="cacheKey">Si no es null, la respuesta se guarda y se usa cuando no hay conexión.</param>
    private async Task<ApiResult<T>> SendAsync<T>(Func<HttpRequestMessage> request, string? cacheKey = null)
    {
        if (cacheKey is not null && offline?.LikelyOffline == true && await CachedAsync<T>(cacheKey) is { } recent)
            return recent;

        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request());

            // Token vencido o revocado: se renueva una vez y se reintenta antes de cerrar la sesión.
            if (response.StatusCode == HttpStatusCode.Unauthorized && refresher is not null
                && await refresher.EnsureFreshAsync(force: true) is not null)
            {
                response.Dispose();
                response = await http.SendAsync(request());
            }
        }
        catch (HttpRequestException)
        {
            return await FromCacheAsync<T>(cacheKey) ?? ApiResult<T>.Fail("No se pudo conectar con el servidor. Revisa tu conexión.", 0);
        }
        catch (TaskCanceledException)
        {
            return await FromCacheAsync<T>(cacheKey) ?? ApiResult<T>.Fail("La petición tardó demasiado. Inténtalo de nuevo.", 0);
        }

        var status = (int)response.StatusCode;
        offline?.ReportNetworkAvailable();

        if (response.StatusCode == HttpStatusCode.Unauthorized && s_unauthorized is not null)
        {
            await s_unauthorized.Invoke();
        }

        string raw;
        try { raw = await response.Content.ReadAsStringAsync(); }
        catch (HttpRequestException) { return ApiResult<T>.Fail("No se pudo leer la respuesta del servidor.", status); }
        catch (TaskCanceledException) { return ApiResult<T>.Fail("La petición tardó demasiado. Inténtalo de nuevo.", status); }

        if (response.IsSuccessStatusCode)
        {
            var ok = Parse<ApiResponse<T>>(raw);
            if (ok is null && response.StatusCode != HttpStatusCode.NoContent)
                return ApiResult<T>.Fail("El servidor devolvió una respuesta que no se pudo interpretar.", status);
            if (cacheKey is not null && offline is not null) await offline.SetCachedAsync(cacheKey, raw);
            return ApiResult<T>.Ok(ok is null ? default : ok.Data, status);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = Parse<ApiResponse<Dictionary<string, string[]>>>(raw);
            return ApiResult<T>.Fail(
                validation?.Message ?? validation?.Error ?? "Revisa los datos capturados.",
                status,
                validation?.Data);
        }

        var failure = Parse<ApiResponse<object>>(raw);
        return ApiResult<T>.Fail(
            failure?.Message ?? failure?.Error ?? DefaultMessage(response.StatusCode),
            status);
    }

    // Sin red: la última respuesta guardada de esa consulta, marcada como FromCache.
    private async Task<ApiResult<T>?> FromCacheAsync<T>(string? cacheKey)
    {
        if (offline is null) return null;
        offline.ReportNetworkFailure();
        return cacheKey is null ? null : await CachedAsync<T>(cacheKey);
    }

    private async Task<ApiResult<T>?> CachedAsync<T>(string cacheKey)
    {
        if (offline is null || await offline.GetCachedAsync(cacheKey) is not { } raw) return null;
        return Parse<ApiResponse<T>>(raw) is { } cached ? ApiResult<T>.Ok(cached.Data, 200) with { FromCache = true } : null;
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
        HttpStatusCode.TooManyRequests => "Demasiadas solicitudes seguidas. Espera un momento e inténtalo de nuevo.",
        _ => "Ocurrió un error inesperado."
    };
}
