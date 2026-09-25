using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>Catálogos globales: bancos y proveedores.</summary>
public sealed class CatalogService(ApiClient api)
{
    private IReadOnlyList<BankDto>? _activeBanks;

    /// <summary>
    /// Bancos activos, cacheados en memoria: alimentan varios formularios y
    /// el catálogo casi no cambia durante una sesión.
    /// </summary>
    public async Task<IReadOnlyList<BankDto>> GetActiveBanksAsync(bool forceReload = false)
    {
        if (_activeBanks is not null && !forceReload) return _activeBanks;

        var result = await api.GetAsync<List<BankDto>>("/api/Bank/Active");
        _activeBanks = result.Data ?? [];
        return _activeBanks;
    }

    public Task<ApiResult<List<BankDto>>> GetAllBanksAsync() =>
        api.GetAsync<List<BankDto>>("/api/Bank/All");

    public Task<ApiResult<List<ProviderDto>>> GetActiveProvidersAsync() =>
        api.GetAsync<List<ProviderDto>>("/api/Provider/Active");

    public Task<ApiResult<List<ProviderDto>>> GetAllProvidersAsync() =>
        api.GetAsync<List<ProviderDto>>("/api/Provider/All");
}
