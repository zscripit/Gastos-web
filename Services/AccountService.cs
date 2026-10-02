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

    public async Task<ApiResult<Guid>> AddAsync(AddFinanceAccountRequest request)
    {
        var result = await api.PostAsync<FinanceAccountListItem>("/api/FinanceAccount", new
        {
            name = request.Name, description = request.Description,
            bankId = request.BankId, accountType = (int?)request.AccountType
        });
        return result.Success && result.Data is not null
            ? ApiResult<Guid>.Ok(result.Data.Id, result.StatusCode)
            : ApiResult<Guid>.Fail(result.Error ?? "No se recibió la cuenta creada.", result.StatusCode, result.ValidationErrors);
    }

    public Task<ApiResult<FinanceAccountListItem>> EditAsync(Guid id, EditFinanceAccountRequest request) =>
        api.PutAsync<FinanceAccountListItem>($"/api/FinanceAccount/{id}", new
        {
            financeAccountId = id,
            name = request.Name,
            description = request.Description,
            isArchived = request.IsArchived,
            bankId = request.BankId
        });

    public async Task<ApiResult<Guid>> AddCreditAsync(SaveCreditAccountRequest request)
    {
        var result = await api.PostAsync<CreditAccountDto>("/api/CreditAccount", BuildCreditPayload(request));
        return result.Success && result.Data is not null
            ? ApiResult<Guid>.Ok(result.Data.Id, result.StatusCode)
            : ApiResult<Guid>.Fail(result.Error ?? "No se recibió la tarjeta creada.", result.StatusCode, result.ValidationErrors);
    }

    public Task<ApiResult<CreditAccountDto>> EditCreditAsync(Guid financeAccountId, SaveCreditAccountRequest request) =>
        api.PutAsync<CreditAccountDto>($"/api/CreditAccount/{financeAccountId}", BuildCreditPayload(request, includeStatus: true));

    private static object BuildCreditPayload(SaveCreditAccountRequest request, bool includeStatus = false) => new
    {
        name = request.Name,
        description = request.Description,
        bankId = request.BankId,
        creditLimit = request.CreditLimit,
        interestRate = request.InterestRate,
        cutDay = request.CutDay,
        payDay = request.PayDay,
        isArchived = includeStatus && request.IsArchived
    };


}
