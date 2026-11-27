using System.ComponentModel.DataAnnotations;
using GastosApp.Web.Models;

namespace GastosApp.Web.Tests;

/// <summary>
/// Método de validación: AttachmentRules.ResolveContentType (Models/AttachmentModels.cs).
/// Decide qué Content-Type se declara al subir un comprobante, o null si el archivo no se permite.
///
///   N1 allowed = AllowedContentTypes(type); reported = browser?.Trim().ToLowerInvariant()   [C0: browser == null]
///   D1 !IsNullOrEmpty(reported) [C1]  &&  allowed.Contains(reported) [C2]   → N2 return reported
///   D2 TryGetValue(extensión) [C3]    &&  allowed.Contains(porExtensión) [C4] → N3 porExtensión : N4 null
///
/// Complejidad ciclomática V(G) = 5 predicados simples + 1 = 6.
/// </summary>
public class ResolveContentTypeTests
{
    // Camino 1: C1=V, C2=V → se respeta lo que reporta el navegador.
    [Fact]
    public void Camino1_TipoReportadoPermitido_SeDevuelveTalCual() =>
        Assert.Equal("application/pdf", AttachmentRules.ResolveContentType(AttachmentType.InvoicePdf, "application/pdf", "factura.pdf"));

    // Camino 1 con normalización: espacios y mayúsculas no deben provocar un rechazo.
    [Fact]
    public void Camino1_TipoReportadoConEspaciosYMayusculas_SeNormaliza() =>
        Assert.Equal("image/jpeg", AttachmentRules.ResolveContentType(AttachmentType.Ticket, "  IMAGE/JPEG ", "ticket.jpg"));

    // Camino 2: C0=V (navegador no reporta), C1=F, C3=V, C4=V → se deduce por extensión.
    [Fact]
    public void Camino2_NavegadorSinTipo_SeDeducePorExtension() =>
        Assert.Equal("application/xml", AttachmentRules.ResolveContentType(AttachmentType.InvoiceXml, null, "factura.XML"));

    // Camino 2 con cadena vacía: Android y Windows reportan "" para XML.
    [Fact]
    public void Camino2_TipoVacio_SeDeducePorExtension() =>
        Assert.Equal("image/png", AttachmentRules.ResolveContentType(AttachmentType.Voucher, "", "pago.png"));

    // Camino 3: C1=V, C2=F (tipo reportado no permitido), C3=V, C4=F → rechazado.
    [Fact]
    public void Camino3_TipoYExtensionNoPermitidosParaElTipo_DevuelveNull() =>
        Assert.Null(AttachmentRules.ResolveContentType(AttachmentType.InvoicePdf, "image/png", "foto.png"));

    // Camino 4: C1=V, C2=F, C3=V, C4=V → el navegador reporta mal, pero la extensión salva el archivo.
    [Fact]
    public void Camino4_TipoReportadoIncorrectoPeroExtensionValida_UsaLaExtension() =>
        Assert.Equal("application/pdf", AttachmentRules.ResolveContentType(AttachmentType.InvoicePdf, "application/octet-stream", "factura.pdf"));

    // Camino 5: C1=F, C3=F → extensión desconocida, rechazado.
    [Fact]
    public void Camino5_ExtensionDesconocida_DevuelveNull() =>
        Assert.Null(AttachmentRules.ResolveContentType(AttachmentType.Other, null, "virus.exe"));

    // Valor límite: archivo sin extensión.
    [Fact]
    public void Limite_ArchivoSinExtension_DevuelveNull() =>
        Assert.Null(AttachmentRules.ResolveContentType(AttachmentType.Ticket, "", "ticket"));

    // Valor límite: sólo espacios en el tipo reportado equivale a vacío.
    [Fact]
    public void Limite_TipoSoloEspacios_SeTrataComoVacio() =>
        Assert.Equal("image/webp", AttachmentRules.ResolveContentType(AttachmentType.Other, "   ", "captura.webp"));
}

/// <summary>
/// Reglas de entrada del formulario de movimientos (TransactionForm, Models/OperationsModels.cs):
/// nombre obligatorio de 3 a 50 caracteres y monto mayor que cero. Pruebas de valores límite.
/// </summary>
public class TransactionFormBoundaryTests
{
    private static List<ValidationResult> Validate(string name, decimal amount)
    {
        var form = new TransactionForm { Name = name, Amount = amount };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(2, false)]   // mínimo − 1
    [InlineData(3, true)]    // mínimo
    [InlineData(4, true)]    // mínimo + 1
    [InlineData(49, true)]   // máximo − 1
    [InlineData(50, true)]   // máximo
    [InlineData(51, false)]  // máximo + 1
    public void Nombre_LongitudEnLosLimites(int length, bool valid) =>
        Assert.Equal(valid, Validate(new string('a', length), 10m).All(r => !r.MemberNames.Contains(nameof(TransactionForm.Name))));

    [Fact]
    public void Nombre_Vacio_EsObligatorio() =>
        Assert.Contains(Validate("", 10m), r => r.ErrorMessage == "El nombre es obligatorio.");

    [Theory]
    [InlineData("-0.01", false)]
    [InlineData("0", false)]     // límite inferior excluido
    [InlineData("0.01", true)]   // primer valor válido
    [InlineData("79228162514264337593543950335", true)] // decimal.MaxValue
    public void Monto_EnLosLimites(string amount, bool valid) =>
        Assert.Equal(valid, Validate("Despensa", decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))
            .All(r => !r.MemberNames.Contains(nameof(TransactionForm.Amount))));
}
