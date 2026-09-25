using System.ComponentModel.DataAnnotations;

namespace GastosApp.Web.Models;

/// <summary>Fila de GET /FinanceAccount/All (pantalla de administración).</summary>
public sealed class FinanceAccountListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string? BankIconUrl { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>
/// Fila de GET /FinanceAccount/Active. Trae además los datos de crédito
/// cuando la cuenta es una tarjeta, para no cruzar contra otro endpoint.
/// </summary>
public sealed class ActiveAccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string? BankIconUrl { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? InterestRate { get; set; }
    public DateTimeOffset? CutDate { get; set; }
    public DateTimeOffset? PayDate { get; set; }
    public AccountType AccountType { get; set; }

    public bool IsCredit => AccountType == AccountType.Credit;

    /// <summary>Días que faltan para la fecha de pago; negativo si ya pasó.</summary>
    public int? DaysUntilPayment => PayDate is null
        ? null
        : (int)(PayDate.Value.Date - DateTimeOffset.Now.Date).TotalDays;

    /// <summary>
    /// Avance del periodo entre la fecha de corte y la de pago, de 0 a 1.
    /// Alimenta el anillo de la tarjeta: es dato que la API ya devuelve, no
    /// una estimación de saldo (para eso harían falta las transacciones).
    /// </summary>
    public double CycleProgress
    {
        get
        {
            if (CutDate is null || PayDate is null) return 0;

            var total = (PayDate.Value.Date - CutDate.Value.Date).TotalDays;
            if (total <= 0) return 1;

            var elapsed = (DateTimeOffset.Now.Date - CutDate.Value.Date).TotalDays;
            return Math.Clamp(elapsed / total, 0, 1);
        }
    }
}

public sealed class AddFinanceAccountRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
    [MaxLength(35, ErrorMessage = "Máximo 35 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Selecciona un banco.")]
    public Guid? BankId { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de cuenta.")]
    public AllowedFinanceAccountType? AccountType { get; set; }
}

public sealed class EditFinanceAccountRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
    [MaxLength(35, ErrorMessage = "Máximo 35 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    [Required(ErrorMessage = "Selecciona un banco.")]
    public Guid? BankId { get; set; }
}

public sealed class SaveCreditAccountRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
    [MaxLength(35, ErrorMessage = "Máximo 35 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Selecciona un banco.")]
    public Guid? BankId { get; set; }

    [Required(ErrorMessage = "El límite de crédito es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Debe ser mayor que cero.")]
    public decimal? CreditLimit { get; set; }

    [Required(ErrorMessage = "La tasa de interés es obligatoria.")]
    [Range(0, 200, ErrorMessage = "Debe estar entre 0 y 200.")]
    public decimal? InterestRate { get; set; }

    [Required(ErrorMessage = "La fecha de corte es obligatoria.")]
    public DateTime? CutDate { get; set; }

    [Required(ErrorMessage = "La fecha de pago es obligatoria.")]
    public DateTime? PayDate { get; set; }

    /// <summary>Sólo se usa al editar; al crear se ignora.</summary>
    public bool IsArchived { get; set; }
}

public sealed class CreditAccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string? BankIconUrl { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal InterestRate { get; set; }
    public DateTimeOffset CutDate { get; set; }
    public DateTimeOffset PayDate { get; set; }
}
