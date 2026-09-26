using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

/// <summary>
/// Catálogos globales: bancos y proveedores. La lectura de activos la usa
/// cualquier usuario (selectores de los formularios); el resto es exclusivo
/// del SuperAdmin.
/// </summary>
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

    public Task<ApiResult<List<ProviderDto>>> GetActiveProvidersAsync() =>
        api.GetAsync<List<ProviderDto>>("/api/Provider/Active");

    // --- Administración (SuperAdmin) ---

    public Task<ApiResult<List<CatalogItemDto>>> GetAllAsync(CatalogKind kind) =>
        api.GetAsync<List<CatalogItemDto>>($"/api/{Segment(kind)}/All");

    public async Task<ApiResult<Guid>> AddAsync(CatalogKind kind, SaveCatalogItemRequest request)
    {
        var result = await api.PostAsync<Guid>($"/api/{Segment(kind)}", new
        {
            name = request.Name.Trim(),
            iconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim()
        });

        if (result.Success) Invalidate(kind);
        return result;
    }

    public async Task<ApiResult<bool>> ChangeStatusAsync(CatalogKind kind, Guid id, bool isArchived)
    {
        var result = await api.PatchAsync<bool>($"/api/{Segment(kind)}/{id}/Status",
            new ChangeStatusRequest { IsArchived = isArchived });

        if (result.Success) Invalidate(kind);
        return result;
    }

    private void Invalidate(CatalogKind kind)
    {
        if (kind == CatalogKind.Bank) _activeBanks = null;
    }

    private static string Segment(CatalogKind kind) => kind switch
    {
        CatalogKind.Bank => "Bank",
        CatalogKind.Provider => "Provider",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

public enum CatalogKind
{
    Bank,
    Provider
}
