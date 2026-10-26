using System.Text.Json;
using GastosApp.Web.Models;
using Microsoft.JSInterop;

namespace GastosApp.Web.Services;

/// <summary>
/// Passkeys: acceso con huella, rostro o PIN del dispositivo. La API genera las opciones y verifica
/// la respuesta; aquí solo se le pide al navegador que haga la ceremonia.
///
/// Safari exige que la huella se pida justo después del toque del usuario: si entre el toque y
/// navigator.credentials.* hay una petición de red, la rechaza. Por eso las opciones se piden por
/// adelantado (<see cref="PrepareLoginAsync"/>, <see cref="PrepareRegistrationAsync"/>).
/// </summary>
public sealed class PasskeyService(ApiClient api, IJSRuntime js) : IAsyncDisposable
{
    // Menor que la vigencia del estado en la API (5 min) para no usar uno a punto de expirar.
    private static readonly TimeSpan PreparedLifetime = TimeSpan.FromMinutes(4);

    private IJSObjectReference? _module;
    private (PasskeyCeremony Ceremony, DateTimeOffset At)? _login, _registration;

    public async Task<bool> IsAvailableAsync()
    {
        try { return await (await ModuleAsync()).InvokeAsync<bool>("isAvailable"); }
        catch (JSException) { return false; }
    }

    public Task<ApiResult<List<PasskeyDto>>> ListAsync() => api.GetAsync<List<PasskeyDto>>("/api/Passkey");

    public Task<ApiResult<Guid>> DeleteAsync(Guid id) => api.DeleteAsync<Guid>($"/api/Passkey/{id}");

    public async Task PrepareLoginAsync() =>
        _login = await FetchAsync("/api/Passkey/LoginOptions") is { } ceremony ? (ceremony, DateTimeOffset.UtcNow) : null;

    public async Task PrepareRegistrationAsync() =>
        _registration = await FetchAsync("/api/Passkey/RegisterOptions") is { } ceremony ? (ceremony, DateTimeOffset.UtcNow) : null;

    /// <summary>Registra una passkey de este dispositivo. null si salió bien; si no, el mensaje.</summary>
    public async Task<string?> RegisterAsync()
    {
        var ceremony = Take(ref _registration) ?? await FetchAsync("/api/Passkey/RegisterOptions");
        if (ceremony is null) return "No se pudo iniciar la activación. Revisa tu conexión.";

        var module = await ModuleAsync();
        var credential = await module.InvokeAsync<JsonElement>("create", ceremony.Options);
        if (ErrorOf(credential) is { } error) return error;

        var saved = await api.PostAsync<PasskeyDto>("/api/Passkey/Register",
            new { state = ceremony.State, credential, name = await module.InvokeAsync<string>("deviceName") });
        return saved.Success ? null : saved.Error;
    }

    /// <summary>Pide la huella o el rostro y devuelve la sesión emitida por la API.</summary>
    public async Task<ApiResult<AuthSession>> LoginAsync()
    {
        var ceremony = Take(ref _login) ?? await FetchAsync("/api/Passkey/LoginOptions");
        if (ceremony is null) return ApiResult<AuthSession>.Fail("No se pudo iniciar el acceso. Revisa tu conexión.", 0);

        var credential = await (await ModuleAsync()).InvokeAsync<JsonElement>("get", ceremony.Options);
        if (ErrorOf(credential) is { } error) return ApiResult<AuthSession>.Fail(error, 0);

        return await api.PostAsync<AuthSession>("/api/Passkey/Login", new { state = ceremony.State, credential });
    }

    // Cada ceremonia se usa una sola vez: el reto no debe repetirse.
    private static PasskeyCeremony? Take(ref (PasskeyCeremony Ceremony, DateTimeOffset At)? prepared)
    {
        var ceremony = prepared is { } p && DateTimeOffset.UtcNow - p.At < PreparedLifetime ? p.Ceremony : null;
        prepared = null;
        return ceremony;
    }

    private async Task<PasskeyCeremony?> FetchAsync(string url)
    {
        await ModuleAsync();
        var result = await api.PostAsync<PasskeyCeremony>(url, new { });
        return result.Success ? result.Data : null;
    }

    private static string? ErrorOf(JsonElement credential) =>
        credential.ValueKind == JsonValueKind.Object && credential.TryGetProperty("error", out var error)
            ? error.GetString() switch
            {
                "cancelled" => "Se canceló la verificación.",
                "exists" => "Este dispositivo ya tiene el acceso activado.",
                "unsupported" => "Este navegador no permite usar la huella o el rostro aquí.",
                _ => "No se pudo completar la verificación."
            }
            : null;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/passkeys.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
