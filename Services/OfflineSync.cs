using GastosApp.Web.Models;
using Microsoft.JSInterop;

namespace GastosApp.Web.Services;

/// <summary>
/// Estado de conexión y cola de movimientos registrados sin red. Envía la cola en orden al iniciar la app,
/// cuando el navegador recupera la conexión y cada minuto mientras haya pendientes.
/// </summary>
public sealed class OfflineSync(OfflineStore store, ApiClient api) : IAsyncDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

    private DotNetObjectReference<OfflineSync>? _self;
    private IJSObjectReference? _watch;
    private CancellationTokenSource? _retry;
    private bool _started, _syncing;

    public bool IsOnline { get; private set; } = true;
    public bool IsSyncing => _syncing;
    public IReadOnlyList<PendingTransaction> Pending { get; private set; } = [];

    /// <summary>Cambió la conexión, la cola o el estado de sincronización.</summary>
    public event Action? Changed;

    /// <summary>Al menos un movimiento pendiente llegó a la API: las pantallas deben recargar.</summary>
    public event Action? Synced;

    public async Task StartAsync()
    {
        // El layout se vuelve a montar tras cerrar e iniciar sesión: basta con sincronizar lo de esa cuenta.
        if (_started)
        {
            await SyncAsync();
            return;
        }
        _started = true;

        IsOnline = await store.IsOnlineAsync();
        store.NetworkUnavailable += ReportOffline;
        store.NetworkAvailable += ReportOnline;
        _self = DotNetObjectReference.Create(this);
        _watch = await store.WatchAsync(_self);
        _retry = new CancellationTokenSource();
        _ = RetryLoopAsync(_retry.Token);

        await RefreshAsync();
        await SyncAsync();
    }

    [JSInvokable]
    public async Task OnConnectivityChanged(bool online)
    {
        IsOnline = online;
        Changed?.Invoke();
        if (online) await SyncAsync();
    }

    /// <summary>Una petición falló por red: se muestra el aviso sin esperar al evento del navegador.</summary>
    public void ReportOffline()
    {
        if (!IsOnline) return;
        IsOnline = false;
        Changed?.Invoke();
    }

    // La API respondió de nuevo: se quita el aviso y se envía lo pendiente.
    private void ReportOnline()
    {
        if (IsOnline) return;
        IsOnline = true;
        Changed?.Invoke();
        _ = SyncAsync();
    }

    public async Task EnqueueAsync(PendingTransaction item)
    {
        await store.SaveAsync(item);
        await RefreshAsync();
    }

    public async Task DiscardAsync(Guid clientId)
    {
        await store.RemoveAsync(clientId);
        await RefreshAsync();
    }

    /// <summary>Reintenta también los que la API rechazó (p. ej. después de corregir la cuenta).</summary>
    public async Task RetryAsync()
    {
        foreach (var item in Pending.Where(p => p.Error is not null))
        {
            item.Error = null;
            await store.SaveAsync(item);
        }
        await SyncAsync();
    }

    public async Task SyncAsync()
    {
        if (_syncing) return;
        await RefreshAsync();
        if (Pending.All(p => p.Error is not null)) return;

        _syncing = true;
        Changed?.Invoke();
        var sent = 0;
        try
        {
            foreach (var item in Pending.Where(p => p.Error is null).ToList())
            {
                var result = await api.PostAsync<TransactionDto>("/api/Transaction", item.Body);
                if (result.Success)
                {
                    await store.RemoveAsync(item.ClientId);
                    sent++;
                    IsOnline = true;
                    continue;
                }

                // Sin red, sin sesión o con el límite de peticiones alcanzado (429): se deja todo como
                // está y se reintenta después; no es un error del movimiento.
                if (result.StatusCode is 0 or 401 or 429)
                {
                    if (result.StatusCode == 0) IsOnline = false;
                    break;
                }

                item.Error = string.Join(" ", new[] { result.Error }.Concat(result.ValidationErrors.Values.SelectMany(x => x)));
                await store.SaveAsync(item);
            }
        }
        finally
        {
            _syncing = false;
            await RefreshAsync();
            if (sent > 0) Synced?.Invoke();
        }
    }

    private async Task RefreshAsync()
    {
        Pending = await store.PendingAsync();
        Changed?.Invoke();
    }

    private async Task RetryLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(RetryInterval, ct);
                if (Pending.Any(p => p.Error is null)) await SyncAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _retry?.Cancel();
        store.NetworkUnavailable -= ReportOffline;
        store.NetworkAvailable -= ReportOnline;
        if (_watch is not null)
        {
            try { await _watch.InvokeVoidAsync("dispose"); await _watch.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
        _self?.Dispose();
    }
}
