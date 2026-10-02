using System.ComponentModel.DataAnnotations;

namespace GastosApp.Web.Models;

public enum TransactionType { Income = 1, Expense = 2, Payment = 3 }
public enum FinancePlanStatus { Active = 1, Finished = 2, All = 3 }
public enum ServiceType
{
    Luz = 1, Agua, Internet, Gas, Telefono, TelefoniaMovil, CableTV,
    RecoleccionBasura, Renta, Hipoteca, MantenimientoCondominio, Predial,
    SeguroHogar, StreamingVideo, StreamingMusica, AlmacenamientoNube,
    SuscripcionSoftware, SeguroAuto, SeguroVida, SeguroGastosMedicos,
    Gimnasio, Seguridad, Colegiatura
}

public sealed class ServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public ServiceType ServiceType { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = "";
}
public sealed class ServiceForm
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(30, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 30 caracteres.")] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public ServiceType ServiceType { get; set; } = ServiceType.Internet;
    public Guid? ProviderId { get; set; }
}
public sealed class TransactionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset Date { get; set; }
    public TransactionType TransactionType { get; set; }
    public Guid FinanceAccountId { get; set; }
    public string FinanceAccountName { get; set; } = "";
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public Guid? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public Guid? TransferId { get; set; }
}
public sealed class TransactionForm
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 50 caracteres.")] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "El monto debe ser mayor que cero.")] public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public TransactionType TransactionType { get; set; } = TransactionType.Expense;
    public Guid? FinanceAccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? SourceAccountId { get; set; }
}
public sealed class PagedTransactions
{
    public List<TransactionDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
public sealed class FinancePlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Interest { get; set; }
    public decimal Total { get; set; }
    public int Months { get; set; }
    public decimal AmountPerMonth { get; set; }
    public DateTimeOffset Date { get; set; }
    public Guid FinanceAccountId { get; set; }
    public string FinanceAccountName { get; set; } = "";
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public Guid TransactionId { get; set; }
    public int InstallmentsCharged { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateOnly? NextInstallmentDate { get; set; }
    public DateOnly? SettledOn { get; set; }
    public List<InstallmentDto> Installments { get; set; } = [];
}
public sealed class InstallmentDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public bool Charged { get; set; }
}
public sealed class FinancePlanForm
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 50 caracteres.")] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "El monto debe ser mayor que cero.")] public decimal Amount { get; set; }
    [Range(0, 100)] public decimal Interest { get; set; }
    [Range(2, 48)] public int Months { get; set; } = 12;
    public DateTime Date { get; set; } = DateTime.Today;
    public Guid? FinanceAccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public DateOnly? FirstInstallmentDate { get; set; }
}
public sealed class AccountBalanceDto
{
    public Guid FinanceAccountId { get; set; }
    public string Name { get; set; } = "";
    public AccountType AccountType { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal Balance { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? Debt { get; set; }
    public decimal? AvailableCredit { get; set; }
    public CreditPeriodDto? CurrentPeriod { get; set; }
}
public sealed class CreditPeriodDto
{
    public DateOnly Start { get; set; }
    public DateOnly CutDate { get; set; }
    public DateOnly PaymentDueDate { get; set; }
    public decimal Expenses { get; set; }
    public decimal Installments { get; set; }
    public decimal Payments { get; set; }
    public decimal Total { get; set; }
}
