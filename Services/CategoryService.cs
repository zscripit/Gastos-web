using GastosApp.Web.Models;

namespace GastosApp.Web.Services;

public sealed class CategoryService(ApiClient api)
{
    public Task<ApiResult<List<CategoryDto>>> GetAllAsync() =>
        api.GetAsync<List<CategoryDto>>("/api/Category/All");

    public Task<ApiResult<List<CategoryDto>>> GetActiveAsync() =>
        api.GetAsync<List<CategoryDto>>("/api/Category/Active");

    public Task<ApiResult<Guid>> AddAsync(SaveCategoryRequest request) =>
        api.PostAsync<Guid>("/api/Category", request);

    public Task<ApiResult<bool>> ChangeStatusAsync(Guid categoryId, bool isArchived) =>
        api.PatchAsync<bool>($"/api/Category/{categoryId}/Status",
            new ChangeStatusRequest { IsArchived = isArchived });
}
