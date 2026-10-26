using GastosApp.Web.Models;
using Microsoft.JSInterop;

namespace GastosApp.Web.Services;

public enum PushState { Unsupported, NeedsInstall, Denied, Off, On }

/// <summary>
/// Recordatorios de corte y pago: suscribe el dispositivo con Web Push y registra la suscripción en la API.
/// </summary>
public sealed class PushService(ApiClient api, IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<PushState> GetStateAsync()
    {
        var module = await ModuleAsync();
        if (!await module.InvokeAsync<bool>("isSupported"))
            // iOS no expone PushManager fuera de la PWA instalada.
            return await module.InvokeAsync<bool>("isStandalone") ? PushState.Unsupported : PushState.NeedsInstall;

        if (await module.InvokeAsync<string>("permission") == "denied") return PushState.Denied;
        return await module.InvokeAsync<string?>("currentEndpoint") is null ? PushState.Off : PushState.On;
    }

    /// <summary>null si quedó activado; si no, el mensaje a mostrar.</summary>
    public async Task<string?> EnableAsync()
    {
        var key = await api.GetAsync<PushPublicKeyDto>("/api/Push/PublicKey");
        if (!key.Success || key.Data is null) return key.Error;

        PushSubscriptionDto? subscription;
        try
        {
            subscription = await (await ModuleAsync()).InvokeAsync<PushSubscriptionDto?>("subscribe", key.Data.PublicKey);
        }
        catch (JSException)
        {
            return "El navegador no permitió activar las notificaciones.";
        }
        if (subscription is null) return "Necesitas permitir las notificaciones para recibir los recordatorios.";

        var saved = await api.PostAsync<Guid>("/api/Push/Subscription", subscription);
        return saved.Success ? null : saved.Error;
    }

    public async Task<string?> DisableAsync()
    {
        var endpoint = await (await ModuleAsync()).InvokeAsync<string?>("unsubscribe");
        if (endpoint is null) return null;
        var removed = await api.PostAsync<bool>("/api/Push/Subscription/Remove", new { endpoint });
        return removed.Success ? null : removed.Error;
    }

    public Task<ApiResult<int>> SendTestAsync() => api.PostAsync<int>("/api/Push/Test", new { });

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/push.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
