using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Comprobantes de movimientos. La subida es en tres pasos: la API registra el comprobante y da una URL,
/// el archivo va directo al storage y al final la API confirma que llegó completo.
/// </summary>
public sealed class AttachmentService(ApiClient api, StorageClient storage)
{
    public Task<ApiResult<List<AttachmentUploadTicket>>> RequestUploadsAsync(Guid transactionId,
        IEnumerable<StagedAttachment> files) =>
        api.PostAsync<List<AttachmentUploadTicket>>($"/api/Transaction/{transactionId}/Attachments", new
        {
            attachments = files.Select(f => new { type = f.Type, fileName = f.FileName, contentType = f.ContentType, sizeBytes = f.Data.LongLength })
        });

    public async Task<ApiResult<AttachmentDto>> ConfirmAsync(Guid transactionId, Guid attachmentId)
    {
        var result = await api.PostAsync<AttachmentDto>($"/api/Transaction/{transactionId}/Attachments/{attachmentId}/Confirm", new { });
        // La confirmación no trae el estado: si respondió bien, ya está subido.
        if (result.Data is not null) result.Data.Status = AttachmentStatus.Uploaded;
        return result;
    }

    public Task<ApiResult<AttachmentUrlDto>> GetUrlAsync(Guid attachmentId) =>
        api.GetAsync<AttachmentUrlDto>($"/api/Attachment/{attachmentId}/Url");

    public Task<ApiResult<Guid>> DeleteAsync(Guid attachmentId) =>
        api.DeleteAsync<Guid>($"/api/Attachment/{attachmentId}");

    public Task<string?> GetTextAsync(string url) => storage.GetTextAsync(url);

    /// <summary>
    /// Sube varios archivos de un movimiento y devuelve el resultado de cada uno. Si un archivo
    /// falla, se libera su lugar para poder reintentar sin esperar a la limpieza del servidor.
    /// </summary>
    public async Task<List<AttachmentUploadResult>> UploadAsync(Guid transactionId, IReadOnlyList<StagedAttachment> files)
    {
        if (files.Count == 0) return [];

        var tickets = await RequestUploadsAsync(transactionId, files);
        if (!tickets.Success || tickets.Data is null || tickets.Data.Count != files.Count)
        {
            var error = tickets.Success
                ? "El servidor devolvió una respuesta que no se pudo interpretar."
                : string.Join(" ", new[] { tickets.Error }.Concat(tickets.ValidationErrors.Values.SelectMany(x => x)).Distinct());
            return files.Select(f => new AttachmentUploadResult(f, false, error)).ToList();
        }

        // La API responde en el mismo orden en que se enviaron los archivos.
        var uploads = files.Select((file, i) => UploadOneAsync(transactionId, tickets.Data[i], file));
        return (await Task.WhenAll(uploads)).ToList();
    }

    private async Task<AttachmentUploadResult> UploadOneAsync(Guid transactionId, AttachmentUploadTicket ticket, StagedAttachment file)
    {
        var uploadError = await storage.UploadAsync(ticket, file);
        if (uploadError is not null)
        {
            await DeleteAsync(ticket.AttachmentId);
            return new(file, false, uploadError);
        }

        var confirmed = await ConfirmAsync(transactionId, ticket.AttachmentId);
        if (confirmed.Success) return new(file, true, null);

        // 409: el archivo no llegó, se libera el lugar. 400: la API ya lo eliminó por no coincidir.
        // Un fallo de red se deja pendiente: el archivo sí subió y se puede verificar después.
        if (confirmed.StatusCode == 409) await DeleteAsync(ticket.AttachmentId);
        return new(file, false, confirmed.Error);
    }
}
