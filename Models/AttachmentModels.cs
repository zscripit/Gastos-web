namespace GastosApp.Web.Models;

public enum AttachmentType { InvoicePdf = 1, InvoiceXml = 2, Ticket = 3, Voucher = 4, Other = 5 }
public enum AttachmentStatus { Pending = 1, Uploaded = 2 }

/// <summary>Metadatos de un comprobante; el archivo se pide aparte con su URL temporal.</summary>
public sealed class AttachmentDto
{
    public Guid Id { get; set; }
    public AttachmentType Type { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public AttachmentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
}

/// <summary>Respuesta de POST /api/Transaction/{id}/Attachments: a dónde y cómo subir cada archivo.</summary>
public sealed class AttachmentUploadTicket
{
    public Guid AttachmentId { get; set; }
    public AttachmentType Type { get; set; }
    public int Slot { get; set; }
    public string UploadUrl { get; set; } = "";
    public string UploadMethod { get; set; } = "PUT";
    public Dictionary<string, string> UploadHeaders { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AttachmentUrlDto
{
    public string Url { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Archivo ya leído (y reducido, si es foto) listo para subirse. Se guarda en memoria porque
/// el InputFile puede desaparecer del DOM antes de la subida, p. ej. al guardar un movimiento nuevo.
/// </summary>
public sealed class StagedAttachment
{
    public required AttachmentType Type { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
}

public sealed record AttachmentUploadResult(StagedAttachment File, bool Success, string? Error);

/// <summary>Espejo de AttachmentRules de gastos-api: se valida antes de pedir la subida.</summary>
public static class AttachmentRules
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // Fotos por encima de esto se reducen antes de subirlas (JPEG, lado mayor de 2048 px).
    public const long ImageReduceThresholdBytes = 1024 * 1024;
    public const int ImageMaxDimension = 2048;

    public static readonly AttachmentType[] Types =
        [AttachmentType.InvoicePdf, AttachmentType.InvoiceXml, AttachmentType.Ticket, AttachmentType.Voucher, AttachmentType.Other];

    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".xml"] = "application/xml",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private static readonly string[] ImageOrPdf = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static int MaxSlots(AttachmentType type) => type == AttachmentType.Other ? 3 : 1;

    public static string[] AllowedContentTypes(AttachmentType type) => type switch
    {
        AttachmentType.InvoicePdf => ["application/pdf"],
        AttachmentType.InvoiceXml => ["application/xml", "text/xml"],
        _ => ImageOrPdf
    };

    // image/* para que el móvil ofrezca cámara y galería; lo que no sea JPEG/PNG/WebP se rechaza después.
    public static string Accept(AttachmentType type) => type switch
    {
        AttachmentType.InvoicePdf => ".pdf,application/pdf",
        AttachmentType.InvoiceXml => ".xml,application/xml,text/xml",
        _ => "image/*,.pdf,application/pdf"
    };

    /// <summary>
    /// Content-Type a declarar. El navegador a veces lo deja vacío (XML en Windows/Android) o lo
    /// reporta distinto; entonces se deduce de la extensión. null si no está permitido para el tipo.
    /// </summary>
    public static string? ResolveContentType(AttachmentType type, string? browserContentType, string fileName)
    {
        var allowed = AllowedContentTypes(type);
        var reported = browserContentType?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(reported) && allowed.Contains(reported))
            return reported;

        return ExtensionToContentType.TryGetValue(Path.GetExtension(fileName), out var byExtension)
               && allowed.Contains(byExtension)
            ? byExtension
            : null;
    }

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    public static bool IsPdf(string contentType) => string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    public static bool IsXml(string contentType) => contentType.EndsWith("/xml", StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(this AttachmentType type) => type switch
    {
        AttachmentType.InvoicePdf => "Factura PDF",
        AttachmentType.InvoiceXml => "Factura XML",
        AttachmentType.Ticket => "Ticket",
        AttachmentType.Voucher => "Comprobante de pago",
        _ => "Otro"
    };

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0} KB",
        _ => $"{bytes / (1024d * 1024):0.0} MB"
    };
}
