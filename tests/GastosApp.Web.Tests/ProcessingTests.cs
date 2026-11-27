using GastosApp.Web.Models;
using GastosApp.Web.Services;

namespace GastosApp.Web.Tests;

/// <summary>
/// Método de procesamiento: OperationsService.NewTransaction (Services/OperationsService.cs).
/// Convierte el formulario en el cuerpo del alta, el mismo que se guarda en la cola sin conexión.
///
///   D1 categoryId  = IsMovement(tipo) ? null : form.CategoryId
///   D2 serviceId   = tipo == Expense  ? form.ServiceId : null
///   D3 sourceId    = IsMovement(tipo) ? form.SourceAccountId : null
///   IsMovement(t)  = t == Payment [A]  ||  (t == TransferIn [B]  ||  t == TransferOut [C])
///
/// Complejidad ciclomática del método (D1, D2, D3) = 4; IsMovement agrega 3 condiciones simples.
/// Cobertura de condiciones: cada A, B y C toma V y F, y cada decisión toma V y F.
/// </summary>
public class NewTransactionTests
{
    private static readonly Guid Account = Guid.NewGuid(), Category = Guid.NewGuid(), Service = Guid.NewGuid(), Source = Guid.NewGuid();

    private static TransactionForm Form(TransactionType type) => new()
    {
        Name = "Movimiento", Amount = 100m, Date = new DateTime(2026, 11, 10), TransactionType = type,
        FinanceAccountId = Account, CategoryId = Category, ServiceId = Service, SourceAccountId = Source
    };

    // A=F, B=F, C=F → IsMovement=F; D2=V (gasto). Conserva categoría y servicio, descarta origen.
    [Fact]
    public void Gasto_ConservaCategoriaYServicio_SinCuentaDeOrigen()
    {
        var body = OperationsService.NewTransaction(Form(TransactionType.Expense), null);
        Assert.Equal(Category, body.CategoryId);
        Assert.Equal(Service, body.ServiceId);
        Assert.Null(body.SourceAccountId);
    }

    // A=F, B=F, C=F; D2=F (ingreso). Un ingreso nunca lleva servicio.
    [Fact]
    public void Ingreso_ConservaCategoria_SinServicioNiOrigen()
    {
        var body = OperationsService.NewTransaction(Form(TransactionType.Income), null);
        Assert.Equal(Category, body.CategoryId);
        Assert.Null(body.ServiceId);
        Assert.Null(body.SourceAccountId);
    }

    // A=V → IsMovement=V. El pago usa la categoría de sistema (null) y lleva cuenta de origen.
    [Fact]
    public void Pago_SinCategoriaNiServicio_ConCuentaDeOrigen()
    {
        var body = OperationsService.NewTransaction(Form(TransactionType.Payment), null);
        Assert.Null(body.CategoryId);
        Assert.Null(body.ServiceId);
        Assert.Equal(Source, body.SourceAccountId);
    }

    // A=F, B=V → IsMovement=V.
    [Fact]
    public void TransferenciaEntrante_SeTrataComoMovimiento()
    {
        var body = OperationsService.NewTransaction(Form(TransactionType.TransferIn), null);
        Assert.Null(body.CategoryId);
        Assert.Equal(Source, body.SourceAccountId);
    }

    // A=F, B=F, C=V → IsMovement=V. La API rechaza TransferOut, pero el cliente debe armarlo de forma consistente.
    [Fact]
    public void TransferenciaSaliente_SeTrataComoMovimiento()
    {
        var body = OperationsService.NewTransaction(Form(TransactionType.TransferOut), null);
        Assert.Null(body.CategoryId);
        Assert.Equal(Source, body.SourceAccountId);
    }

    // El clientId (idempotencia sin conexión) y los datos básicos viajan sin cambios; la fecha se envía con el desfase local.
    [Fact]
    public void ConservaClientIdYDatosBasicos()
    {
        var clientId = Guid.NewGuid();
        var body = OperationsService.NewTransaction(Form(TransactionType.Expense), clientId);
        Assert.Equal(clientId, body.ClientId);
        Assert.Equal("Movimiento", body.Name);
        Assert.Equal(100m, body.Amount);
        Assert.Equal(Account, body.FinanceAccountId);
        Assert.Equal(new DateTime(2026, 11, 10), body.Date.DateTime);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 10)), body.Date.Offset);
    }
}
