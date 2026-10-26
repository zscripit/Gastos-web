using GastosApp.Web.Models;
using Microsoft.JSInterop;

namespace GastosApp.Web.Services;

/// <summary>
/// IndexedDB del dispositivo: caché de consultas y cola de movimientos sin conexión, separadas por usuario.
/// Si el navegador no permite IndexedDB (modo privado en algunos navegadores), todo se degrada a "sin caché".
/// </summary>
public sealed class OfflineStore(IJSRuntime js, TokenStore tokens) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>Una petición a la API falló por red (lo avisa ApiClient; lo escucha OfflineSync).</summary>
    public event Action? NetworkUnavailable;

    /// <summary>La API volvió a responder. El navegador no siempre lo avisa (p. ej. si cayó el servidor, no la red).</summary>
    public event Action? NetworkAvailable;

    // Tras un fallo de red, durante un momento las consultas van directo a lo guardado: sin conexión cada
    // intento puede tardar segundos y una pantalla hace varias consultas seguidas.
    private static readonly TimeSpan OfflineWindow = TimeSpan.FromSeconds(20);
    private DateTimeOffset? _lastFailure;

    public bool LikelyOffline => _lastFailure is { } failure && DateTimeOffset.UtcNow - failure < OfflineWindow;

    public void ReportNetworkFailure()
    {
        _lastFailure = DateTimeOffset.UtcNow;
        NetworkUnavailable?.Invoke();
    }

    public void ReportNetworkAvailable()
    {
        _lastFailure = null;
        NetworkAvailable?.Invoke();
    }

    public async Task<string?> GetCachedAsync(string url) =>
        await KeyAsync(url) is { } key ? await TryAsync(m => m.InvokeAsync<string?>("cacheGet", key), null) : null;

    public async Task SetCachedAsync(string url, string raw)
    {
        if (await KeyAsync(url) is { } key)
            await TryAsync(async m => { await m.InvokeVoidAsync("cacheSet", key, raw); return true; }, false);
    }

    public Task ClearCacheAsync() => TryAsync(async m => { await m.InvokeVoidAsync("cacheClear"); return true; }, false);

    public async Task<List<PendingTransaction>> PendingAsync()
    {
        var userId = (await tokens.GetSessionAsync())?.UserId;
        if (userId is null) return [];
        var all = await TryAsync(m => m.InvokeAsync<List<PendingTransaction>>("queueAll"), []);
        return all.Where(p => p.UserId == userId).OrderBy(p => p.QueuedAt).ToList();
    }

    public async Task SaveAsync(PendingTransaction item)
    {
        if (string.IsNullOrEmpty(item.UserId))
            item.UserId = (await tokens.GetSessionAsync())?.UserId ?? "";
        await TryAsync(async m => { await m.InvokeVoidAsync("queuePut", item); return true; }, false);
    }

    public Task RemoveAsync(Guid clientId) =>
        TryAsync(async m => { await m.InvokeVoidAsync("queueDelete", clientId); return true; }, false);

    public Task<bool> IsOnlineAsync() => TryAsync(m => m.InvokeAsync<bool>("isOnline"), true);

    public Task<IJSObjectReference?> WatchAsync<T>(DotNetObjectReference<T> listener) where T : class =>
        TryAsync<IJSObjectReference?>(async m => await m.InvokeAsync<IJSObjectReference>("watch", listener), null);

    // La caché es por usuario: otra cuenta en el mismo dispositivo no ve datos ajenos.
    private async Task<string?> KeyAsync(string url) =>
        (await tokens.GetSessionAsync())?.UserId is { } userId ? $"{userId}:{url}" : null;

    private async Task<TResult> TryAsync<TResult>(Func<IJSObjectReference, ValueTask<TResult>> action, TResult fallback)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/offline-db.js");
            return await action(_module);
        }
        catch (JSException)
        {
            return fallback;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
