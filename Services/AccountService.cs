using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Cuentas financieras y tarjetas de crédito. La API las separa en dos
/// recursos (/FinanceAccount y /CreditAccount) porque la tarjeta añade sus
/// propios atributos, pero para la app son la misma lista.
/// </summary>
public sealed class AccountService(ApiClient api)
{
    public Task<ApiResult<List<ActiveAccountDto>>> GetActiveAsync() =>
        api.GetAsync<List<ActiveAccountDto>>("/api/FinanceAccount/Active");

    public Task<ApiResult<List<FinanceAccountListItem>>> GetAllAsync() =>
        api.GetAsync<List<FinanceAccountListItem>>("/api/FinanceAccount/All");

    public Task<ApiResult<Guid>> AddAsync(AddFinanceAccountRequest request) =>
        api.PostAsync<Guid>("/api/FinanceAccount", new
        {
            name = request.Name,
            description = request.Description,
            bankId = request.BankId,
            accountType = (int?)request.AccountType
        });

    public Task<ApiResult<FinanceAccountListItem>> EditAsync(Guid id, EditFinanceAccountRequest request) =>
        api.PutAsync<FinanceAccountListItem>($"/api/FinanceAccount/{id}", new
        {
            financeAccountId = id,
            name = request.Name,
            description = request.Description,
            isArchived = request.IsArchived,
            bankId = request.BankId
        });

    public Task<ApiResult<Guid>> AddCreditAsync(SaveCreditAccountRequest request) =>
        api.PostAsync<Guid>("/api/CreditAccount", BuildCreditPayload(request));

    public Task<ApiResult<CreditAccountDto>> EditCreditAsync(Guid financeAccountId, SaveCreditAccountRequest request) =>
        api.PutAsync<CreditAccountDto>($"/api/CreditAccount/{financeAccountId}", BuildCreditPayload(request, includeStatus: true));

    private static object BuildCreditPayload(SaveCreditAccountRequest request, bool includeStatus = false) => new
    {
        name = request.Name,
        description = request.Description,
        bankId = request.BankId,
        creditLimit = request.CreditLimit,
        interestRate = request.InterestRate,
        // La API espera date-time con offset; el formulario captura sólo la fecha.
        cutDate = ToOffset(request.CutDate),
        payDate = ToOffset(request.PayDate),
        isArchived = includeStatus && request.IsArchived
    };

    private static DateTimeOffset? ToOffset(DateTime? date) =>
        date is null ? null : new DateTimeOffset(DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc));
}
