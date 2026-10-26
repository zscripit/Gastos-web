namespace GastosApp.Web.Models;

/// <summary>Body de POST /api/Transaction. ClientId permite reintentar sin duplicar.</summary>
public sealed record NewTransactionBody(
    string Name,
    string? Description,
    decimal Amount,
    DateTimeOffset Date,
    TransactionType TransactionType,
    Guid? FinanceAccountId,
    Guid? CategoryId,
    Guid? ServiceId,
    Guid? SourceAccountId,
    Guid? ClientId);

/// <summary>
/// Movimiento registrado sin conexión, guardado en el dispositivo hasta que se pueda enviar.
/// Lleva los nombres de cuenta y categoría para mostrarlo en la lista sin consultar la API.
/// </summary>
public sealed class PendingTransaction
{
    public Guid ClientId { get; set; }
    public string UserId { get; set; } = "";
    public NewTransactionBody Body { get; set; } = null!;
    public string AccountName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? ServiceName { get; set; }
    public DateTimeOffset QueuedAt { get; set; }

    // Motivo por el que la API lo rechazó (p. ej. la cuenta se archivó). Con error ya no se reintenta solo.
    public string? Error { get; set; }
}
