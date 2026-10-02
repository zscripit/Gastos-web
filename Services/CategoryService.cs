using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

public sealed class CategoryService(ApiClient api)
{
    public Task<ApiResult<List<CategoryDto>>> GetAllAsync() =>
        api.GetAsync<List<CategoryDto>>("/api/Category/All");

    public Task<ApiResult<List<CategoryDto>>> GetActiveAsync() =>
        api.GetAsync<List<CategoryDto>>("/api/Category/Active");

    public async Task<ApiResult<Guid>> AddAsync(SaveCategoryRequest request)
    {
        var result = await api.PostAsync<CategoryDto>("/api/Category", request);
        return result.Success && result.Data is not null
            ? ApiResult<Guid>.Ok(result.Data.Id, result.StatusCode)
            : ApiResult<Guid>.Fail(result.Error ?? "No se recibió la categoría creada.", result.StatusCode, result.ValidationErrors);
    }

    public Task<ApiResult<bool>> ChangeStatusAsync(Guid categoryId, bool isArchived) =>
        api.PatchAsync<bool>($"/api/Category/{categoryId}/Status",
            new ChangeStatusRequest { IsArchived = isArchived });
}
