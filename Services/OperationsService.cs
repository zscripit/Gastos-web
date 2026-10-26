using System.Globalization;
using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

public sealed class OperationsService(ApiClient api)
{
    public Task<ApiResult<List<ServiceDto>>> ServicesAsync(bool active = false) =>
        api.GetAsync<List<ServiceDto>>($"/api/Service/{(active ? "Active" : "All")}");
    public Task<ApiResult<ServiceDto>> SaveServiceAsync(ServiceForm form, Guid? id) => id is null
        ? api.PostAsync<ServiceDto>("/api/Service", new { form.Name, form.Description, form.ServiceType, form.ProviderId })
        : api.PutAsync<ServiceDto>($"/api/Service/{id}", new { form.Name, form.Description, form.IsArchived });
    public Task<ApiResult<PagedTransactions>> TransactionsAsync(int page, Guid? account = null,
        Guid? category = null, Guid? service = null, TransactionType? type = null, DateTime? from = null, DateTime? to = null)
    {
        var query = new List<string> { $"page={page}", "pageSize=20" };
        Add(query, "financeAccountId", account); Add(query, "categoryId", category);
        Add(query, "serviceId", service); Add(query, "transactionType", type is null ? null : (int)type);
        Add(query, "from", from is null ? null : Offset(from.Value.Date).ToString("O"));
        Add(query, "to", to is null ? null : Offset(to.Value.Date.AddDays(1).AddTicks(-1)).ToString("O"));
        return api.GetAsync<PagedTransactions>("/api/Transaction?" + string.Join("&", query));
    }
    public Task<ApiResult<TransactionDto>> TransactionAsync(Guid id) => api.GetAsync<TransactionDto>($"/api/Transaction/{id}");
    public Task<ApiResult<TransactionDto>> SaveTransactionAsync(TransactionForm form, Guid? id, Guid? clientId = null)
    {
        var categoryId = form.TransactionType == TransactionType.Payment ? null : form.CategoryId;
        var serviceId = form.TransactionType == TransactionType.Expense ? form.ServiceId : null;
        return id is null
            ? api.PostAsync<TransactionDto>("/api/Transaction", NewTransaction(form, clientId))
            : api.PutAsync<TransactionDto>($"/api/Transaction/{id}", new { form.Name, form.Description, form.Amount,
                date = Offset(form.Date), categoryId, serviceId });
    }

    /// <summary>Body del alta. Es el mismo que se guarda en la cola cuando no hay conexión.</summary>
    public static NewTransactionBody NewTransaction(TransactionForm form, Guid? clientId) => new(
        form.Name, form.Description, form.Amount, Offset(form.Date), form.TransactionType, form.FinanceAccountId,
        form.TransactionType == TransactionType.Payment ? null : form.CategoryId,
        form.TransactionType == TransactionType.Expense ? form.ServiceId : null,
        form.TransactionType == TransactionType.Payment ? form.SourceAccountId : null,
        clientId);
    public Task<ApiResult<List<Guid>>> DeleteTransactionAsync(Guid id) => api.DeleteAsync<List<Guid>>($"/api/Transaction/{id}");
    public Task<ApiResult<List<FinancePlanDto>>> PlansAsync(FinancePlanStatus status = FinancePlanStatus.All) =>
        api.GetAsync<List<FinancePlanDto>>($"/api/FinancePlan?status={(int)status}");
    public Task<ApiResult<FinancePlanDto>> PlanAsync(Guid id) => api.GetAsync<FinancePlanDto>($"/api/FinancePlan/{id}");
    public Task<ApiResult<FinancePlanDto>> AddPlanAsync(FinancePlanForm form) =>
        api.PostAsync<FinancePlanDto>("/api/FinancePlan", new { form.Name, form.Description, form.Amount,
            form.Interest, form.Months, date = Offset(form.Date), form.FinanceAccountId,
            form.CategoryId, form.FirstInstallmentDate });
    public Task<ApiResult<object>> SettleAsync(Guid id, DateOnly? date, decimal? amount) =>
        api.PostAsync<object>($"/api/FinancePlan/{id}/Settle", new { settleDate = date, payoffAmount = amount });
    public Task<ApiResult<object>> DeletePlanAsync(Guid id) => api.DeleteAsync<object>($"/api/FinancePlan/{id}");
    public Task<ApiResult<AccountBalanceDto>> BalanceAsync(Guid id) =>
        api.GetAsync<AccountBalanceDto>($"/api/FinanceAccount/{id}/Balance");
    private static DateTimeOffset Offset(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Local));
    private static void Add(List<string> query, string key, object? value)
    {
        if (value is not null) query.Add(key + "=" + Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture)!));
    }
}
