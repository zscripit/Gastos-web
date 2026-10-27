using System.Globalization;
using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

public sealed class AnalyticsService(ApiClient api)
{
    public Task<ApiResult<MonthlySpendingDto>> MonthlyAsync(int months = 12) =>
        api.GetAsync<MonthlySpendingDto>($"/api/Analytics/Monthly?months={months}");

    public Task<ApiResult<CategorySpendingDto>> CategoriesAsync(DateOnly? month = null) =>
        api.GetAsync<CategorySpendingDto>(month is DateOnly m
            ? $"/api/Analytics/Categories?month={m.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : "/api/Analytics/Categories");

    public Task<ApiResult<CommitmentsDto>> CommitmentsAsync(int months = 12) =>
        api.GetAsync<CommitmentsDto>($"/api/Analytics/Commitments?months={months}");

    public Task<ApiResult<CardStatementsResponse>> CardsAsync(int months = 6) =>
        api.GetAsync<CardStatementsResponse>($"/api/Analytics/Cards?months={months}");

    public Task<ApiResult<ServiceSpendingResponse>> ServicesAsync(int months = 6) =>
        api.GetAsync<ServiceSpendingResponse>($"/api/Analytics/Services?months={months}");
}
