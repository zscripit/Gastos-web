namespace GastosApp.Web.Models;

/// <summary>GET /Analytics/Monthly: gasto por mes y resumen del mes en curso.</summary>
public sealed class MonthlySpendingDto
{
    public List<MonthlySpendingItem> Months { get; set; } = [];
    public CurrentMonthSpending CurrentMonth { get; set; } = new();
}

public sealed class MonthlySpendingItem
{
    public DateOnly Month { get; set; }
    /// <summary>Gastos normales (sin compras a meses).</summary>
    public decimal Expenses { get; set; }
    /// <summary>Compras a meses hechas en el mes, por su total.</summary>
    public decimal PlanPurchases { get; set; }
    public decimal Total { get; set; }
    public decimal Income { get; set; }
}

public sealed class CurrentMonthSpending
{
    public decimal SpentToDate { get; set; }
    public decimal Projected { get; set; }
    public int DaysElapsed { get; set; }
    public int DaysInMonth { get; set; }
    public decimal PreviousMonthToDate { get; set; }
    public decimal PreviousMonthTotal { get; set; }

    /// <summary>Cambio contra el mes anterior al mismo día; null si el mes anterior fue 0.</summary>
    public decimal? ChangeVsPrevious => PreviousMonthToDate == 0 ? null : (SpentToDate - PreviousMonthToDate) / PreviousMonthToDate;
}

/// <summary>GET /Analytics/Categories: gasto por categoría contra el promedio de los meses anteriores.</summary>
public sealed class CategorySpendingDto
{
    public DateOnly Month { get; set; }
    public int AverageMonths { get; set; }
    public List<CategorySpendingItem> Categories { get; set; } = [];
}

public sealed class CategorySpendingItem
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Average { get; set; }

    /// <summary>Cambio contra el promedio; null sin historial o con promedio 0.</summary>
    public decimal? ChangeVsAverage => Average is > 0 ? (Amount - Average.Value) / Average.Value : null;
}

/// <summary>GET /Analytics/Commitments: cuotas por cobrar, por mes y por compra.</summary>
public sealed class CommitmentsDto
{
    public List<CommitmentMonth> Months { get; set; } = [];
}

public sealed class CommitmentMonth
{
    public DateOnly Month { get; set; }
    public decimal Total { get; set; }
    public List<CommitmentPlan> Plans { get; set; } = [];
}

public sealed class CommitmentPlan
{
    public Guid FinancePlanId { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>GET /Analytics/Cards: últimos cortes cerrados de cada tarjeta.</summary>
public sealed class CardStatementsResponse
{
    public List<CardStatements> Cards { get; set; } = [];
}

public sealed class CardStatements
{
    public Guid FinanceAccountId { get; set; }
    public string Name { get; set; } = "";
    public decimal CreditLimit { get; set; }
    /// <summary>Del corte más antiguo al más reciente.</summary>
    public List<CardStatement> Statements { get; set; } = [];
}

public sealed class CardStatement
{
    public DateOnly CutDate { get; set; }
    public DateOnly PaymentDueDate { get; set; }
    /// <summary>Lo que cortó: gastos + cuotas del periodo.</summary>
    public decimal Total { get; set; }
    /// <summary>Pagos desde el corte hasta la fecha límite (u hoy).</summary>
    public decimal Paid { get; set; }
    public decimal DebtAtCut { get; set; }
    public decimal Usage { get; set; }
    public bool DueDatePassed { get; set; }
}

/// <summary>GET /Analytics/Services: gasto mensual por servicio.</summary>
public sealed class ServiceSpendingResponse
{
    public List<DateOnly> Months { get; set; } = [];
    public List<ServiceSpending> Services { get; set; } = [];
}

public sealed class ServiceSpending
{
    public Guid ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public List<decimal> Amounts { get; set; } = [];
    public decimal Current { get; set; }
    public decimal? PreviousAverage { get; set; }

    public decimal? ChangeVsAverage => PreviousAverage is > 0 && Current > 0 ? (Current - PreviousAverage.Value) / PreviousAverage.Value : null;
}

/// <summary>Columna de BarChart: segmentos apilados de abajo hacia arriba y, opcional, una proyección.</summary>
public sealed record ChartColumn(string Label, IReadOnlyList<ChartSegment> Segments, decimal? Ghost = null)
{
    public decimal Total => Segments.Sum(s => s.Value);
}

/// <summary>Segmento de una columna; Series es el índice de color (0 = acento).</summary>
public sealed record ChartSegment(decimal Value, int Series);
