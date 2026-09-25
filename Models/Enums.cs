namespace GastosApp.Web.Models;

public enum AccountType
{
    Debit = 1,
    Credit = 2,
    Cash = 3,
    Savings = 4
}

/// <summary>Tipos permitidos por POST /api/FinanceAccount (crédito se da de alta aparte).</summary>
public enum AllowedFinanceAccountType
{
    Debit = 1,
    Cash = 3,
    Savings = 4
}

public static class AccountTypeExtensions
{
    public static string DisplayName(this AccountType type) => type switch
    {
        AccountType.Debit => "Débito",
        AccountType.Credit => "Crédito",
        AccountType.Cash => "Efectivo",
        AccountType.Savings => "Ahorro",
        _ => "Otra"
    };

    public static string DisplayName(this AllowedFinanceAccountType type) =>
        ((AccountType)(int)type).DisplayName();
}
