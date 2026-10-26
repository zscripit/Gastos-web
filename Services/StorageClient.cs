using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Habla directo con el storage de comprobantes (R2) usando las URLs prefirmadas que da la API.
/// Usa un HttpClient propio, sin <see cref="AuthHeaderHandler"/>: el JWT nunca debe salir hacia el storage.
/// </summary>
public sealed class StorageClient(HttpClient http)
{
    /// <summary>Sube el archivo con el método y los headers que indicó la API. null si salió bien; si no, el error.</summary>
    public async Task<string?> UploadAsync(AttachmentUploadTicket ticket, StagedAttachment file)
    {
        var request = new HttpRequestMessage(new HttpMethod(ticket.UploadMethod), ticket.UploadUrl)
        {
            Content = new ByteArrayContent(file.Data)
        };

        // El Content-Type va firmado en la URL: debe ser exactamente el de la API, no el del navegador.
        // Lo que no es header de petición (Content-Type, Content-Length…) es header del contenido.
        foreach (var (name, value) in ticket.UploadHeaders)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
                request.Content.Headers.TryAddWithoutValidation(name, value);
        }
        if (request.Content.Headers.ContentType is null)
            request.Content.Headers.TryAddWithoutValidation("Content-Type", file.ContentType);

        try
        {
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? null : $"El almacenamiento rechazó el archivo ({(int)response.StatusCode}).";
        }
        catch (HttpRequestException)
        {
            // También llega aquí un bloqueo de CORS: el navegador no deja ver más detalle.
            return "No se pudo subir el archivo. Revisa tu conexión.";
        }
        catch (TaskCanceledException)
        {
            return "La subida tardó demasiado. Inténtalo de nuevo.";
        }
    }

    /// <summary>Lee un archivo de texto (XML) desde su URL temporal. null si no se pudo.</summary>
    public async Task<string?> GetTextAsync(string url)
    {
        try
        {
            return await http.GetStringAsync(url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
