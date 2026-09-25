using Microsoft.JSInterop;

namespace GastosApp.Web.Services;

/// <summary>
/// Envoltura mínima sobre window.localStorage. Se implementa aquí en vez de
/// traer un paquete para no cargar dependencias que sólo aportarían esto.
/// </summary>
public sealed class LocalStorageService(IJSRuntime js)
{
    public async ValueTask<string?> GetAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            // Modo privado o almacenamiento bloqueado: la app debe seguir funcionando.
            return null;
        }
    }

    public async ValueTask SetAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException) { }
    }

    public async ValueTask RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException) { }
    }
}
